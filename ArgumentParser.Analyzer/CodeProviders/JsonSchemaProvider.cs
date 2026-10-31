using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ArgumentParser.Internal.Utilities;

namespace ArgumentParser.Internal;

/// <summary>
/// Writes the JSON Schema of a tool (<see cref="ToolSpec"/>) and the manifest of the tools of a tree, the way the Model Context Protocol (MCP)
/// lists tools. The JSON is written from the spec only, so it says what the JSON binder accepts. It is compact (no whitespace) and has no
/// <c>$schema</c>: MCP takes an input schema without one as JSON Schema 2020-12.
/// </summary>
public static class JsonSchemaProvider
{
	/// <summary>
	/// What the description of a DateTime field says about the text it takes: what the runtime ValueParser reads without UseCurrentCulture.
	/// </summary>
	public const string DateTimeNote = "ISO 8601: a date (2026-10-26), or a date and a time (2026-10-26T14:30, 2026-10-26T14:30:15.5), each optionally followed by Z or an offset (+02:00)";

	/// <summary>
	/// What the description of a TimeSpan field says about the text it takes.
	/// </summary>
	public const string TimeSpanNote = "a time span: [-][d.]hh:mm[:ss[.fffffff]] (01:30, 2.12:00:00), or a number of days";

	// the integer types and their ranges, so the schema agrees with the range check of the binder
	private static readonly Dictionary<string, (string Minimum, string Maximum)> IntegerRanges = new()
	{
		["byte"] = ("0", "255"),
		["sbyte"] = ("-128", "127"),
		["short"] = ("-32768", "32767"),
		["ushort"] = ("0", "65535"),
		["int"] = ("-2147483648", "2147483647"),
		["uint"] = ("0", "4294967295"),
		["long"] = ("-9223372036854775808", "9223372036854775807"),
		["ulong"] = ("0", "18446744073709551615"),
	};

	/// <summary>
	/// Writes the input schema of a tool: an object with a property for every field, the required fields, and no other properties.
	/// </summary>
	/// <param name="spec">The tool.</param>
	/// <returns>The JSON Schema.</returns>
	public static string InputSchema(ToolSpec spec)
	{
		if (spec is null)
		{
			throw new ArgumentNullException(nameof(spec));
		}
		var json = new StringBuilder();
		WriteInputSchema(json, spec);
		return json.ToString();
	}

	/// <summary>
	/// Writes the manifest of a tree: <c>{"tools": [...]}</c>, the result of the MCP <c>tools/list</c> request, with the name, description,
	/// input schema and annotations of every tool.
	/// </summary>
	/// <param name="tools">The tools, in the order of the Commands list.</param>
	/// <returns>The manifest.</returns>
	public static string Manifest(IEnumerable<ToolSpec> tools)
	{
		if (tools is null)
		{
			throw new ArgumentNullException(nameof(tools));
		}
		var json = new StringBuilder("{\"tools\":[");
		var first = true;
		foreach (var tool in tools)
		{
			if (!first)
			{
				json.Append(',');
			}
			first = false;
			json.Append("{\"name\":").Append(Quote(tool.Name));
			if (tool.Description.Length > 0)
			{
				json.Append(",\"description\":").Append(Quote(tool.Description));
			}
			json.Append(",\"inputSchema\":");
			WriteInputSchema(json, tool);
			json.Append(",\"annotations\":");
			WriteAnnotations(json, tool.Annotations);
			json.Append('}');
		}
		return json.Append("]}").ToString();
	}

	/// <summary>
	/// Writes a text as a JSON string, with the quotes around it.
	/// </summary>
	/// <param name="value">The text.</param>
	/// <returns>The JSON string.</returns>
	public static string Quote(string value)
	{
		var json = new StringBuilder("\"");
		foreach (var c in value ?? "")
		{
			switch (c)
			{
				case '"':
					json.Append("\\\"");
					break;
				case '\\':
					json.Append("\\\\");
					break;
				case '\n':
					json.Append("\\n");
					break;
				case '\r':
					json.Append("\\r");
					break;
				case '\t':
					json.Append("\\t");
					break;
				default:
					if (c < ' ')
					{
						json.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
					}
					else
					{
						json.Append(c);
					}
					break;
			}
		}
		return json.Append('"').ToString();
	}

	private static void WriteInputSchema(StringBuilder json, ToolSpec spec)
	{
		json.Append("{\"type\":\"object\",\"properties\":{");
		for (int i = 0; i < spec.Fields.Count; i++)
		{
			if (i > 0)
			{
				json.Append(',');
			}
			var field = spec.Fields[i];
			json.Append(Quote(field.Key)).Append(':');
			WriteField(json, field);
		}
		json.Append('}');
		var required = spec.Fields.Where(f => f.Required).Select(f => Quote(f.Key)).ToList();
		if (required.Count > 0)
		{
			json.Append(",\"required\":[").Append(string.Join(",", required)).Append(']');
		}
		json.Append(",\"additionalProperties\":false}");
	}

	// the MCP hints: a flag that is set states that its hint is true, one that is not set that it is false
	private static void WriteAnnotations(StringBuilder json, int annotations)
	{
		static string Hint(int annotations, int flag) => (annotations & flag) != 0 ? "true" : "false";
		json.Append("{\"readOnlyHint\":").Append(Hint(annotations, 1))
			.Append(",\"destructiveHint\":").Append(Hint(annotations, 2))
			.Append(",\"idempotentHint\":").Append(Hint(annotations, 4))
			.Append(",\"openWorldHint\":").Append(Hint(annotations, 8))
			.Append('}');
	}

	private static void WriteField(StringBuilder json, ToolField field)
	{
		var members = new List<string>();
		var description = Description(field);
		if (field.IsArray)
		{
			// a list: an array of values; null stands for the list that was not given, required asks for one value at least
			members.Add("\"type\":" + Types("array", field.Nullable));
			members.Add("\"items\":{" + string.Join(",", ValueMembers(field, nullable: false)) + "}");
			if (field.Required)
			{
				members.Add("\"minItems\":1");
			}
		}
		else
		{
			members.AddRange(ValueMembers(field, field.Nullable));
		}
		if (description.Length > 0)
		{
			members.Add("\"description\":" + Quote(description));
		}
		json.Append('{').Append(string.Join(",", members)).Append('}');
	}

	// the members of the schema of one value
	private static List<string> ValueMembers(ToolField field, bool nullable)
	{
		var members = new List<string>();
		if (field.AcceptsEnumNumber)
		{
			// a flag that holds an enum counts how often it is given: it takes the name of a member or its level as a number
			var options = new List<string>
			{
				"{\"type\":\"string\",\"enum\":[" + string.Join(",", field.EnumMembers.Select(Quote)) + "]}",
				"{\"type\":\"integer\",\"minimum\":0,\"maximum\":" + (field.EnumMembers.Count - 1).ToString(CultureInfo.InvariantCulture) + "}",
			};
			if (nullable)
			{
				options.Add("{\"type\":\"null\"}");
			}
			members.Add("\"oneOf\":[" + string.Join(",", options) + "]");
			return members;
		}
		members.Add("\"type\":" + Types(field.JsonType, nullable));
		if (field.EnumMembers.Count > 0)
		{
			// enum lists the values themselves, so null must be one of them too
			var values = field.EnumMembers.Select(Quote).ToList();
			if (nullable)
			{
				values.Add("null");
			}
			members.Add("\"enum\":[" + string.Join(",", values) + "]");
		}
		// a parse method decides the format of its text itself
		if (field.ParseMethodName.Length > 0)
		{
			return members;
		}
		if (field.JsonType == "integer" && IntegerRanges.TryGetValue(field.ValueType, out var range))
		{
			members.Add("\"minimum\":" + range.Minimum);
			members.Add("\"maximum\":" + range.Maximum);
		}
		switch (field.ValueType)
		{
			case "System.Guid":
				members.Add("\"format\":\"uuid\"");
				break;
			case "System.Uri":
				members.Add("\"format\":\"uri-reference\"");
				break;
			case "char":
				members.Add("\"minLength\":1");
				members.Add("\"maxLength\":1");
				break;
		}
		return members;
	}

	private static string Types(string type, bool nullable)
	{
		return nullable ? $"[{Quote(type)},\"null\"]" : Quote(type);
	}

	// the description of the argument, with what a date or time span must look like
	private static string Description(ToolField field)
	{
		var note = field.ParseMethodName.Length > 0
			? ""
			: field.ValueType switch
			{
				"System.DateTime" => DateTimeNote,
				"System.TimeSpan" => TimeSpanNote,
				_ => "",
			};
		if (note.Length == 0)
		{
			return field.Description;
		}
		return field.Description.Length == 0 ? note : $"{field.Description} ({note})";
	}
}
