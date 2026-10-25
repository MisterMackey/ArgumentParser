using System.Reflection;
using ArgumentParser.Internal;
using Microsoft.CodeAnalysis;

namespace ArgumentParser.Tests;

/// <summary>
/// Exercises every diagnostic rule from both sides: the situations that must trigger it
/// and the neighbouring situations that must not, plus how the rules interact in the generator pipeline.
/// </summary>
public class DiagnosticRuleTests
{
	private const string SafeAttribute = "[ParameterCollection(HelpTextGeneration.GenerateTextOnly, \"h\", \"Help\", BehaviourOnError.ThrowNever)]";

	private sealed record Result(IReadOnlyList<Diagnostic> Diagnostics, string Output, string Source)
	{
		// Locations are rebuilt from plain data, so they have no syntax tree: read the flagged text from the source.
		public string TextAt(Diagnostic diagnostic)
		{
			var span = diagnostic.Location.GetLineSpan().Span;
			var lines = Source.Split('\n');
			var first = span.Start.Line;
			var last = span.End.Line;
			var text = string.Join("\n", lines[first..(last + 1)]);
			var startOffset = span.Start.Character;
			var endOffset = text.Length - (lines[last].Length - span.End.Character);
			return text[startOffset..endOffset];
		}

		public IReadOnlyList<string> Ids => Diagnostics.Select(d => d.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();

		public IReadOnlyList<Diagnostic> Rule(string id) => Diagnostics.Where(d => d.Id == id).ToList();
	}

	private static Result Diagnose(
		string members,
		string attribute = SafeAttribute,
		string modifiers = "public partial",
		string classNameSuffix = "",
		bool includeTreeRules = false,
		string? otherPart = null)
	{
		var source = $$"""
			using System;
			using System.IO;
			using ArgumentParser;

			namespace Diagnostics;

			public enum Colour { Red, Green }
			public class Custom { }
			public struct CustomStruct { }

			{{attribute}}
			{{modifiers}} class TestClass{{classNameSuffix}}
			{
			{{members}}
			}
			""";
		// another part of the same class, in a file of its own (Other1.cs)
		var otherFiles = otherPart is null ? [] : new[]
		{
			$$"""
			using ArgumentParser;

			namespace Diagnostics;

			public partial class TestClass{{classNameSuffix}}
			{
			{{otherPart}}
			}
			"""
		};
		var (diagnostics, output) = TestHelper.GetGeneratedOutput(source, otherFiles);
		// A class on its own is not a tree: it would be a group without commands or a command without a root.
		// The rules about the tree are tested with whole trees, so they are left out unless asked for.
		var treeRules = new[] { "ARG017", "ARG018", "ARG019", "ARG020", "ARG021", "ARG022" };
		return new Result(
			diagnostics.Where(d => d.Id.StartsWith("ARG", StringComparison.Ordinal) && (includeTreeRules || !treeRules.Contains(d.Id))).ToList(),
			output,
			source);
	}

	private static string Option(string shortName, string longName, string name, string type = "string", bool required = false)
		=> $"[Option(\"{shortName}\", \"{longName}\", \"desc\", {(required ? "true" : "false")})] public {type} {name} {{ get; set; }}";

	private static string Flag(string shortName, string longName, string name, string type = "bool")
		=> $"[Flag(\"{shortName}\", \"{longName}\", \"desc\")] public {type} {name} {{ get; set; }}";

	private static string Positional(int position, string name, string type = "string")
		=> $"[Positional({position}, \"desc\")] public {type} {name} {{ get; set; }}";

	private static string Join(params string[] members) => string.Join(Environment.NewLine, members);

	private static string[] Repeat(string id, int count) => Enumerable.Repeat(id, count).ToArray();

	public static TheoryData<string> ValidMembers => new()
	{
		"",
		Option("o", "Output", "Output"),
		Join(Option("o", "Output", "Output", required: true), Flag("v", "Verbose", "Verbose"), Positional(0, "Input")),
		Join(Option("", "Output", "Output"), Option("o", "", "Other")),
		Join(Positional(1, "Second"), Positional(0, "First")),
	};

	[Theory]
	[MemberData(nameof(ValidMembers))]
	public void ValidClass_ReportsNothingAndGeneratesSource(string members)
	{
		var result = Diagnose(members);

		Assert.Empty(result.Ids);
		Assert.NotEmpty(result.Output);
	}

	#region ARG001
	[Theory]
	[InlineData("public")]
	[InlineData("internal")]
	[InlineData("public sealed")]
	[InlineData("public abstract")]
	public void ARG001_ClassWithoutPartial_IsReportedAndNothingIsGenerated(string modifiers)
	{
		var result = Diagnose(Option("o", "Output", "Output"), modifiers: modifiers);

		var diagnostic = Assert.Single(result.Diagnostics);
		Assert.Equal("ARG001", diagnostic.Id);
		Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
		Assert.Contains("'TestClass'", diagnostic.GetMessage());
		Assert.Empty(result.Output);
	}

	[Theory]
	[InlineData("public partial")]
	[InlineData("internal partial")]
	[InlineData("public sealed partial")]
	[InlineData("partial")]
	public void ARG001_PartialClass_IsNotReported(string modifiers)
	{
		var result = Diagnose(Option("o", "Output", "Output"), modifiers: modifiers);

		Assert.Empty(result.Rule("ARG001"));
	}
	#endregion

	#region ARG002
	[Theory]
	[InlineData("public static int Parse(string[] args) => 0;")]
	[InlineData("public static int Parse(string[] arguments) => 0;")]
	[InlineData("private static int Parse(string[] args) => 0;")]
	[InlineData("public int Parse(string[] args) => 0;")]
	public void ARG002_ExistingParseMethod_IsReportedAndNothingIsGenerated(string parseMethod)
	{
		var result = Diagnose(Join(Option("o", "Output", "Output"), parseMethod));

		var diagnostic = Assert.Single(result.Diagnostics);
		Assert.Equal("ARG002", diagnostic.Id);
		Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
		Assert.Empty(result.Output);
	}

	[Theory]
	[InlineData("public static int Parse(string input) => 0;")]
	[InlineData("public static int Parse() => 0;")]
	[InlineData("public static int Parse(string[] args, int count) => 0;")]
	[InlineData("public static int Parse(int[] args) => 0;")]
	[InlineData("public static int Parse(IEnumerable<string> args) => 0;")]
	[InlineData("public static int Parse(string[][] args) => 0;")]
	[InlineData("public static int Other(string[] args) => 0;")]
	public void ARG002_DifferentSignature_IsNotReported(string method)
	{
		var result = Diagnose(Join(Option("o", "Output", "Output"), method));

		Assert.Empty(result.Rule("ARG002"));
	}

	[Theory]
	[InlineData("public static int Parse(String[] args) => 0;")]
	[InlineData("public static int Parse(System.String[] args) => 0;")]
	public void ARG002_ExistingParseMethodSpelledWithTheFrameworkTypeName_IsReported(string parseMethod)
	{
		var result = Diagnose(Join(Option("o", "Output", "Output"), parseMethod));

		Assert.Single(result.Rule("ARG002"));
	}
	#endregion

	#region ARG003
	public static TheoryData<string, string[]> DuplicateNames => new()
	{
		{ Join(Option("s", "One", "A"), Option("s", "Two", "B")), Repeat("ARG003", 2) },
		{ Join(Option("s", "One", "A"), Flag("s", "Two", "B")), Repeat("ARG003", 2) },
		{ Join(Flag("s", "One", "A"), Flag("s", "Two", "B")), Repeat("ARG003", 2) },
		{ Join(Option("a", "Same", "A"), Option("b", "Same", "B")), Repeat("ARG003", 2) },
		{ Join(Option("a", "Same", "A"), Flag("b", "Same", "B")), Repeat("ARG003", 2) },
		{ Join(Flag("a", "Same", "A"), Flag("b", "Same", "B")), Repeat("ARG003", 2) },
		{ Join(Option("s", "One", "A"), Option("s", "Two", "B"), Flag("s", "Three", "C")), Repeat("ARG003", 3) },
		{ Join(Option("s", "Same", "A"), Option("s", "Same", "B")), Repeat("ARG003", 4) },
		{ Join(Option("s", "One", "A"), Option("s", "Two", "B"), Option("t", "Three", "C"), Option("t", "Four", "D")), Repeat("ARG003", 4) },
	};

	[Theory]
	[MemberData(nameof(DuplicateNames))]
	public void ARG003_DuplicateNames_ReportsOncePerOffendingProperty(string members, string[] expectedIds)
	{
		var result = Diagnose(members);

		Assert.Equal(expectedIds, result.Ids);
		Assert.All(result.Diagnostics, d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
		Assert.Empty(result.Output);
	}

	[Fact]
	public void ARG003_MessageNamesTheKindOfNameAndTheName()
	{
		var shortResult = Diagnose(Join(Option("s", "One", "A"), Option("s", "Two", "B")));
		var longResult = Diagnose(Join(Option("a", "Same", "A"), Option("b", "Same", "B")));

		Assert.All(shortResult.Diagnostics, d => Assert.Contains("Duplicate short name 's'", d.GetMessage()));
		Assert.All(longResult.Diagnostics, d => Assert.Contains("Duplicate long name 'Same'", d.GetMessage()));
	}

	public static TheoryData<string> DistinctNames => new()
	{
		Join(Option("s", "One", "A"), Option("t", "Two", "B")),
		Join(Option("s", "One", "A"), Flag("t", "Two", "B")),
		Join(Option("s", "", "A"), Option("t", "", "B")),
		Join(Option("", "One", "A"), Option("", "Two", "B")),
		Join(Flag("", "One", "A"), Flag("", "Two", "B"), Option("", "Three", "C")),
		Join(Option("v", "Verbose", "A"), Flag("V", "verbose", "B")),
		Join(Option("s", "One", "A"), Positional(0, "B")),
	};

	[Theory]
	[MemberData(nameof(DistinctNames))]
	public void ARG003_DistinctOrEmptyNames_AreNotReported(string members)
	{
		var result = Diagnose(members);

		Assert.Empty(result.Rule("ARG003"));
	}

	[Fact]
	public void ARG003_OptionNameCollidingWithTheGeneratedHelpFlag_IsReported()
	{
		var result = Diagnose(Option("h", "Host", "Host"), attribute: "[ParameterCollection]");

		Assert.NotEmpty(result.Rule("ARG003"));
	}

	[Fact]
	public void ARG003_OptionNameMatchingTheHelpFlagIsFineWhenNoHelpArgumentIsGenerated()
	{
		var result = Diagnose(Option("h", "Help", "Host"));

		Assert.Empty(result.Rule("ARG003"));
	}
	#endregion

	#region ARG004 and ARG005
	[Theory]
	[InlineData(0, 0)]
	[InlineData(1, 1)]
	public void ARG004_TwoPositionalsOnTheSamePosition_ReportsBoth(int first, int second)
	{
		var result = Diagnose(Join(Positional(first, "A"), Positional(second, "B")));

		Assert.Equal(Repeat("ARG004", 2), result.Ids.Where(id => id == "ARG004"));
		Assert.All(result.Rule("ARG004"), d => Assert.Contains($"'{first}'", d.GetMessage()));
		Assert.Empty(result.Output);
	}

	[Fact]
	public void ARG004_ThreeOnOnePositionAndTwoOnAnother_ReportsEveryOffender()
	{
		var result = Diagnose(Join(Positional(0, "A"), Positional(0, "B"), Positional(0, "C"), Positional(1, "D"), Positional(1, "E")));

		Assert.Equal(5, result.Rule("ARG004").Count);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(99)]
	public void ARG005_SinglePositionalOutsideTheRange_IsReported(int position)
	{
		var result = Diagnose(Positional(position, "A"));

		var diagnostic = Assert.Single(result.Rule("ARG005"));
		Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
		Assert.Contains($"'{position}'", diagnostic.GetMessage());
		Assert.Empty(result.Output);
	}

	[Fact]
	public void ARG005_GapInPositions_ReportsTheOutOfRangeOnes()
	{
		var result = Diagnose(Join(Positional(0, "A"), Positional(2, "B")));

		var diagnostic = Assert.Single(result.Rule("ARG005"));
		Assert.Contains("'2'", diagnostic.GetMessage());
	}

	[Fact]
	public void ARG005_StartingAtOne_ReportsTheLastPosition()
	{
		var result = Diagnose(Join(Positional(1, "A"), Positional(2, "B")));

		var diagnostic = Assert.Single(result.Rule("ARG005"));
		Assert.Contains("'2'", diagnostic.GetMessage());
	}

	public static TheoryData<string> ValidPositionals => new()
	{
		Positional(0, "A"),
		Join(Positional(0, "A"), Positional(1, "B")),
		Join(Positional(1, "B"), Positional(0, "A")),
		Join(Positional(2, "C"), Positional(0, "A"), Positional(1, "B")),
	};

	[Theory]
	[MemberData(nameof(ValidPositionals))]
	public void ARG004And5_ContiguousPositionsInAnyDeclarationOrder_AreNotReported(string members)
	{
		var result = Diagnose(members);

		Assert.Empty(result.Rule("ARG004"));
		Assert.Empty(result.Rule("ARG005"));
	}
	#endregion

	#region ARG006
	[Theory]
	[InlineData("public TestClass(int count) { }")]
	[InlineData("public TestClass(string a, string b) { }")]
	[InlineData("private TestClass(int count) { }")]
	[InlineData("public TestClass(int a) { } public TestClass(int a, int b) { }")]
	public void ARG006_OnlyConstructorsWithParameters_IsReported(string constructors)
	{
		var result = Diagnose(Join(Option("o", "Output", "Output"), constructors));

		var diagnostic = Assert.Single(result.Diagnostics);
		Assert.Equal("ARG006", diagnostic.Id);
		Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
		Assert.Empty(result.Output);
	}

	[Theory]
	[InlineData("")]
	[InlineData("public TestClass() { }")]
	[InlineData("private TestClass() { }")]
	[InlineData("public TestClass() { } public TestClass(int count) { }")]
	[InlineData("public TestClass(int count) { } public TestClass() { }")]
	public void ARG006_ParameterlessConstructorOrNone_IsNotReported(string constructors)
	{
		var result = Diagnose(Join(Option("o", "Output", "Output"), constructors));

		Assert.Empty(result.Rule("ARG006"));
	}

	[Fact]
	public void ARG006_PrimaryConstructorWithParameters_IsReported()
	{
		var result = Diagnose(Option("o", "Output", "Output"), classNameSuffix: "(int count)");

		Assert.Single(result.Rule("ARG006"));
	}

	[Fact]
	public void ARG006_PrimaryConstructorWithAParameterlessConstructorAlongside_IsNotReported()
	{
		var result = Diagnose(Join(Option("o", "Output", "Output"), "public TestClass() : this(0) { }"), classNameSuffix: "(int count)");

		Assert.Empty(result.Rule("ARG006"));
	}

	[Fact]
	public void ARG006_StaticConstructorDoesNotCountAsParameterless()
	{
		var result = Diagnose(Join(Option("o", "Output", "Output"), "static TestClass() { } public TestClass(int count) { }"));

		Assert.Single(result.Rule("ARG006"));
	}
	#endregion

	#region ARG007
	private static readonly string[] SupportedTypes =
	[
		"int", "double", "float", "long", "short", "decimal", "byte", "sbyte", "char", "uint", "ulong", "ushort",
		"bool", "string", "Guid", "Uri", "TimeSpan", "DateTime", "Colour",
		"Int32", "System.Int32", "System.String", "System.Guid", "System.DateTime", "Boolean",
	];

	private static readonly string[] UnsupportedTypes =
	[
		"object", "Custom", "CustomStruct", "FileInfo", "DateTimeOffset", "Version", "List<string>", "int[]", "string[]", "Array",
	];

	public static TheoryData<string> SupportedTypeNames => new(SupportedTypes);

	public static TheoryData<string> UnsupportedTypeNames => new(UnsupportedTypes);

	[Theory]
	[MemberData(nameof(SupportedTypeNames))]
	public void ARG007_SupportedTypeOnAnOption_IsNotReported(string type)
	{
		var result = Diagnose(Option("o", "Output", "Output", type));

		Assert.Empty(result.Rule("ARG007"));
		Assert.NotEmpty(result.Output);
	}

	[Theory]
	[MemberData(nameof(SupportedTypeNames))]
	public void ARG007_SupportedTypeOnAPositional_IsNotReported(string type)
	{
		var result = Diagnose(Positional(0, "Input", type));

		Assert.Empty(result.Rule("ARG007"));
		Assert.NotEmpty(result.Output);
	}

	[Theory]
	[MemberData(nameof(UnsupportedTypeNames))]
	public void ARG007_UnsupportedTypeOnAnOption_IsReportedAndNothingIsGenerated(string type)
	{
		var result = Diagnose(Option("o", "Output", "Output", type));

		var diagnostic = Assert.Single(result.Rule("ARG007"));
		Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
		Assert.Empty(result.Output);
	}

	[Theory]
	[MemberData(nameof(UnsupportedTypeNames))]
	public void ARG007_UnsupportedTypeOnAPositional_IsReported(string type)
	{
		var result = Diagnose(Positional(0, "Input", type));

		Assert.Single(result.Rule("ARG007"));
	}

	[Fact]
	public void ARG007_MessageContainsTheFullTypeName()
	{
		var result = Diagnose(Option("o", "Output", "Output", "Custom"));

		var diagnostic = Assert.Single(result.Rule("ARG007"));
		Assert.Contains("'Diagnostics.Custom'", diagnostic.GetMessage());
	}

	[Fact]
	public void ARG007_ReportedOncePerUnsupportedProperty()
	{
		var result = Diagnose(Join(
			Option("a", "A", "A", "Custom"),
			Option("b", "B", "B", "int"),
			Positional(0, "C", "FileInfo"),
			Option("d", "D", "D", "object")));

		Assert.Equal(3, result.Rule("ARG007").Count);
	}

	[Fact]
	public void ARG007_UnsupportedTypeWithACustomParseMethod_IsNotReported()
	{
		var result = Diagnose(Join(
			"[ParsedWithMethod(\"TryParse\")] " + Option("o", "Output", "Output", "Custom"),
			"[ParsedWithMethod(\"TryParse\")] " + Positional(0, "Input", "CustomStruct")));

		Assert.Empty(result.Rule("ARG007"));
	}

	[Fact]
	public void ARG007_EnumOnEveryAttributeKind_IsNotReported()
	{
		var result = Diagnose(Join(Option("o", "Output", "A", "Colour"), Positional(0, "B", "Colour"), Flag("f", "Flag", "C", "Colour")));

		Assert.Empty(result.Rule("ARG007"));
		Assert.Empty(result.Rule("ARG013"));
	}

	[Fact]
	public void ARG007_NullableStringIsSupportedWhateverTheNullableContext()
	{
		var result = Diagnose(Option("o", "Output", "Output", "string?"));

		Assert.Empty(result.Rule("ARG007"));
	}
	#endregion

	#region ARG013
	[Theory]
	[InlineData("int")]
	[InlineData("string")]
	[InlineData("double")]
	[InlineData("DateTime")]
	[InlineData("Guid")]
	public void ARG013_FlagOnASupportedNonBooleanType_IsReportedAlone(string type)
	{
		var result = Diagnose(Flag("f", "Flag", "Value", type));

		var diagnostic = Assert.Single(result.Diagnostics);
		Assert.Equal("ARG013", diagnostic.Id);
		Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
		Assert.Empty(result.Output);
	}

	[Fact]
	public void ARG013_MessageNamesThePropertyAndItsType()
	{
		var result = Diagnose(Flag("f", "Flag", "Value", "int"));

		var message = Assert.Single(result.Rule("ARG013")).GetMessage();
		Assert.Contains("'Value'", message);
		Assert.Contains("'int'", message);
	}

	[Fact]
	public void ARG013_FlagOnAnUnsupportedType_ReportsBothRules()
	{
		var result = Diagnose(Flag("f", "Flag", "Value", "Custom"));

		Assert.Equal(["ARG007", "ARG013"], result.Ids);
	}

	[Theory]
	[InlineData("bool")]
	[InlineData("Boolean")]
	[InlineData("System.Boolean")]
	[InlineData("Colour")]
	public void ARG013_FlagOnBooleanOrEnum_IsNotReported(string type)
	{
		var result = Diagnose(Flag("f", "Flag", "Value", type));

		Assert.Empty(result.Rule("ARG013"));
		Assert.NotEmpty(result.Output);
	}
	#endregion

	#region command nodes (ARG014 to ARG016)
	// The rules about a tree as a whole (ARG017 to ARG022) are tested in CommandTreeLinkerTests; a class on its own is not a tree.
	private const string GroupAttribute = "[Command(\"group\", \"A group\")]";

	private const string LeafAttributes = SafeAttribute + "\n[Command(\"leaf\", \"A leaf\")]";

	[Fact]
	public void CommandNodes_ValidGroupAndRootOnTheirOwn_ReportNoNodeRulesAndGenerateNothing()
	{
		var group = Diagnose(Option("o", "Output", "Output"), attribute: GroupAttribute);
		var root = Diagnose(Option("o", "Output", "Output"), attribute: "[CommandApp(\"app\", \"An app\")]");

		Assert.Empty(group.Ids);
		Assert.Empty(group.Output);
		Assert.Empty(root.Ids);
		Assert.Empty(root.Output);
	}

	[Fact]
	public void CommandNodes_ValidLeaf_GeneratesItsParserAsBeforeWithAParseCoreForTheTree()
	{
		var leaf = Diagnose(Option("o", "Output", "Output"), attribute: LeafAttributes);
		var plain = Diagnose(Option("o", "Output", "Output"));

		Assert.Empty(leaf.Ids);
		// the tables, the help text and Parse are the same; ParseCore hands its work to the overload the tree uses too
		var startOfParseCore = plain.Output.IndexOf("internal static", StringComparison.Ordinal);
		var parse = plain.Output[plain.Output.IndexOf("public static (TestClass result", StringComparison.Ordinal)..plain.Output.LastIndexOf("    }\n}", StringComparison.Ordinal)];
		Assert.StartsWith(plain.Output[..startOfParseCore], leaf.Output, StringComparison.Ordinal);
		Assert.Contains(parse, leaf.Output, StringComparison.Ordinal);
		Assert.Contains("var (instance, errors, missingRequired, _) = ParseCore(args, flags, null);", leaf.Output, StringComparison.Ordinal);
		Assert.Contains("ParseCore(string[] args, global::ArgumentParser.FlagAttribute[] flagsTable, global::ArgumentParser.FlagAttribute help)", leaf.Output, StringComparison.Ordinal);
		Assert.DoesNotContain("FlagAttribute help)", plain.Output, StringComparison.Ordinal);
	}

	[Fact]
	public void ARG014_InvalidCommandName_IsReportedOnTheAttribute()
	{
		var result = Diagnose("", attribute: "[Command(\"bad name\", \"A group\")]");

		var diagnostic = Assert.Single(result.Diagnostics);
		Assert.Equal("ARG014", diagnostic.Id);
		Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
		Assert.Equal("Command(\"bad name\", \"A group\")", result.TextAt(diagnostic));
		Assert.Contains("'bad name'", diagnostic.GetMessage());
		Assert.Contains("'TestClass'", diagnostic.GetMessage());
	}

	[Fact]
	public void ARG014_InvalidNameOfALeaf_IsReportedOnce()
	{
		var result = Diagnose("", attribute: SafeAttribute + "\n[Command(\"-leaf\")]");

		Assert.Equal(["ARG014"], result.Ids);
		// the parser of the leaf is still generated, the name only matters to the tree
		Assert.NotEmpty(result.Output);
	}

	[Fact]
	public void ARG015_PositionalOnAGroup_IsReportedOnTheProperty()
	{
		var result = Diagnose(Join(Positional(0, "First"), Option("o", "Output", "Output")), attribute: GroupAttribute);

		var diagnostic = Assert.Single(result.Diagnostics);
		Assert.Equal("ARG015", diagnostic.Id);
		Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
		Assert.Equal("First", result.TextAt(diagnostic));
	}

	[Fact]
	public void ARG015_PositionalOnARoot_IsReported()
	{
		var result = Diagnose(Positional(0, "First"), attribute: "[CommandApp(\"app\")]");

		Assert.Equal(["ARG015"], result.Ids);
	}

	[Fact]
	public void ARG015_PositionalOnALeaf_IsNotReported()
	{
		var result = Diagnose(Positional(0, "First"), attribute: LeafAttributes);

		Assert.Empty(result.Ids);
	}

	[Fact]
	public void ARG016_RootCombinedWithACommand_IsReportedOnceOnTheRoot()
	{
		var result = Diagnose("", attribute: "[CommandApp(\"app\")]\n[Command(\"x\")]");

		var diagnostic = Assert.Single(result.Diagnostics);
		Assert.Equal("ARG016", diagnostic.Id);
		Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
		Assert.Equal("CommandApp(\"app\")", result.TextAt(diagnostic));
	}

	[Fact]
	public void ARG016_RootCombinedWithAParameterCollection_IsReported()
	{
		var result = Diagnose("", attribute: "[CommandApp(\"app\")]\n" + SafeAttribute);

		Assert.Equal(["ARG016"], result.Ids);
	}

	[Fact]
	public void ARG016_RootCombinedWithAParameterCollection_ReportsTheRulesOfTheClassAndItsArgumentsOnce()
	{
		// both pipelines see the class: the one of the ParameterCollection attribute reports what is wrong with the class and its arguments
		var members = Join(Option("s", "One", "A"), Option("s", "Two", "B"), "public static int Parse(string[] args) => 0;", "public TestClass(int count) { }");

		var notPartial = Diagnose(members, attribute: "[CommandApp(\"app\")]\n" + SafeAttribute, modifiers: "public");
		var partial = Diagnose(Option("s", "One", "A") + "\n" + Option("s", "Two", "B"), attribute: "[CommandApp(\"app\")]\n" + SafeAttribute);

		Assert.Equal(["ARG001", "ARG002", "ARG006", "ARG016"], notPartial.Ids);
		Assert.Equal(["ARG003", "ARG003", "ARG016"], partial.Ids);
	}

	[Theory]
	[InlineData("HelpTextGeneration.GenerateAll", "DisplayHelpAndExit", false)]
	[InlineData("HelpTextGeneration.GenerateTextOnly", "DisplayHelpAndExit", false)]
	[InlineData("HelpTextGeneration.None", "ThrowNever", false)]
	[InlineData("HelpTextGeneration.None", "ThrowIfAnyError", false)]
	[InlineData("HelpTextGeneration.None", "DisplayHelpAndExit", true)]
	[InlineData("HelpTextGeneration.GenerateArgumentOnly", "ThrowNever", true)]
	[InlineData("HelpTextGeneration.GenerateArgumentAndHandler", "ThrowNever", true)]
	public void ARG028_HelpSettingsOfTheCommandApp_MustLetTheTreeGenerateItsHelp(string mode, string behaviour, bool reported)
	{
		var attribute = $"[CommandApp(\"app\", \"\", {mode}, behaviourOnError: BehaviourOnError.{behaviour})]";

		var result = Diagnose("", attribute: attribute);

		Assert.Equal(reported ? ["ARG028"] : Array.Empty<string>(), result.Ids);
		if (reported)
		{
			var diagnostic = result.Diagnostics[0];
			Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
			Assert.Equal(attribute[1..^1], result.TextAt(diagnostic));
			Assert.Contains(mode, diagnostic.GetMessage(), StringComparison.Ordinal);
		}
	}

	[Theory]
	[InlineData(GroupAttribute)]
	[InlineData("[CommandApp(\"app\")]")]
	[InlineData(LeafAttributes)]
	public void CommandNodes_NotPartial_ReportARG001Once(string attribute)
	{
		var result = Diagnose("", attribute: attribute, modifiers: "public");

		Assert.Equal(["ARG001"], result.Ids);
	}

	[Fact]
	public void CommandNodes_OnlyTheRootMayDeclareAParseMethod()
	{
		const string parse = "public static int Parse(string[] args) => 0;";

		Assert.Equal(["ARG002"], Diagnose(parse, attribute: "[CommandApp(\"app\")]").Ids);
		Assert.Empty(Diagnose(parse, attribute: GroupAttribute).Ids);
	}

	[Fact]
	public void CommandNodes_ConstructorWithParameters_IsReportedOnceForEachKind()
	{
		const string constructor = "public TestClass(int count) { }";

		Assert.Equal(["ARG006"], Diagnose(constructor, attribute: GroupAttribute).Ids);
		Assert.Equal(["ARG006"], Diagnose(constructor, attribute: "[CommandApp(\"app\")]").Ids);
		Assert.Equal(["ARG006"], Diagnose(constructor, attribute: LeafAttributes).Ids);
	}
	#endregion

	#region ARG026
	// a command class nested in the class under test, so every kind of class can refer to it
	private const string CommandClass = "[ParameterCollection] [Command(\"cmd\")] public partial class Cmd { }";

	public static TheoryData<string, string> CommandClassArguments()
	{
		var data = new TheoryData<string, string>();
		var arguments = new[]
		{
			"[Option(\"c\", \"Cmd\", \"desc\")]",
			"[Flag(\"c\", \"Cmd\", \"desc\")]",
			"[Positional(0, \"desc\")]",
			"[Option(\"c\", \"Cmd\", \"desc\")] [ParsedWithMethod(\"Read\")]",
		};
		foreach (var attribute in new[] { SafeAttribute, LeafAttributes, GroupAttribute, "[CommandApp(\"app\")]" })
		{
			foreach (var argument in arguments)
			{
				data.Add(attribute, argument);
			}
		}
		return data;
	}

	[Theory]
	[MemberData(nameof(CommandClassArguments))]
	public void ARG026_ArgumentOfACommandClassType_IsTheOnlyRuleReported(string attribute, string argument)
	{
		var property = $"{argument} public Cmd? Command {{ get; set; }}";
		var result = Diagnose(Join(CommandClass, property), attribute: attribute);

		var diagnostic = Assert.Single(result.Diagnostics);
		Assert.Equal("ARG026", diagnostic.Id);
		Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
		// like the other rules about arguments, it is reported on the whole property
		Assert.Equal(property, result.TextAt(diagnostic));
	}

	[Fact]
	public void ARG026_MessageNamesThePropertyTheAttributeAndTheType()
	{
		var result = Diagnose(Join(CommandClass, "[Flag(\"c\", \"Cmd\", \"desc\")] public Cmd? Command { get; set; }"));

		var message = Assert.Single(result.Rule("ARG026")).GetMessage();
		Assert.Contains("'Command'", message);
		Assert.Contains("Flag attribute", message);
		Assert.Contains("'Diagnostics.TestClass.Cmd?'", message);
	}

	[Fact]
	public void ARG026_OnTheRoot_LeavesTheTreeRulesQuiet()
	{
		var result = Diagnose(
			Join(CommandClass, "[Option(\"c\", \"Cmd\", \"desc\")] public Cmd? Command { get; set; }"),
			attribute: "[CommandApp(\"app\")]",
			includeTreeRules: true);

		Assert.Equal(["ARG026"], result.Ids);
	}

	[Fact]
	public void ARG026_TypeThatIsNotACommandClass_IsNotReported()
	{
		var result = Diagnose("[Option(\"c\", \"Cmd\", \"desc\")] [ParsedWithMethod(\"Read\")] public Custom? Command { get; set; }");

		Assert.Empty(result.Rule("ARG026"));
	}
	#endregion

	#region generation options (ARG008 to ARG012)
	private static string Attribute(string mode, string behaviour, string? shortName = null, string? longName = null)
		=> $"[ParameterCollection(HelpTextGeneration.{mode}, \"{shortName ?? "h"}\", \"{longName ?? "Help"}\", BehaviourOnError.{behaviour})]";

	private const string UserHelpText = "public const string HelpText = \"help\";";

	private static readonly string[] Behaviours = ["DisplayHelpAndExit", "ThrowIfAnyError", "ThrowNever", "ThrowIfMissingRequired"];

	public static TheoryData<string, string, string[]> HelpOptionsWithoutUserMembers()
	{
		var data = new TheoryData<string, string, string[]>();
		foreach (var behaviour in Behaviours)
		{
			var exits = behaviour == "DisplayHelpAndExit";
			data.Add("GenerateAll", behaviour, []);
			data.Add("GenerateTextOnly", behaviour, []);
			data.Add("None", behaviour, exits ? ["ARG010"] : []);
			data.Add("GenerateArgumentOnly", behaviour, exits ? ["ARG010"] : []);
			data.Add("GenerateArgumentAndHandler", behaviour, exits ? ["ARG009", "ARG010"] : ["ARG009"]);
		}
		return data;
	}

	[Theory]
	[MemberData(nameof(HelpOptionsWithoutUserMembers))]
	public void HelpOptions_EveryModeAndBehaviourCombination_ReportsTheExpectedRules(string mode, string behaviour, string[] expectedIds)
	{
		var result = Diagnose(Option("o", "Output", "Output"), Attribute(mode, behaviour));

		Assert.Equal(expectedIds, result.Ids);
		Assert.All(result.Diagnostics, d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
		Assert.Equal(expectedIds.Length == 0, result.Output.Length > 0);
	}

	public static TheoryData<string, string, string[]> HelpOptionsWithUserHelpText()
	{
		var data = new TheoryData<string, string, string[]>();
		foreach (var behaviour in Behaviours)
		{
			data.Add("GenerateAll", behaviour, ["ARG012"]);
			data.Add("GenerateTextOnly", behaviour, ["ARG012"]);
			data.Add("None", behaviour, []);
			data.Add("GenerateArgumentOnly", behaviour, []);
			data.Add("GenerateArgumentAndHandler", behaviour, []);
		}
		return data;
	}

	[Theory]
	[MemberData(nameof(HelpOptionsWithUserHelpText))]
	public void HelpOptions_UserProvidedHelpText_IsOnlyAllowedWhenTheGeneratorDoesNotSupplyIt(string mode, string behaviour, string[] expectedIds)
	{
		var result = Diagnose(Join(Option("o", "Output", "Output"), UserHelpText), Attribute(mode, behaviour));

		Assert.Equal(expectedIds, result.Ids);
	}

	[Theory]
	[InlineData("public static readonly string HelpText = \"help\";")]
	[InlineData("public static string HelpText => \"help\";")]
	[InlineData("public string HelpText { get; set; } = \"help\";")]
	public void ARG009And10_HelpTextThatIsNotAConstant_DoesNotSatisfyTheRequirement(string helpText)
	{
		var result = Diagnose(Join(Option("o", "Output", "Output"), helpText), Attribute("GenerateArgumentAndHandler", "DisplayHelpAndExit"));

		Assert.Equal(["ARG009", "ARG010"], result.Ids);
	}

	[Theory]
	[InlineData("GenerateAll", true)]
	[InlineData("GenerateArgumentOnly", true)]
	[InlineData("GenerateArgumentAndHandler", true)]
	[InlineData("GenerateTextOnly", false)]
	[InlineData("None", false)]
	public void ARG011_UserDisplayHelpProperty_IsOnlyReportedWhenTheHelpArgumentIsGenerated(string mode, bool expected)
	{
		var result = Diagnose(Join(Option("o", "Output", "Output"), UserHelpText, "public bool DisplayHelp { get; set; }"), Attribute(mode, "ThrowNever"));

		Assert.Equal(expected, result.Rule("ARG011").Count == 1);
	}

	[Theory]
	[InlineData("GenerateTextOnly", "x", "Ex", true)]
	[InlineData("None", "x", "Ex", true)]
	[InlineData("None", "x", "Help", true)]
	[InlineData("None", "h", "Ex", true)]
	[InlineData("None", "h", "Help", false)]
	[InlineData("GenerateTextOnly", "h", "Help", false)]
	[InlineData("GenerateAll", "x", "Ex", false)]
	[InlineData("GenerateArgumentOnly", "x", "Ex", false)]
	[InlineData("GenerateArgumentAndHandler", "x", "Ex", false)]
	public void ARG008_CustomHelpNamesWithoutHelpArgument_IsAWarningThatDoesNotStopGeneration(string mode, string shortName, string longName, bool expected)
	{
		var result = Diagnose(Option("o", "Output", "Output"), Attribute(mode, "ThrowNever", shortName, longName));

		var warnings = result.Rule("ARG008");
		Assert.Equal(expected, warnings.Count == 1);
		if (expected)
		{
			Assert.Equal(DiagnosticSeverity.Warning, warnings[0].Severity);
		}
	}

	[Fact]
	public void ARG008_WarningDoesNotPreventGeneration()
	{
		var result = Diagnose(Option("o", "Output", "Output"), Attribute("GenerateTextOnly", "ThrowNever", "x", "Ex"));

		Assert.Equal(["ARG008"], result.Ids);
		Assert.NotEmpty(result.Output);
	}

	public static TheoryData<string> EquivalentConfigurationSyntax => new()
	{
		"[ParameterCollection(HelpTextGeneration.None, \"x\", \"Ex\", BehaviourOnError.ThrowNever)]",
		"[ParameterCollectionAttribute(HelpTextGeneration.None, \"x\", \"Ex\", BehaviourOnError.ThrowNever)]",
		"[ArgumentParser.ParameterCollection(ArgumentParser.HelpTextGeneration.None, \"x\", \"Ex\", ArgumentParser.BehaviourOnError.ThrowNever)]",
		"[global::ArgumentParser.ParameterCollection(global::ArgumentParser.HelpTextGeneration.None, \"x\", \"Ex\", global::ArgumentParser.BehaviourOnError.ThrowNever)]",
		"[ParameterCollection(helpTextGeneration: HelpTextGeneration.None, helpArgumentShortName: \"x\", helpArgumentLongName: \"Ex\", behaviourOnError: BehaviourOnError.ThrowNever)]",
		"[ParameterCollection(behaviourOnError: BehaviourOnError.ThrowNever, helpArgumentLongName: \"Ex\", helpArgumentShortName: \"x\", helpTextGeneration: HelpTextGeneration.None)]",
		"[ParameterCollection(HelpTextGeneration.None, \"x\", behaviourOnError: BehaviourOnError.ThrowNever, helpArgumentLongName: \"Ex\")]",
		"[Serializable] [ParameterCollection(HelpTextGeneration.None, \"x\", \"Ex\", BehaviourOnError.ThrowNever)]",
		"[Serializable, ParameterCollection(HelpTextGeneration.None, \"x\", \"Ex\", BehaviourOnError.ThrowNever)]",
	};

	[Theory]
	[MemberData(nameof(EquivalentConfigurationSyntax))]
	public void Configuration_EquivalentAttributeSyntax_IsReadTheSameWay(string attribute)
	{
		var result = Diagnose(Option("o", "Output", "Output"), attribute);

		Assert.Equal(["ARG008"], result.Ids);
	}

	[Fact]
	public void Configuration_DefaultAttributeWithoutArguments_GeneratesTheHelpArgumentAndText()
	{
		var result = Diagnose(Option("o", "Output", "Output"), "[ParameterCollection]");

		Assert.Empty(result.Ids);
		Assert.Contains("HelpText", result.Output, StringComparison.Ordinal);
		Assert.Contains("\"Help\"", result.Output, StringComparison.Ordinal);
	}

	[Fact]
	public void Configuration_CustomHelpNames_AreUsedForTheGeneratedArgument()
	{
		var result = Diagnose(Option("o", "Output", "Output"), Attribute("GenerateAll", "ThrowNever", "?", "Helpme"));

		Assert.Empty(result.Ids);
		Assert.Contains("FlagAttribute(\"?\", \"Helpme\"", result.Output, StringComparison.Ordinal);
	}
	#endregion

	#region classes in several parts
	[Fact]
	public void Parts_ArgumentsOfEveryPart_AreGenerated()
	{
		var result = Diagnose(Option("o", "Output", "Output"), otherPart: Join(Flag("v", "Verbose", "Verbose"), Positional(0, "Input")));

		Assert.Empty(result.Ids);
		Assert.Contains("\"Output\"", result.Output, StringComparison.Ordinal);
		Assert.Contains("\"Verbose\"", result.Output, StringComparison.Ordinal);
		Assert.Contains("Input", result.Output, StringComparison.Ordinal);
	}

	[Fact]
	public void Parts_DuplicateNamesAcrossParts_AreReportedInTheFileOfEachProperty()
	{
		var result = Diagnose(Option("s", "One", "A"), otherPart: Option("s", "Two", "B"));

		Assert.Equal(Repeat("ARG003", 2), result.Ids);
		Assert.Equal(["", "Other1.cs"], result.Diagnostics.Select(d => d.Location.GetLineSpan().Path).Order(StringComparer.Ordinal));
		Assert.Empty(result.Output);
	}

	[Fact]
	public void Parts_PositionsAreCountedOverEveryPart()
	{
		var result = Diagnose(Positional(1, "Second"), otherPart: Positional(0, "First"));

		Assert.Empty(result.Ids);
	}

	[Theory]
	[InlineData("public static int Parse(string[] args) => 0;", "GenerateTextOnly", "ARG002")]
	[InlineData("public TestClass(int count) { }", "GenerateTextOnly", "ARG006")]
	[InlineData("public bool DisplayHelp { get; set; }", "GenerateAll", "ARG011")]
	[InlineData("public const string HelpText = \"text\";", "GenerateTextOnly", "ARG012")]
	public void Parts_MembersOfAnotherPart_AreCheckedLikeTheirOwn(string member, string mode, string rule)
	{
		var result = Diagnose(Option("o", "Output", "Output"), Attribute(mode, "ThrowNever"), otherPart: member);

		Assert.Equal([rule], result.Ids);
	}
	#endregion

	#region pipeline
	[Fact]
	public void Pipeline_ClassLevelErrorsHideAttributeLevelErrors()
	{
		var result = Diagnose(Join(Option("s", "One", "A"), Option("s", "Two", "B")), modifiers: "public");

		Assert.Equal(["ARG001"], result.Ids);
	}

	[Fact]
	public void Pipeline_AttributeLevelErrorsHideGenerationOptionErrors()
	{
		var result = Diagnose(Join(Option("s", "One", "A"), Option("s", "Two", "B")), Attribute("None", "DisplayHelpAndExit", "x", "Ex"));

		Assert.Equal(Repeat("ARG003", 2), result.Ids);
	}

	[Fact]
	public void Pipeline_SeveralClassLevelProblemsAreAllReported()
	{
		var result = Diagnose(Join("public static int Parse(string[] args) => 0;", "public TestClass(int count) { }"), modifiers: "public");

		Assert.Equal(["ARG001", "ARG002", "ARG006"], result.Ids);
	}

	[Fact]
	public void Pipeline_SeveralAttributeLevelProblemsAreAllReported()
	{
		var result = Diagnose(Join(
			Option("s", "One", "A"),
			Option("s", "Two", "B"),
			Positional(0, "C"),
			Positional(0, "D"),
			Option("x", "Three", "E", "Custom"),
			Flag("f", "Flag", "F", "int")));

		Assert.Equal(["ARG003", "ARG003", "ARG004", "ARG004", "ARG007", "ARG013"], result.Ids);
	}

	[Fact]
	public void Pipeline_SeveralGenerationOptionProblemsAreAllReported()
	{
		var result = Diagnose(
			Join(Option("o", "Output", "Output"), "public bool DisplayHelp { get; set; }"),
			Attribute("GenerateArgumentAndHandler", "DisplayHelpAndExit"));

		Assert.Equal(["ARG009", "ARG010", "ARG011"], result.Ids);
	}

	[Fact]
	public void Pipeline_AttributeLevelDiagnosticsPointAtTheOffendingProperty()
	{
		var result = Diagnose(Join(Option("s", "One", "First"), Option("t", "Two", "Second"), Option("s", "Three", "Third")));

		var lines = result.Rule("ARG003")
			.Select(d => d.Location.GetLineSpan().StartLinePosition.Line)
			.OrderBy(line => line)
			.ToList();
		Assert.Equal(2, lines.Count);
		Assert.Equal(2, lines[1] - lines[0]);
	}

	[Fact]
	public void Pipeline_GenerationOptionDiagnosticsPointAtTheParameterCollectionAttribute()
	{
		var result = Diagnose(Option("o", "Output", "Output"), Attribute("None", "DisplayHelpAndExit"));

		var diagnostic = Assert.Single(result.Rule("ARG010"));
		var span = result.TextAt(diagnostic);
		Assert.StartsWith("ParameterCollection", span, StringComparison.Ordinal);
	}

	[Fact]
	public void Pipeline_ClassLevelDiagnosticsPointAtTheClass()
	{
		var result = Diagnose(Option("o", "Output", "Output"), modifiers: "public");

		var diagnostic = Assert.Single(result.Rule("ARG001"));
		var span = result.TextAt(diagnostic);
		Assert.Contains("class TestClass", span, StringComparison.Ordinal);
	}

	[Fact]
	public void Pipeline_ClassWithoutAnyArgumentProperties_GeneratesAParser()
	{
		var result = Diagnose("");

		Assert.Empty(result.Ids);
		Assert.Contains("Parse(string[] args)", result.Output, StringComparison.Ordinal);
	}

	[Fact]
	public void Pipeline_DiagnosticsInOneClassDoNotAffectAnother()
	{
		var source = $$"""
			using ArgumentParser;
			namespace Diagnostics;

			[ParameterCollection(HelpTextGeneration.GenerateTextOnly, "h", "Help", BehaviourOnError.ThrowNever)]
			public partial class Good { [Option("o", "Output", "desc")] public string Output { get; set; } = ""; }

			[ParameterCollection(HelpTextGeneration.GenerateTextOnly, "h", "Help", BehaviourOnError.ThrowNever)]
			public class Bad { [Option("o", "Output", "desc")] public string Output { get; set; } = ""; }
			""";

		var (diagnostics, output) = TestHelper.GetGeneratedOutput(source);

		Assert.Equal(["ARG001"], diagnostics.Where(d => d.Id.StartsWith("ARG", StringComparison.Ordinal)).Select(d => d.Id));
		Assert.Contains("class Good", output, StringComparison.Ordinal);
	}
	#endregion

	#region descriptors
	private static readonly DiagnosticDescriptor[] Descriptors = typeof(GeneratorDiagnostics)
		.GetFields(BindingFlags.Public | BindingFlags.Static)
		.Where(f => f.FieldType == typeof(DiagnosticDescriptor))
		.Select(f => (DiagnosticDescriptor)f.GetValue(null)!)
		.OrderBy(d => d.Id, StringComparer.Ordinal)
		.ToArray();

	public static TheoryData<string> RuleIds => new(Descriptors.Select(d => d.Id));

	private static string RepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArgumentParser.sln")))
		{
			directory = directory.Parent;
		}
		return directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root from " + AppContext.BaseDirectory);
	}

	[Fact]
	public void Descriptors_AreNumberedWithoutGaps()
	{
		var expected = Enumerable.Range(1, Descriptors.Length).Select(n => $"ARG{n:D3}");

		Assert.Equal(expected, Descriptors.Select(d => d.Id));
	}

	[Theory]
	[MemberData(nameof(RuleIds))]
	public void Descriptor_LinksToItsDocumentationPage(string id)
	{
		var descriptor = Descriptors.Single(d => d.Id == id);

		Assert.Equal($"https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/{id}.md", descriptor.HelpLinkUri);
		Assert.True(File.Exists(Path.Combine(RepositoryRoot(), "ArgumentParser.Analyzer", "docs", "rules", id + ".md")), $"docs/rules/{id}.md is missing");
	}

	[Theory]
	[MemberData(nameof(RuleIds))]
	public void Descriptor_IsListedInTheReleaseTrackingFiles(string id)
	{
		var descriptor = Descriptors.Single(d => d.Id == id);
		var tracked = new[] { "AnalyzerReleases.Shipped.md", "AnalyzerReleases.UnShipped.md" }
			.SelectMany(file => File.ReadAllLines(Path.Combine(RepositoryRoot(), "ArgumentParser.Analyzer", file)))
			.Where(line => line.StartsWith(id + " |", StringComparison.Ordinal))
			.ToList();

		var line = Assert.Single(tracked);
		var columns = line.Split('|', StringSplitOptions.TrimEntries);
		Assert.Equal(descriptor.Category, columns[1]);
		Assert.Equal(descriptor.DefaultSeverity.ToString(), columns[2]);
	}

	[Theory]
	[MemberData(nameof(RuleIds))]
	public void Descriptor_IsEnabledByDefaultWithATitleAndMessage(string id)
	{
		var descriptor = Descriptors.Single(d => d.Id == id);

		Assert.True(descriptor.IsEnabledByDefault);
		Assert.False(string.IsNullOrWhiteSpace(descriptor.Title.ToString()));
		Assert.False(string.IsNullOrWhiteSpace(descriptor.MessageFormat.ToString()));
	}

	[Fact]
	public void Descriptors_OnlySettingsWithoutEffectAndUnreachableCommandsAreWarnings()
	{
		var warnings = Descriptors.Where(d => d.DefaultSeverity == DiagnosticSeverity.Warning).Select(d => d.Id);

		Assert.Equal(["ARG008", "ARG019", "ARG029"], warnings);
	}
	#endregion
}
