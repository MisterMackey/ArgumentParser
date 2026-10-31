using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArgumentParser.Internal;

namespace ArgumentParser.Tests;

/// <summary>
/// Tests for the JSON Schema of a class (the JsonSchema constant) and the manifest of a tree (the McpManifest constant and CommandInfo.InputSchema):
/// the generated code is compiled and the JSON is read back.
/// </summary>
public class JsonSchemaTests
{
	private const string Manifest = "[ParameterCollection(HelpTextGeneration.GenerateTextOnly, \"h\", \"Help\", BehaviourOnError.ThrowNever, EmitMcpManifest = true)]";

	private static string ClassSource(string members, string attribute = Manifest) => $$"""
		using System;
		using System.Collections.Generic;
		using ArgumentParser;

		namespace ArgumentParser.Tests;

		public enum Level { Quiet, Normal, Loud }
		public class Point { public static bool TryParse(string input, out Point value) { value = new Point(); return true; } }

		{{attribute}}
		public partial class TestClass
		{
		{{members}}
		}
		""";

	private static JsonObject Schema(string members)
	{
		var parser = GeneratedParser.Compile(ClassSource(members));
		return JsonNode.Parse(parser.GetStaticMember<string>("JsonSchema")!)!.AsObject();
	}

	private static JsonObject Property(JsonObject schema, string key) => schema["properties"]![key]!.AsObject();

	// a JSON value written compactly, to compare a part of a schema with the text it should be (without escaping + and the like)
	private static readonly JsonSerializerOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

	private static string Json(JsonNode? node) => node!.ToJsonString(Compact);

	#region the schema of a class

	[Fact]
	public void JsonSchema_TheQuickstartClassOfTheReadme()
	{
		var schema = Schema("""
			[Flag(shortName: "v", longName: "Verbose", description: "Enable verbose output")]
			public bool Verbose { get; set; }
			[Option(longName: "Target", required: true)]
			public string Output { get; set; } = "";
			[Positional(0, "the amount of times to repeat")]
			public int RepeatTimes { get; set; }
			[Option(shortName: "t", longName: "TimeStamp", description: "The timestamp to use", required: false)]
			public DateTime TimeStamp { get; set; }
			[Flag("l", "", "A level")]
			public Level Level { get; set; }
			[Option(shortName: "", longName: "Level", description: "A level option with a long name")]
			public Level LevelOption { get; set; }
			""");

		Assert.Equal("object", (string?)schema["type"]);
		Assert.False((bool)schema["additionalProperties"]!);
		// positionals by position, then options, then flags
		Assert.Equal(["repeatTimes", "output", "timeStamp", "levelOption", "verbose", "level"], schema["properties"]!.AsObject().Select(p => p.Key));
		Assert.Equal("""["output"]""", Json(schema["required"]));
		Assert.Equal("""{"type":"integer","minimum":-2147483648,"maximum":2147483647,"description":"the amount of times to repeat"}""", Json(Property(schema, "repeatTimes")));
		Assert.Equal("""{"type":"string"}""", Json(Property(schema, "output")));
		Assert.Equal($$"""{"type":"string","description":"The timestamp to use ({{JsonSchemaProvider.DateTimeNote}})"}""", Json(Property(schema, "timeStamp")));
		Assert.Equal("""{"type":"string","enum":["Quiet","Normal","Loud"],"description":"A level option with a long name"}""", Json(Property(schema, "levelOption")));
		Assert.Equal("""{"type":"boolean","description":"Enable verbose output"}""", Json(Property(schema, "verbose")));
		// a flag that holds an enum takes the name of a member or the number of times it is given
		Assert.Equal("""{"oneOf":[{"type":"string","enum":["Quiet","Normal","Loud"]},{"type":"integer","minimum":0,"maximum":2}],"description":"A level"}""", Json(Property(schema, "level")));
	}

	[Fact]
	public void JsonSchema_TheExampleOfTheReadme()
	{
		var schema = Schema("""
			[Positional(0, "the amount of times to repeat", required: true)]
			public int RepeatTimes { get; set; }

			[Option("o", "Target", "Where to write the output")]
			public string? Output { get; set; }

			[Flag("v", "Verbose", "Enable verbose output")]
			public bool Verbose { get; set; }
			""");

		Assert.Equal(
			"""{"type":"object","properties":{"repeatTimes":{"type":"integer","minimum":-2147483648,"maximum":2147483647,"description":"the amount of times to repeat"},"output":{"type":["string","null"],"description":"Where to write the output"},"verbose":{"type":"boolean","description":"Enable verbose output"}},"required":["repeatTimes"],"additionalProperties":false}""",
			Json(schema));
	}

	[Theory]
	[InlineData("byte", """{"type":"integer","minimum":0,"maximum":255}""")]
	[InlineData("sbyte", """{"type":"integer","minimum":-128,"maximum":127}""")]
	[InlineData("short", """{"type":"integer","minimum":-32768,"maximum":32767}""")]
	[InlineData("ushort", """{"type":"integer","minimum":0,"maximum":65535}""")]
	[InlineData("uint", """{"type":"integer","minimum":0,"maximum":4294967295}""")]
	[InlineData("long", """{"type":"integer","minimum":-9223372036854775808,"maximum":9223372036854775807}""")]
	[InlineData("ulong", """{"type":"integer","minimum":0,"maximum":18446744073709551615}""")]
	[InlineData("double", """{"type":"number"}""")]
	[InlineData("float", """{"type":"number"}""")]
	[InlineData("decimal", """{"type":"number"}""")]
	[InlineData("string", """{"type":"string"}""")]
	[InlineData("char", """{"type":"string","minLength":1,"maxLength":1}""")]
	[InlineData("Guid", """{"type":"string","format":"uuid"}""")]
	[InlineData("Uri", """{"type":"string","format":"uri-reference"}""")]
	[InlineData("TimeSpan", """{"type":"string","description":"a time span: [-][d.]hh:mm[:ss[.fffffff]] (01:30, 2.12:00:00), or a number of days"}""")]
	public void JsonSchema_EverySupportedType(string type, string expected)
	{
		var schema = Schema($"[Option(\"v\", \"value\", \"\")] public {type} Value {{ get; set; }}");

		Assert.Equal(expected, Json(Property(schema, "value")));
	}

	[Fact]
	public void JsonSchema_NullableArgumentsAcceptNull()
	{
		var schema = Schema("""
			[Option("r", "retries", "")] public int? Retries { get; set; }
			[Option("m", "mode", "")] public Level? Mode { get; set; }
			[Flag("l", "level", "")] public Level? Level { get; set; }
			[Flag("f", "force", "")] public bool? Force { get; set; }
			[Option("u", "uri", "")] public Uri? Address { get; set; }
			""");

		Assert.Equal("""{"type":["integer","null"],"minimum":-2147483648,"maximum":2147483647}""", Json(Property(schema, "retries")));
		// enum lists the values themselves, so null is one of them
		Assert.Equal("""{"type":["string","null"],"enum":["Quiet","Normal","Loud",null]}""", Json(Property(schema, "mode")));
		Assert.Equal("""{"oneOf":[{"type":"string","enum":["Quiet","Normal","Loud"]},{"type":"integer","minimum":0,"maximum":2},{"type":"null"}]}""", Json(Property(schema, "level")));
		Assert.Equal("""{"type":["boolean","null"]}""", Json(Property(schema, "force")));
		Assert.Equal("""{"type":["string","null"],"format":"uri-reference"}""", Json(Property(schema, "address")));
	}

	[Fact]
	public void JsonSchema_ARequiredNullableArgumentIsRequiredAndAcceptsNull()
	{
		var schema = Schema("""[Option("r", "retries", "", required: true)] public int? Retries { get; set; }""");

		Assert.Equal("""["retries"]""", Json(schema["required"]));
		Assert.Equal("""["integer","null"]""", Json(Property(schema, "retries")["type"]));
	}

	[Fact]
	public void JsonSchema_ListsAreArrays()
	{
		var schema = Schema("""
			[Option("n", "number", "Numbers")] public List<int>? Numbers { get; set; }
			[Option("m", "mode", "")] public List<Level> Modes { get; set; } = new();
			[Positional(0, "Files", true)] public List<string> Files { get; set; } = new();
			""");

		Assert.Equal("""{"type":["array","null"],"items":{"type":"integer","minimum":-2147483648,"maximum":2147483647},"description":"Numbers"}""", Json(Property(schema, "numbers")));
		Assert.Equal("""{"type":"array","items":{"type":"string","enum":["Quiet","Normal","Loud"]}}""", Json(Property(schema, "modes")));
		// required on a list asks for one value at least, as on the command line
		Assert.Equal("""{"type":"array","items":{"type":"string"},"minItems":1,"description":"Files"}""", Json(Property(schema, "files")));
		Assert.Equal("""["files"]""", Json(schema["required"]));
	}

	[Fact]
	public void JsonSchema_AParseMethodTakesAStringInItsOwnFormat()
	{
		var schema = Schema("""
			[Option("p", "point", "A point"), ParsedWithMethod("TryParse")] public Point Point { get; set; } = new();
			[Option("d", "date", "A date"), ParsedWithMethod("ReadDate")] public DateTime Date { get; set; }
			[Option("l", "level", ""), ParsedWithMethod("ReadLevel")] public Level Level { get; set; }
			[Option("n", "number", ""), ParsedWithMethod("ReadNumber")] public int Number { get; set; }
			private static bool ReadDate(string input, out DateTime value) { value = DateTime.Now; return true; }
			private static bool ReadLevel(string input, out Level value) { value = Level.Loud; return true; }
			private static bool ReadNumber(string input, out int value) { value = 1; return true; }
			""");

		Assert.Equal("""{"type":"string","description":"A point"}""", Json(Property(schema, "point")));
		Assert.Equal("""{"type":"string","description":"A date"}""", Json(Property(schema, "date")));
		Assert.Equal("""{"type":"string"}""", Json(Property(schema, "level")));
		Assert.Equal("""{"type":"string"}""", Json(Property(schema, "number")));
	}

	[Fact]
	public void JsonSchema_TextIsEscaped()
	{
		var schema = Schema("""
			[Option("p", "path", "A \"path\" \\ with\na line break\tand a tab")] public string Path { get; set; } = "";
			""");

		Assert.Equal("A \"path\" \\ with\na line break\tand a tab", (string?)Property(schema, "path")["description"]);
	}

	[Fact]
	public void JsonSchema_AnExcludedArgumentIsNotInIt()
	{
		var schema = Schema("""
			[Option("d", "db", ""), ExcludeFromManifest] public string Db { get; set; } = "";
			[Positional(0, "Name")] public string Name { get; set; } = "";
			""");

		Assert.Equal(["name"], schema["properties"]!.AsObject().Select(p => p.Key));
	}

	[Fact]
	public void JsonSchema_AClassWithoutArguments_AcceptsOnlyAnEmptyObject()
	{
		var schema = Schema("");

		Assert.Equal("""{"type":"object","properties":{},"additionalProperties":false}""", Json(schema));
	}

	[Theory]
	[InlineData("[ParameterCollection(HelpTextGeneration.GenerateTextOnly, \"h\", \"Help\", BehaviourOnError.ThrowNever)]")]
	[InlineData("[ParameterCollection(HelpTextGeneration.GenerateTextOnly, \"h\", \"Help\", BehaviourOnError.ThrowNever, EmitMcpManifest = false)]")]
	// the binder (phase 3) does not need the constant
	[InlineData("[ParameterCollection(HelpTextGeneration.GenerateTextOnly, \"h\", \"Help\", BehaviourOnError.ThrowNever, EmitJsonBinder = true)]")]
	public void JsonSchema_IsOnlyEmittedWithEmitMcpManifest(string attribute)
	{
		var parser = GeneratedParser.Compile(ClassSource("""[Option("n", "name", "")] public string Name { get; set; } = "";""", attribute));

		Assert.Null(parser.GetStaticMember<string>("JsonSchema"));
	}

	[Fact]
	public void JsonSchema_TheGeneratedCodeHasNoWarnings()
	{
		var diagnostics = GeneratedParser.GeneratedCodeDiagnostics(ClassSource("""
			[Option("r", "retries", "A \"number\"")] public int? Retries { get; set; }
			[Positional(0, "Files", true)] public List<string> Files { get; set; } = new();
			"""));

		Assert.Empty(diagnostics);
	}
	#endregion

	#region the manifest of a tree

	private const string FileTool = """
		using ArgumentParser;

		namespace ArgumentParser.Tests;

		[CommandApp("myFileManagingTool", "Manage files and directories", HelpTextGeneration.GenerateTextOnly, behaviourOnError: BehaviourOnError.ThrowNever, EmitMcpManifest = true)]
		public partial class FileTool
		{
			[Option("r", "root", "Directory to work in")]
			public string Root { get; set; } = ".";
			[Option("t", "token", "Set by the host"), ExcludeFromManifest]
			public string Token { get; set; } = "";

			public DirectoriesCommand? Directories { get; private set; }
			public FilesCommand? Files { get; private set; }
		}

		[Command("directories", "Work with directories")]
		public partial class DirectoriesCommand
		{
			public ListDirectories? List { get; private set; }
			public CreateDirectory? Create { get; private set; }
		}

		[Command("files", "Work with files")]
		public partial class FilesCommand
		{
			[Flag("n", "dryrun", "Only show what would be done")]
			public bool DryRun { get; set; }

			public DeleteFile? Delete { get; private set; }
		}

		[ParameterCollection(Annotations = CommandAnnotations.ReadOnly)]
		[Command("list", "")]
		public partial class ListDirectories
		{
		}

		[ParameterCollection(Annotations = CommandAnnotations.Idempotent)]
		[Command("create", "Create a directory")]
		public partial class CreateDirectory
		{
			[Positional(0, "Name of the new directory", true)]
			public string Name { get; set; } = "";
		}

		[ParameterCollection]
		[Command("delete", "Delete a file")]
		public partial class DeleteFile
		{
			[Positional(0, "Path of the file", true)]
			public string FilePath { get; set; } = "";
		}
		""";

	[Fact]
	public void McpManifest_HasAToolForEveryCommand_InTheOrderOfCommands()
	{
		var parser = GeneratedParser.Compile(FileTool, "FileTool");

		var tools = JsonNode.Parse(parser.GetStaticMember<string>("McpManifest")!)!["tools"]!.AsArray();

		Assert.Equal(["directories__list", "directories__create", "files__delete"], tools.Select(t => (string)t!["name"]!));
		// a command without a description has none in its tool
		Assert.Null(tools[0]!["description"]);
		Assert.Equal("Create a directory", (string?)tools[1]!["description"]);
		Assert.Equal("""{"readOnlyHint":true,"destructiveHint":false,"idempotentHint":false,"openWorldHint":false}""", Json(tools[0]!["annotations"]));
		Assert.Equal("""{"readOnlyHint":false,"destructiveHint":false,"idempotentHint":true,"openWorldHint":false}""", Json(tools[1]!["annotations"]));
		// the default annotations are those of MCP: destructive and open world
		Assert.Equal("""{"readOnlyHint":false,"destructiveHint":true,"idempotentHint":false,"openWorldHint":true}""", Json(tools[2]!["annotations"]));
	}

	[Fact]
	public void McpManifest_TheSchemaOfACommandHoldsTheOptionsOfTheCommandsAboveIt()
	{
		var parser = GeneratedParser.Compile(FileTool, "FileTool");

		var tools = JsonNode.Parse(parser.GetStaticMember<string>("McpManifest")!)!["tools"]!.AsArray();

		Assert.Equal(
			"""{"type":"object","properties":{"root":{"type":"string","description":"Directory to work in"}},"additionalProperties":false}""",
			Json(tools[0]!["inputSchema"]));
		Assert.Equal(
			"""{"type":"object","properties":{"root":{"type":"string","description":"Directory to work in"},"dryRun":{"type":"boolean","description":"Only show what would be done"},"filePath":{"type":"string","description":"Path of the file"}},"required":["filePath"],"additionalProperties":false}""",
			Json(tools[2]!["inputSchema"]));
	}

	[Fact]
	public void CommandInfo_InputSchema_IsTheSchemaOfItsToolInTheManifest()
	{
		var parser = GeneratedParser.Compile(FileTool, "FileTool");

		var tools = JsonNode.Parse(parser.GetStaticMember<string>("McpManifest")!)!["tools"]!.AsArray();
		var commands = parser.GetStaticProperty<IReadOnlyList<CommandInfo>>("Commands")!;

		Assert.Equal(tools.Count, commands.Count);
		for (int i = 0; i < commands.Count; i++)
		{
			Assert.Equal(Json(tools[i]!["inputSchema"]), commands[i].InputSchema);
			Assert.Equal((string)tools[i]!["name"]!, string.Join("__", commands[i].Path));
		}
	}

	[Fact]
	public void McpManifest_IsOnlyEmittedWithEmitMcpManifestOnTheCommandApp()
	{
		var parser = GeneratedParser.Compile(FileTool.Replace(", EmitMcpManifest = true", "", StringComparison.Ordinal), "FileTool");

		Assert.Null(parser.GetStaticMember<string>("McpManifest"));
		Assert.All(parser.GetStaticProperty<IReadOnlyList<CommandInfo>>("Commands")!, command => Assert.Null(command.InputSchema));
	}

	[Fact]
	public void McpManifest_TheGeneratedCodeOfTheTreeHasNoWarnings()
	{
		Assert.Empty(GeneratedParser.GeneratedCodeDiagnostics(FileTool));
	}

	[Fact]
	public void JsonSchema_ALeafWithItsOwnSetting_GetsTheSchemaOfItsOwnArgumentsOnly()
	{
		// the setting of a leaf only applies to the class on its own (and ARG029 warns that the tree ignores it)
		var parser = GeneratedParser.Compile(
			FileTool
				.Replace(", EmitMcpManifest = true", "", StringComparison.Ordinal)
				.Replace("[ParameterCollection]\n[Command(\"delete\"", "[ParameterCollection(EmitMcpManifest = true)]\n[Command(\"delete\"", StringComparison.Ordinal),
			"DeleteFile");

		var schema = JsonNode.Parse(parser.GetStaticMember<string>("JsonSchema")!)!.AsObject();

		Assert.Equal(["filePath"], schema["properties"]!.AsObject().Select(p => p.Key));
	}
	#endregion
}
