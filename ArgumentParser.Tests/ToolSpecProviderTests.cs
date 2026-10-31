using ArgumentParser.Internal;
using ArgumentParser.Internal.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArgumentParser.Tests;

/// <summary>
/// Tests for the tool spec (ToolSpecProvider): the JSON view of a class or of the commands of a tree,
/// which the JSON Schema and the JSON binder are written from.
/// </summary>
public class ToolSpecProviderTests
{
	private const string Preamble = """
		using System;
		using System.Collections.Generic;
		using ArgumentParser;

		namespace Spec;

		public enum Level { Quiet, Normal, Loud }
		public class Point { public static bool Read(string input, out Point value) { value = new Point(); return true; } }

		""";

	private static ToolSpec ForClass(string members)
	{
		var compilation = TestHelper.CreateCompilation(Preamble + $$"""
			[ParameterCollection]
			public partial class Args
			{
			{{members}}
			}
			""");
		var tree = compilation.SyntaxTrees.Single();
		var declaration = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.Text == "Args");
		var factory = new AttributeFactory(compilation.GetSemanticModel(tree), declaration.Members.OfType<PropertyDeclarationSyntax>().ToList().AsReadOnly());
		var model = new ClassModel(
			"Args",
			"Spec",
			"program",
			null,
			new EquatableArray<PropertyAndAttributeInfo>(factory.GetOptionAttributes()),
			new EquatableArray<PropertyAndAttributeInfo>(factory.GetFlagAttributes()),
			new EquatableArray<PropertyAndAttributeInfo>(factory.GetPositionalAttributes()),
			default);
		return ToolSpecProvider.ForClass(model);
	}

	private static IReadOnlyList<ToolSpec> ForTree(string types)
	{
		var compilation = TestHelper.CreateCompilation(Preamble + types);
		var tree = compilation.SyntaxTrees.Single();
		var semanticModel = compilation.GetSemanticModel(tree);
		var sources = new List<CommandNodeSource>();
		foreach (var declaration in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
		{
			var symbol = (INamedTypeSymbol)semanticModel.GetDeclaredSymbol(declaration)!;
			if (symbol.GetAttributes().Any(a => a.AttributeClass?.Name is "CommandAppAttribute" or "CommandAttribute"))
			{
				sources.Add(CommandNodeExtractor.Extract(declaration, symbol, semanticModel).Source);
			}
		}
		var linked = CommandTreeLinker.Link(sources);
		Assert.Empty(linked.Problems.AsReadOnly());
		return ToolSpecProvider.ForTree(linked.Trees.AsReadOnly().Single());
	}

	private static ToolField Field(ToolSpec spec, string key) => spec.Fields.Single(f => f.Key == key);

	[Theory]
	[InlineData("Name", "name")]
	[InlineData("FilePath", "filePath")]
	[InlineData("name", "name")]
	[InlineData("X", "x")]
	[InlineData("Id", "id")]
	[InlineData("ID", "id")]
	[InlineData("URL", "url")]
	[InlineData("URLValue", "urlValue")]
	[InlineData("IPAddress", "ipAddress")]
	[InlineData("A1", "a1")]
	[InlineData("_count", "_count")]
	[InlineData("@class", "class")]
	[InlineData("@Event", "event")]
	public void Key_IsThePropertyNameInCamelCase_AsSystemTextJsonWritesIt(string propertyName, string expected)
	{
		Assert.Equal(expected, ToolSpecProvider.Key(propertyName));
	}

	[Fact]
	public void ToolName_JoinsThePathWithTwoUnderscores()
	{
		Assert.Equal("ingredient__add", ToolSpecProvider.ToolName(["ingredient", "add"]));
		Assert.Equal("log_entry__add", ToolSpecProvider.ToolName(["log_entry", "add"]));
		Assert.Equal("run", ToolSpecProvider.ToolName(["run"]));
	}

	[Fact]
	public void ForClass_TheQuickstartClassOfTheReadme_HasAFieldForEveryArgument()
	{
		var spec = ForClass("""
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

		Assert.Equal(("Args", "", CommandNodeInfo.DefaultAnnotations), (spec.Name, spec.Description, spec.Annotations));
		// positionals by position, then options, then flags
		Assert.Equal(
			[
				("repeatTimes", ArgumentKind.Positional, "integer", false, "the amount of times to repeat"),
				("output", ArgumentKind.Option, "string", true, ""),
				("timeStamp", ArgumentKind.Option, "string", false, "The timestamp to use"),
				("levelOption", ArgumentKind.Option, "string", false, "A level option with a long name"),
				("verbose", ArgumentKind.Flag, "boolean", false, "Enable verbose output"),
				("level", ArgumentKind.Flag, "string", false, "A level"),
			],
			spec.Fields.Select(f => (f.Key, f.Kind, f.JsonType, f.Required, f.Description)));
		Assert.All(spec.Fields, f => Assert.Equal([f.PropertyName], f.Target));
		Assert.Equal("Output", Field(spec, "output").PropertyName);
		Assert.Equal("System.DateTime", Field(spec, "timeStamp").ValueType);
	}

	[Fact]
	public void ForClass_AnEnumIsGivenByName_AndAnEnumFlagAlsoByNumber()
	{
		var spec = ForClass("""
			[Flag("l", "", "A level")] public Level Level { get; set; }
			[Option("", "mode", "A mode")] public Level? Mode { get; set; }
			""");

		Assert.Equal(["Quiet", "Normal", "Loud"], Field(spec, "level").EnumMembers);
		Assert.True(Field(spec, "level").AcceptsEnumNumber);
		Assert.Equal(["Quiet", "Normal", "Loud"], Field(spec, "mode").EnumMembers);
		Assert.False(Field(spec, "mode").AcceptsEnumNumber);
		Assert.True(Field(spec, "mode").Nullable);
		Assert.Equal("string", Field(spec, "mode").JsonType);
	}

	[Theory]
	[InlineData("int", "integer")]
	[InlineData("long", "integer")]
	[InlineData("short", "integer")]
	[InlineData("byte", "integer")]
	[InlineData("sbyte", "integer")]
	[InlineData("uint", "integer")]
	[InlineData("ulong", "integer")]
	[InlineData("ushort", "integer")]
	[InlineData("double", "number")]
	[InlineData("float", "number")]
	[InlineData("decimal", "number")]
	[InlineData("bool", "boolean")]
	[InlineData("string", "string")]
	[InlineData("char", "string")]
	[InlineData("Guid", "string")]
	[InlineData("Uri", "string")]
	[InlineData("TimeSpan", "string")]
	[InlineData("DateTime", "string")]
	public void ForClass_EverySupportedTypeHasAJsonType(string type, string jsonType)
	{
		var field = Assert.Single(ForClass($"[Option(\"v\", \"value\", \"A value\")] public {type} Value {{ get; set; }}").Fields);

		Assert.Equal(jsonType, field.JsonType);
		Assert.False(field.Nullable);
		Assert.False(field.IsArray);
	}

	[Fact]
	public void ForClass_NullablesListsAndParseMethods()
	{
		var spec = ForClass("""
			[Option("r", "retries", "Retries")] public int? Retries { get; set; }
			[Option("n", "name", "Name")] public string? Name { get; set; }
			[Option("i", "include", "Numbers")] public List<int>? Numbers { get; set; }
			[Option("p", "point", "A point"), ParsedWithMethod("Read")] public Point Point { get; set; } = new Point();
			[Option("q", "points", "Points"), ParsedWithMethod("Read")] public List<Point>? Points { get; set; }
			[Positional(0, "Files", true)] public List<string> Files { get; set; } = new();
			[Flag("f", "force", "Force"), ParsedWithMethod("Read")] public bool Force { get; set; }
			""");

		Assert.Equal(("integer", "int", true, false), (Field(spec, "retries").JsonType, Field(spec, "retries").ValueType, Field(spec, "retries").Nullable, Field(spec, "retries").IsArray));
		Assert.Equal(("string", true), (Field(spec, "name").JsonType, Field(spec, "name").Nullable));
		Assert.Equal(("integer", "int", true, true), (Field(spec, "numbers").JsonType, Field(spec, "numbers").ValueType, Field(spec, "numbers").Nullable, Field(spec, "numbers").IsArray));
		Assert.Equal(("string", "global::Spec.Point", "Read", false), (Field(spec, "point").JsonType, Field(spec, "point").ParseMethodOwner, Field(spec, "point").ParseMethodName, Field(spec, "point").IsArray));
		Assert.Equal(("string", "global::Spec.Point", true), (Field(spec, "points").JsonType, Field(spec, "points").ParseMethodOwner, Field(spec, "points").IsArray));
		Assert.Equal(("string", true, true, false), (Field(spec, "files").JsonType, Field(spec, "files").IsArray, Field(spec, "files").Required, Field(spec, "files").Nullable));
		// a flag reads no text, so a parse method on it does nothing, as on the command line
		Assert.Equal(("boolean", "", ""), (Field(spec, "force").JsonType, Field(spec, "force").ParseMethodOwner, Field(spec, "force").ParseMethodName));
	}

	[Fact]
	public void ForClass_AParseMethodOnAnEnumWins_SoTheNamesOfTheMembersAreNotTheOnlyValues()
	{
		var spec = ForClass("""
			[Option("m", "mode", "A mode"), ParsedWithMethod("ReadLevel")] public Level Mode { get; set; }
			[Option("s", "modes", "Modes"), ParsedWithMethod("ReadLevel")] public List<Level>? Modes { get; set; }
			[Flag("l", "level", "A level"), ParsedWithMethod("ReadLevel")] public Level Level { get; set; }
			private static bool ReadLevel(string input, out Level value) { value = Level.Loud; return true; }
			""");

		Assert.Equal(("string", "global::Spec.Args", "ReadLevel"), (Field(spec, "mode").JsonType, Field(spec, "mode").ParseMethodOwner, Field(spec, "mode").ParseMethodName));
		Assert.Empty(Field(spec, "mode").EnumMembers);
		Assert.Equal(("string", "ReadLevel", true), (Field(spec, "modes").JsonType, Field(spec, "modes").ParseMethodName, Field(spec, "modes").IsArray));
		Assert.Empty(Field(spec, "modes").EnumMembers);
		// a flag reads no text, so it keeps counting into the enum
		Assert.Equal(("", true), (Field(spec, "level").ParseMethodName, Field(spec, "level").AcceptsEnumNumber));
		Assert.Equal(["Quiet", "Normal", "Loud"], Field(spec, "level").EnumMembers);
	}

	[Fact]
	public void ForClass_AListOfEnumsAndANullableEnumFlag()
	{
		var spec = ForClass("""
			[Option("m", "modes", "Modes")] public List<Level>? Modes { get; set; }
			[Flag("l", "level", "A level")] public Level? Level { get; set; }
			""");

		Assert.Equal(("string", "Spec.Level", true, true), (Field(spec, "modes").JsonType, Field(spec, "modes").ValueType, Field(spec, "modes").IsArray, Field(spec, "modes").Nullable));
		Assert.Equal(["Quiet", "Normal", "Loud"], Field(spec, "modes").EnumMembers);
		Assert.False(Field(spec, "modes").AcceptsEnumNumber);
		Assert.Equal(("string", "Spec.Level", true, true), (Field(spec, "level").JsonType, Field(spec, "level").ValueType, Field(spec, "level").Nullable, Field(spec, "level").AcceptsEnumNumber));
		Assert.Equal(["Quiet", "Normal", "Loud"], Field(spec, "level").EnumMembers);
	}

	[Fact]
	public void ForClass_ARequiredNullableArgumentMustBeGivenButCanBeNull()
	{
		// as on the command line, where a required argument counts as given when it is there, even when a parse method turns it into null
		var spec = ForClass("""
			[Option("r", "retries", "Retries", required: true)] public int? Retries { get; set; }
			[Positional(0, "Name", true)] public string? Name { get; set; }
			""");

		Assert.Equal((true, true), (Field(spec, "retries").Required, Field(spec, "retries").Nullable));
		Assert.Equal((true, true), (Field(spec, "name").Required, Field(spec, "name").Nullable));
	}

	[Fact]
	public void ForClass_AnExcludedArgumentIsNoField()
	{
		var spec = ForClass("""
			[Option("d", "db", "Database"), ExcludeFromManifest] public string Db { get; set; } = "";
			[Flag("v", "verbose", "Verbose"), ExcludeFromManifest] public bool Verbose { get; set; }
			[Positional(0, "Name")] public string Name { get; set; } = "";
			""");

		Assert.Equal(["name"], spec.Fields.Select(f => f.Key));
	}

	private const string FileTool = """
		[CommandApp("myFileManagingTool", "Manage files and directories")]
		public partial class FileTool
		{
			[Option("r", "root", "Directory to work in")]
			public string Root { get; set; } = ".";

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
		[Command("list", "List the directories")]
		public partial class ListDirectories
		{
			[Flag("", "showhidden", "Include hidden directories")]
			public bool ShowHidden { get; set; }
		}

		[ParameterCollection(Annotations = CommandAnnotations.Idempotent)]
		[Command("create", "Create a directory")]
		public partial class CreateDirectory
		{
			[Positional(0, "Name of the new directory", true)]
			public string Name { get; set; } = "";
		}

		[ParameterCollection(Annotations = CommandAnnotations.Destructive | CommandAnnotations.Idempotent)]
		[Command("delete", "Delete a file")]
		public partial class DeleteFile
		{
			[Positional(0, "Path of the file", true)]
			public string FilePath { get; set; } = "";

			[Flag("f", "force", "Also delete read-only files")]
			public bool Force { get; set; }
		}
		""";

	[Fact]
	public void ForTree_HasAToolForEveryLeaf_InTheOrderOfCommands()
	{
		var tools = ForTree(FileTool);

		Assert.Equal(
			[
				("directories__list", "List the directories", (int)CommandAnnotations.ReadOnly),
				("directories__create", "Create a directory", (int)CommandAnnotations.Idempotent),
				("files__delete", "Delete a file", (int)(CommandAnnotations.Destructive | CommandAnnotations.Idempotent)),
			],
			tools.Select(t => (t.Name, t.Description, t.Annotations)));
	}

	[Fact]
	public void ForTree_ALeafHasTheOptionsOfTheLevelsAboveIt_WithThePathToTheirProperty()
	{
		var delete = ForTree(FileTool).Single(t => t.Name == "files__delete");

		Assert.Equal(
			[
				("root", ArgumentKind.Option, new[] { "Root" }),
				("dryRun", ArgumentKind.Flag, new[] { "Files", "DryRun" }),
				("filePath", ArgumentKind.Positional, new[] { "Files", "Delete", "FilePath" }),
				("force", ArgumentKind.Flag, new[] { "Files", "Delete", "Force" }),
			],
			delete.Fields.Select(f => (f.Key, f.Kind, f.Target.ToArray())));
		Assert.True(Field(delete, "filePath").Required);
		Assert.Equal("Directory to work in", Field(delete, "root").Description);

		var list = ForTree(FileTool).Single(t => t.Name == "directories__list");
		Assert.Equal(["root", "showHidden"], list.Fields.Select(f => f.Key));
		Assert.Equal(["Directories", "List", "ShowHidden"], Field(list, "showHidden").Target);
	}

	[Fact]
	public void ForTree_AnExcludedOptionOfTheRootIsInNoTool()
	{
		var tools = ForTree(FileTool.Replace("[Option(\"r\", \"root\", \"Directory to work in\")]", "[Option(\"r\", \"root\", \"Directory to work in\"), ExcludeFromManifest]", StringComparison.Ordinal));

		Assert.All(tools, tool => Assert.DoesNotContain(tool.Fields, f => f.Key == "root"));
		Assert.Equal(["dryRun", "filePath", "force"], tools.Single(t => t.Name == "files__delete").Fields.Select(f => f.Key));
	}

	[Fact]
	public void ForTree_ALeafDirectlyBelowTheRoot()
	{
		var tool = Assert.Single(ForTree("""
			[CommandApp("app")]
			public partial class App
			{
				[Flag("v", "verbose", "Verbose")] public bool Verbose { get; set; }
				public Run? Run { get; private set; }
			}

			[ParameterCollection]
			[Command("run", "Run it")]
			public partial class Run
			{
				[Option("t", "times", "How often")] public int Times { get; set; }
			}
			"""));

		Assert.Equal("run", tool.Name);
		Assert.Equal(CommandNodeInfo.DefaultAnnotations, tool.Annotations);
		Assert.Equal([("verbose", new[] { "Verbose" }), ("times", new[] { "Run", "Times" })], tool.Fields.Select(f => (f.Key, f.Target.ToArray())));
	}

	[Fact]
	public void ForTree_ClassesNestedInOtherTypes_TheTargetIsThePathOfChildProperties()
	{
		// the target follows the child properties, not the types the classes are declared in
		var tool = Assert.Single(ForTree("""
			public static partial class Outer
			{
				[CommandApp("app")]
				public partial class App
				{
					[Option("r", "root", "Root")] public string Root { get; set; } = "";
					public Group? Things { get; private set; }
				}

				[Command("things", "Things")]
				public partial class Group
				{
					public Inner.Add? Adding { get; private set; }
				}

				public partial class Inner
				{
					[ParameterCollection]
					[Command("add", "Add one")]
					public partial class Add
					{
						[Positional(0, "Name", true)] public string Name { get; set; } = "";
					}
				}
			}
			"""));

		Assert.Equal("things__add", tool.Name);
		Assert.Equal([("root", new[] { "Root" }), ("name", new[] { "Things", "Adding", "Name" })], tool.Fields.Select(f => (f.Key, f.Target.ToArray())));
	}
}
