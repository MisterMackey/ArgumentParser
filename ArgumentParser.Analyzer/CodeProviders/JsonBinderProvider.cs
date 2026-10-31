using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ArgumentParser.Internal.Utilities;

namespace ArgumentParser.Internal;

/// <summary>
/// Writes the JSON binder: the generated <c>Bind</c> methods, which set the properties of a class (or of the chain of classes of a tree)
/// from the arguments of a tool call, a JSON object. The code is written from the tool spec (<see cref="ToolField"/>), as the JSON Schema is,
/// so it accepts what the schema describes. It never throws or exits because of the arguments; it returns the errors, each with the key it is about.
/// </summary>
public static class JsonBinderProvider
{
	private const string JsonElement = "global::System.Text.Json.JsonElement";
	private const string ValueKind = "global::System.Text.Json.JsonValueKind";
	private const string Arguments = "IReadOnlyDictionary<string, " + JsonElement + ">";
	// what both IDictionary (the arguments of a call in the C# MCP SDK) and IReadOnlyDictionary are
	private const string Pairs = "IEnumerable<KeyValuePair<string, " + JsonElement + ">>";
	private const string ErrorList = "List<global::ArgumentParser.ArgumentParserException>";

	/// <summary>
	/// The name of the internal method every class of a tree gets: it creates the class, binds its own fields and the child on the path of the tool.
	/// </summary>
	public const string BindCommandName = "BindCommand";

	/// <summary>
	/// Writes the Bind methods of a class on its own: one that takes the arguments as a JSON object, and one that takes them as key and value pairs
	/// (any dictionary).
	/// </summary>
	/// <param name="writer">The writer.</param>
	/// <param name="className">The name of the class.</param>
	/// <param name="fields">The fields of the class.</param>
	public static void WriteClassBind(IndentedTextWriter writer, string className, IReadOnlyList<ToolField> fields)
	{
		if (writer is null)
		{
			throw new ArgumentNullException(nameof(writer));
		}
		if (fields is null)
		{
			throw new ArgumentNullException(nameof(fields));
		}
		var result = $"({className} result, {ErrorList} errors)";
		WriteObjectOverload(writer, result, "Bind(" + JsonElement + " arguments)", "Bind(values)");
		writer.WriteLine();

		writer.WriteLine($"public static {result} Bind({Pairs} arguments)");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine($"var errors = new {ErrorList}();");
		WriteCollect(writer);
		WriteUnknownKeys(writer, fields, "values");
		writer.WriteLine($"var instance = new {className}();");
		WriteFields(writer, fields, "values");
		writer.WriteLine("return (instance, errors);");
		writer.Indent--;
		writer.WriteLine("}");
	}

	/// <summary>
	/// Writes the Bind methods of the root of a tree, which take the name of a tool and its arguments, find the command of the tool and
	/// bind the chain from the root to it with <see cref="BindCommandName"/>.
	/// </summary>
	/// <param name="writer">The writer.</param>
	/// <param name="className">The name of the class of the root.</param>
	/// <param name="tools">The tools of the tree, in the order of the Commands list.</param>
	/// <param name="paths">The command path of every tool, in the same order.</param>
	public static void WriteTreeBind(IndentedTextWriter writer, string className, IReadOnlyList<ToolSpec> tools, IReadOnlyList<IReadOnlyList<string>> paths)
	{
		if (writer is null)
		{
			throw new ArgumentNullException(nameof(writer));
		}
		if (tools is null)
		{
			throw new ArgumentNullException(nameof(tools));
		}
		if (paths is null)
		{
			throw new ArgumentNullException(nameof(paths));
		}
		var result = $"({className} result, {ErrorList} errors)";
		WriteObjectOverload(writer, result, "Bind(string toolName, " + JsonElement + " arguments)", "Bind(toolName, values)");
		writer.WriteLine();

		writer.WriteLine($"public static {result} Bind(string toolName, {Pairs} arguments)");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine($"var errors = new {ErrorList}();");
		WriteCollect(writer);
		writer.WriteLine("switch (toolName)");
		writer.WriteLine("{");
		writer.Indent++;
		for (int i = 0; i < tools.Count; i++)
		{
			writer.WriteLine($"case {SourceTextGenerator.Literal(tools[i].Name)}:");
			writer.WriteLine("{");
			writer.Indent++;
			WriteUnknownKeys(writer, tools[i].Fields, "values");
			var path = string.Join(", ", paths[i].Select(SourceTextGenerator.Literal));
			writer.WriteLine($"var instance = {BindCommandName}(new string[] {{ {path} }}, 0, values, errors);");
			writer.WriteLine("return (instance, errors);");
			writer.Indent--;
			writer.WriteLine("}");
		}
		writer.WriteLine("default:");
		writer.Indent++;
		var expected = SourceTextGenerator.Literal("'. Expected one of: " + string.Join(", ", tools.Select(t => t.Name)) + ".");
		writer.WriteLine($"errors.Add(new global::ArgumentParser.UnknownCommandException(\"Unknown tool '\" + toolName + {expected}));");
		writer.WriteLine($"return (new {className}(), errors);");
		writer.Indent--;
		writer.Indent--;
		writer.WriteLine("}");
		writer.Indent--;
		writer.WriteLine("}");
	}

	/// <summary>
	/// Writes the <see cref="BindCommandName"/> method of a class of a tree: it creates the class, binds the fields of its own arguments and,
	/// for the root or a group, binds the child that the path names at <c>depth</c> and sets it. Every class binds itself, so private setters and
	/// parse methods work as they do for ParseCommand.
	/// </summary>
	/// <param name="writer">The writer.</param>
	/// <param name="node">The node.</param>
	/// <param name="fields">The fields of the node (<see cref="ToolSpecProvider.FieldsOf"/>).</param>
	public static void WriteBindCommand(IndentedTextWriter writer, TreeNode node, IReadOnlyList<ToolField> fields)
	{
		if (writer is null)
		{
			throw new ArgumentNullException(nameof(writer));
		}
		if (node is null)
		{
			throw new ArgumentNullException(nameof(node));
		}
		if (fields is null)
		{
			throw new ArgumentNullException(nameof(fields));
		}
		var className = node.Source.Node.ClassName;
		writer.WriteLine($"internal static {className} {BindCommandName}(string[] path, int depth, {Arguments} arguments, {ErrorList} errors)");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine($"var instance = new {className}();");
		WriteFields(writer, fields, "arguments");
		if (node.Children.Count > 0)
		{
			writer.WriteLine("switch (path[depth])");
			writer.WriteLine("{");
			writer.Indent++;
			foreach (var child in node.Children.AsReadOnly())
			{
				writer.WriteLine($"case {SourceTextGenerator.Literal(child.CommandName)}:");
				writer.Indent++;
				writer.WriteLine($"instance.{child.PropertyName} = global::{child.TypeName}.{BindCommandName}(path, depth + 1, arguments, errors);");
				writer.WriteLine("break;");
				writer.Indent--;
			}
			writer.Indent--;
			writer.WriteLine("}");
		}
		writer.WriteLine("return instance;");
		writer.Indent--;
		writer.WriteLine("}");
	}

	/// <summary>
	/// Describes the value a field takes, for the message of an error: <c>an integer from 0 to 255</c>, <c>one of: Quiet, Loud</c>.
	/// </summary>
	/// <param name="field">The field.</param>
	/// <returns>The description.</returns>
	public static string Expected(ToolField field)
	{
		if (field is null)
		{
			throw new ArgumentNullException(nameof(field));
		}
		var single = ExpectedValue(field);
		var expected = field.IsArray ? $"an array of which every value is {single}" : single;
		return field.Nullable ? expected + ", or null" : expected;
	}

	// the overload that takes a JSON object hands its properties to the other overload, which also sees a key that is given twice
	private static void WriteObjectOverload(IndentedTextWriter writer, string result, string signature, string call)
	{
		writer.WriteLine($"public static {result} {signature}");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine($"var errors = new {ErrorList}();");
		writer.WriteLine($"var values = new List<KeyValuePair<string, {JsonElement}>>();");
		writer.WriteLine($"if (arguments.ValueKind == {ValueKind}.Object)");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine("foreach (var property in arguments.EnumerateObject())");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine($"values.Add(new KeyValuePair<string, {JsonElement}>(property.Name, property.Value));");
		writer.Indent--;
		writer.WriteLine("}");
		writer.Indent--;
		writer.WriteLine("}");
		// no arguments at all is an empty object
		writer.WriteLine($"else if (arguments.ValueKind != {ValueKind}.Undefined && arguments.ValueKind != {ValueKind}.Null)");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine("errors.Add(new global::ArgumentParser.InvalidArgumentValueException(\"The arguments must be a JSON object, not \" + arguments.GetRawText() + \".\"));");
		writer.Indent--;
		writer.WriteLine("}");
		writer.WriteLine($"var bound = {call};");
		writer.WriteLine("errors.AddRange(bound.errors);");
		writer.WriteLine("return (bound.result, errors);");
		writer.Indent--;
		writer.WriteLine("}");
	}

	// the pairs become a dictionary (values); null is no arguments at all, and a key that is given twice is an error
	private static void WriteCollect(IndentedTextWriter writer)
	{
		writer.WriteLine($"var values = new Dictionary<string, {JsonElement}>(global::System.StringComparer.Ordinal);");
		writer.WriteLine("if (arguments != null)");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine("foreach (var pair in arguments)");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine("if (pair.Key == null)");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine("errors.Add(new global::ArgumentParser.UnexpectedArgumentException(\"A key of the arguments is null.\"));");
		writer.Indent--;
		writer.WriteLine("}");
		writer.WriteLine("else if (values.ContainsKey(pair.Key))");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine("errors.Add(new global::ArgumentParser.UnexpectedArgumentException(\"The key '\" + pair.Key + \"' is given more than once.\") { Key = pair.Key });");
		writer.Indent--;
		writer.WriteLine("}");
		writer.WriteLine("else");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine("values.Add(pair.Key, pair.Value);");
		writer.Indent--;
		writer.WriteLine("}");
		writer.Indent--;
		writer.WriteLine("}");
		writer.Indent--;
		writer.WriteLine("}");
	}

	// a key that is no field of the tool is an error, as the schema allows no other properties
	private static void WriteUnknownKeys(IndentedTextWriter writer, IEnumerable<ToolField> fields, string dictionary)
	{
		writer.WriteLine($"foreach (var key in {dictionary}.Keys)");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine("switch (key)");
		writer.WriteLine("{");
		writer.Indent++;
		var keys = fields.Select(f => f.Key).ToList();
		if (keys.Count > 0)
		{
			foreach (var key in keys)
			{
				writer.WriteLine($"case {SourceTextGenerator.Literal(key)}:");
			}
			writer.Indent++;
			writer.WriteLine("break;");
			writer.Indent--;
		}
		writer.WriteLine("default:");
		writer.Indent++;
		writer.WriteLine("errors.Add(new global::ArgumentParser.UnexpectedArgumentException(\"Unknown key '\" + key + \"'.\") { Key = key });");
		writer.WriteLine("break;");
		writer.Indent--;
		writer.Indent--;
		writer.WriteLine("}");
		writer.Indent--;
		writer.WriteLine("}");
	}

	private static void WriteFields(IndentedTextWriter writer, IEnumerable<ToolField> fields, string dictionary)
	{
		foreach (var field in fields)
		{
			WriteField(writer, field, dictionary);
		}
	}

	// A key that is not given leaves the property alone, as an argument that is not on the command line does.
	private static void WriteField(IndentedTextWriter writer, ToolField field, string dictionary)
	{
		var key = SourceTextGenerator.Literal(field.Key);
		var property = $"instance.{field.PropertyName}";
		var invalid = $"errors.Add(new global::ArgumentParser.InvalidArgumentValueException({SourceTextGenerator.Literal($"Invalid value for '{field.Key}': ")} + value.GetRawText() + {SourceTextGenerator.Literal(". Expected " + Expected(field) + ".")}) {{ Key = {key} }});";

		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine($"if ({dictionary}.TryGetValue({key}, out var value))");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine($"if (value.ValueKind == {ValueKind}.Null)");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine(field.Nullable ? $"{property} = null;" : invalid);
		writer.Indent--;
		writer.WriteLine("}");
		if (field.IsArray)
		{
			WriteArray(writer, field, property, key, invalid);
		}
		else
		{
			foreach (var (condition, result) in Readers(field, "value"))
			{
				writer.WriteLine($"else if ({condition})");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"{property} = {result};");
				writer.Indent--;
				writer.WriteLine("}");
			}
			writer.WriteLine("else");
			writer.WriteLine("{");
			writer.Indent++;
			writer.WriteLine(invalid);
			writer.Indent--;
			writer.WriteLine("}");
		}
		writer.Indent--;
		writer.WriteLine("}");
		if (field.Required)
		{
			writer.WriteLine("else");
			writer.WriteLine("{");
			writer.Indent++;
			writer.WriteLine($"errors.Add(new global::ArgumentParser.MissingRequiredArgumentException({SourceTextGenerator.Literal($"Missing required key '{field.Key}'.")}) {{ Key = {key} }});");
			writer.Indent--;
			writer.WriteLine("}");
		}
		writer.Indent--;
		writer.WriteLine("}");
	}

	// The values of an array are read one by one; a value that cannot be read is an error and the others are kept, as on the command line.
	// An array replaces what the property held, also when it is empty; required asks for one value at least.
	private static void WriteArray(IndentedTextWriter writer, ToolField field, string property, string key, string invalid)
	{
		writer.WriteLine($"else if (value.ValueKind == {ValueKind}.Array)");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine($"var list = new List<{TypeName(field.ValueType)}>();");
		writer.WriteLine("var index = 0;");
		writer.WriteLine("foreach (var item in value.EnumerateArray())");
		writer.WriteLine("{");
		writer.Indent++;
		var first = true;
		foreach (var (condition, result) in Readers(field, "item"))
		{
			writer.WriteLine($"{(first ? "if" : "else if")} ({condition})");
			writer.WriteLine("{");
			writer.Indent++;
			writer.WriteLine($"list.Add({result});");
			writer.Indent--;
			writer.WriteLine("}");
			first = false;
		}
		writer.WriteLine("else");
		writer.WriteLine("{");
		writer.Indent++;
		var itemMessage = SourceTextGenerator.Literal(". Expected " + ExpectedValue(field) + ".");
		writer.WriteLine($"errors.Add(new global::ArgumentParser.InvalidArgumentValueException({SourceTextGenerator.Literal($"Invalid value for '{field.Key}' at index ")} + index + \": \" + item.GetRawText() + {itemMessage}) {{ Key = {key} }});");
		writer.Indent--;
		writer.WriteLine("}");
		writer.WriteLine("index++;");
		writer.Indent--;
		writer.WriteLine("}");
		if (field.Required)
		{
			writer.WriteLine("if (index == 0)");
			writer.WriteLine("{");
			writer.Indent++;
			writer.WriteLine($"errors.Add(new global::ArgumentParser.MissingRequiredArgumentException({SourceTextGenerator.Literal($"The key '{field.Key}' needs one value at least.")}) {{ Key = {key} }});");
			writer.Indent--;
			writer.WriteLine("}");
		}
		writer.WriteLine($"{property} = list;");
		writer.Indent--;
		writer.WriteLine("}");
		writer.WriteLine("else");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine(invalid);
		writer.Indent--;
		writer.WriteLine("}");
	}

	// The ways a JSON value can be read as the value of the field: a condition that reads it into a variable, and the value to assign.
	// A condition declares its variable with out var, so every alternative has a name of its own.
	private static List<(string Condition, string Result)> Readers(ToolField field, string json)
	{
		var isString = $"{json}.ValueKind == {ValueKind}.String";
		var isNumber = $"{json}.ValueKind == {ValueKind}.Number";
		var text = $"{json}.GetString()";
		var name = json + "Value";
		if (field.ParseMethodName.Length > 0)
		{
			// the method reads the text as it does on the command line
			return [($"{isString} && {field.ParseMethodOwner}.{field.ParseMethodName}({text}, out var {name})", name)];
		}
		if (field.IsEnum)
		{
			// only the names of the members, as the schema says; Enum.TryParse would also take numbers and lists of names
			var type = TypeName(field.ValueType);
			var names = field.EnumMembers.Count == 0
				? "false"
				: string.Join(" || ", field.EnumMembers.Select(member => $"{text} == {SourceTextGenerator.Literal(member)}"));
			var readers = new List<(string, string)> { ($"{isString} && ({names}) && global::System.Enum.TryParse<{type}>({text}, out var {name})", name) };
			if (field.AcceptsEnumNumber)
			{
				var level = json + "Level";
				var maximum = (field.EnumMembers.Count - 1).ToString(CultureInfo.InvariantCulture);
				readers.Add(($"{isNumber} && {json}.TryGetInt32(out var {level}) && {level} >= 0 && {level} <= {maximum}", $"({type}){level}"));
			}
			return readers;
		}
		return field.ValueType switch
		{
			"bool" => [($"{json}.ValueKind == {ValueKind}.True || {json}.ValueKind == {ValueKind}.False", $"{json}.GetBoolean()")],
			"byte" => Integer(json, isNumber, "byte", "Byte", name),
			"sbyte" => Integer(json, isNumber, "sbyte", "SByte", name),
			"short" => Integer(json, isNumber, "short", "Int16", name),
			"ushort" => Integer(json, isNumber, "ushort", "UInt16", name),
			"int" => Integer(json, isNumber, "int", "Int32", name),
			"uint" => Integer(json, isNumber, "uint", "UInt32", name),
			"long" => Integer(json, isNumber, "long", "Int64", name),
			"ulong" => Integer(json, isNumber, "ulong", "UInt64", name),
			"decimal" => [($"{isNumber} && {json}.TryGetDecimal(out var {name})", name)],
			// a number too large for the type would read as infinity
			"double" => [($"{isNumber} && {json}.TryGetDouble(out var {name}) && !double.IsInfinity({name})", name)],
			"float" => [($"{isNumber} && {json}.TryGetSingle(out var {name}) && !float.IsInfinity({name})", name)],
			"char" => [($"{isString} && {text}.Length == 1", $"{text}[0]")],
			// the uuid format of the schema is the D format: 32 hexadecimal digits in groups of 8, 4, 4, 4 and 12
			"System.Guid" => [($"{isString} && global::System.Guid.TryParseExact({text}, \"D\", out var {name})", name)],
			"System.Uri" => [($"{isString} && global::System.Uri.TryCreate({text}, global::System.UriKind.RelativeOrAbsolute, out var {name})", name)],
			// a tool call has no culture, so dates and time spans are always read the invariant way
			"System.DateTime" => [($"{isString} && global::ArgumentParser.ValueParser.TryParse({text}, false, out global::System.DateTime {name})", name)],
			"System.TimeSpan" => [($"{isString} && global::ArgumentParser.ValueParser.TryParse({text}, false, out global::System.TimeSpan {name})", name)],
			_ => [(isString, text)],
		};
	}

	// An integer is any JSON number without a fraction in the range of the type, as "type": "integer" of JSON Schema says: 1.0 and 1e3 are
	// integers too. TryGetInt32 and the like only read the plain form (1, 1000), so a number in another form is read as a decimal and checked.
	private static List<(string Condition, string Result)> Integer(string json, string isNumber, string keyword, string method, string name)
	{
		var (minimum, maximum) = JsonSchemaProvider.IntegerRanges[keyword];
		var whole = name + "Whole";
		return
		[
			($"{isNumber} && {json}.TryGet{method}(out var {name})", name),
			($"{isNumber} && {json}.TryGetDecimal(out var {whole}) && {whole} == global::System.Math.Truncate({whole}) && {whole} >= {minimum}m && {whole} <= {maximum}m", $"({keyword}){whole}"),
		];
	}

	private static string ExpectedValue(ToolField field)
	{
		if (field.ParseMethodName.Length > 0)
		{
			// the caller did send a string when the method rejects it, so say whose format it is
			return $"a string that {field.ParseMethodName} accepts";
		}
		if (field.IsEnum)
		{
			if (field.EnumMembers.Count == 0)
			{
				return "nothing (the enum has no members)";
			}
			var names = "one of: " + string.Join(", ", field.EnumMembers);
			return field.AcceptsEnumNumber
				? $"{names}, or a number from 0 to {(field.EnumMembers.Count - 1).ToString(CultureInfo.InvariantCulture)}"
				: names;
		}
		return field.ValueType switch
		{
			"bool" => "true or false",
			"byte" => "an integer from 0 to 255",
			"sbyte" => "an integer from -128 to 127",
			"short" => "an integer from -32768 to 32767",
			"ushort" => "an integer from 0 to 65535",
			"int" => "an integer from -2147483648 to 2147483647",
			"uint" => "an integer from 0 to 4294967295",
			"long" => "an integer from -9223372036854775808 to 9223372036854775807",
			"ulong" => "an integer from 0 to 18446744073709551615",
			"double" or "float" or "decimal" => "a number",
			"char" => "a string of one character",
			"System.Guid" => "a UUID (0f8fad5b-d9cb-469f-a165-70867728950e)",
			"System.Uri" => "a URI",
			"System.DateTime" => "a string in " + JsonSchemaProvider.DateTimeNote,
			"System.TimeSpan" => JsonSchemaProvider.TimeSpanNote,
			_ => "a string",
		};
	}

	private static readonly HashSet<string> Keywords =
	[
		"bool", "byte", "sbyte", "short", "ushort", "int", "uint", "long", "ulong", "float", "double", "decimal", "char", "string", "object",
	];

	// the type as it can be written in generated code: a keyword as it is, any other type from the global namespace
	private static string TypeName(string valueType)
	{
		var type = valueType.TrimEnd('?');
		return Keywords.Contains(type) ? type : "global::" + type;
	}
}
