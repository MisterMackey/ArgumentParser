using System.Globalization;
using System.Text.Json;

namespace ArgumentParser.Tests;

/// <summary>
/// Tests for the generated JSON binder (EmitJsonBinder): Bind of a class on its own and Bind of the root of a tree.
/// The generated code is compiled, Bind is called with JSON, and the bound object and the errors are checked.
/// </summary>
public class JsonBinderTests
{
	private const string Binder = "[ParameterCollection(HelpTextGeneration.GenerateTextOnly, \"h\", \"Help\", BehaviourOnError.ThrowNever, EmitJsonBinder = true)]";

	private const string Types = """
		public enum Level { Quiet, Normal, Loud }
		public class Point
		{
			public int X { get; set; }
			public static bool TryParse(string input, out Point value)
			{
				value = new Point();
				if (!int.TryParse(input, out var x))
				{
					return false;
				}
				value.X = x;
				return true;
			}
		}
		""";

	private static string ClassSource(string members, string attribute = Binder) => $$"""
		using System;
		using System.Collections.Generic;
		using ArgumentParser;

		namespace ArgumentParser.Tests;

		{{Types}}

		{{attribute}}
		public partial class TestClass
		{
		{{members}}
		}
		""";

	private static GeneratedParser Compile(string members) => GeneratedParser.Compile(ClassSource(members));

	private static void AssertError<T>(ParseOutcome outcome, string? key) where T : ArgumentParserException
	{
		var error = Assert.Single(outcome.Errors);
		Assert.IsType<T>(error);
		Assert.Equal(key, error.Key);
	}

	#region a class on its own

	private const string AllTypes = """
		[Option("a", "text", "")] public string Text { get; set; } = "";
		[Option("b", "number", "")] public int Number { get; set; }
		[Option("c", "big", "")] public ulong Big { get; set; }
		[Option("d", "small", "")] public sbyte Small { get; set; }
		[Option("e", "ratio", "")] public double Ratio { get; set; }
		[Option("f", "single", "")] public float Single { get; set; }
		[Option("g", "money", "")] public decimal Money { get; set; }
		[Option("i", "letter", "")] public char Letter { get; set; }
		[Option("j", "id", "")] public Guid Id { get; set; }
		[Option("k", "address", "")] public Uri Address { get; set; } = new Uri("http://x");
		[Option("l", "when", "")] public DateTime When { get; set; }
		[Option("m", "span", "")] public TimeSpan Span { get; set; }
		[Option("n", "mode", "")] public Level Mode { get; set; }
		[Option("o", "point", ""), ParsedWithMethod("TryParse")] public Point Where { get; set; } = new Point();
		[Flag("v", "verbose", "")] public bool Verbose { get; set; }
		[Flag("w", "level", "")] public Level Level { get; set; }
		[Positional(0, "")] public long First { get; set; }
		""";

	[Fact]
	public void Bind_EverySupportedType()
	{
		var parser = Compile(AllTypes);

		var outcome = parser.Bind("""
			{
				"text": "hello", "number": -12, "big": 18446744073709551615, "small": -128, "ratio": 1.5, "single": 2.25, "money": 10.10,
				"letter": "x", "id": "0f8fad5b-d9cb-469f-a165-70867728950e", "address": "../relative", "when": "2026-10-26T14:30:15Z",
				"span": "1.02:03:04", "mode": "Loud", "where": "7", "verbose": true, "level": 2, "first": 9000000000
			}
			""");

		Assert.Empty(outcome.Errors);
		Assert.Equal("hello", outcome.Get<string>("Text"));
		Assert.Equal(-12, outcome.Get<int>("Number"));
		Assert.Equal(ulong.MaxValue, outcome.Get<ulong>("Big"));
		Assert.Equal(sbyte.MinValue, outcome.Get<sbyte>("Small"));
		Assert.Equal(1.5, outcome.Get<double>("Ratio"));
		Assert.Equal(2.25f, outcome.Get<float>("Single"));
		Assert.Equal(10.10m, outcome.Get<decimal>("Money"));
		Assert.Equal('x', outcome.Get<char>("Letter"));
		Assert.Equal(Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"), outcome.Get<Guid>("Id"));
		Assert.Equal("../relative", outcome.Get<Uri>("Address")!.OriginalString);
		Assert.Equal(new DateTime(2026, 10, 26, 14, 30, 15, DateTimeKind.Utc), outcome.Get<DateTime>("When"));
		Assert.Equal(DateTimeKind.Utc, outcome.Get<DateTime>("When").Kind);
		Assert.Equal(new TimeSpan(1, 2, 3, 4), outcome.Get<TimeSpan>("Span"));
		Assert.Equal("Loud", outcome.Get<object>("Mode")!.ToString());
		Assert.Equal(7, outcome.GetPath<int>("Where.X"));
		Assert.True(outcome.Get<bool>("Verbose"));
		Assert.Equal("Loud", outcome.Get<object>("Level")!.ToString());
		Assert.Equal(9000000000L, outcome.Get<long>("First"));
	}

	[Fact]
	public void Bind_AKeyThatIsNotGiven_LeavesThePropertyAlone()
	{
		var parser = Compile("""
			[Option("r", "retries", "")] public int Retries { get; set; } = 3;
			[Option("n", "name", "")] public string Name { get; set; } = "default";
			""");

		var outcome = parser.Bind("""{"name": "x"}""");

		Assert.Empty(outcome.Errors);
		Assert.Equal(3, outcome.Get<int>("Retries"));
		Assert.Equal("x", outcome.Get<string>("Name"));
	}

	[Fact]
	public void Bind_AMissingRequiredKey_IsAnErrorWithTheKey()
	{
		var parser = Compile("""
			[Option("r", "retries", "", required: true)] public int Retries { get; set; }
			[Positional(0, "", true)] public string FilePath { get; set; } = "";
			""");

		var outcome = parser.Bind("""{"retries": 1}""");

		AssertError<MissingRequiredArgumentException>(outcome, "filePath");
		Assert.Contains("'filePath'", outcome.Errors[0].Message, StringComparison.Ordinal);
	}

	[Fact]
	public void Bind_ARequiredNullableKey_CanBeNull()
	{
		var parser = Compile("""[Option("r", "retries", "", required: true)] public int? Retries { get; set; } = 1;""");

		var given = parser.Bind("""{"retries": null}""");
		var missing = parser.Bind("{}");

		Assert.Empty(given.Errors);
		Assert.Null(given.Get<int?>("Retries"));
		AssertError<MissingRequiredArgumentException>(missing, "retries");
	}

	[Fact]
	public void Bind_NullForAnArgumentThatIsNotNullable_IsAnError()
	{
		var parser = Compile("""[Option("r", "retries", "")] public int Retries { get; set; } = 3;""");

		var outcome = parser.Bind("""{"retries": null}""");

		AssertError<InvalidArgumentValueException>(outcome, "retries");
		Assert.Equal(3, outcome.Get<int>("Retries"));
		Assert.Contains("Invalid value for 'retries': null. Expected an integer from -2147483648 to 2147483647.", outcome.Errors[0].Message, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("1.5")]
	[InlineData("\"5\"")]
	[InlineData("2147483648")]
	[InlineData("2147483648.0")]
	[InlineData("1e10")]
	[InlineData("1e400")]
	[InlineData("true")]
	public void Bind_AnIntegerMustBeAWholeJsonNumberInRange(string value)
	{
		var parser = Compile("""[Option("r", "retries", "")] public int Retries { get; set; }""");

		var outcome = parser.Bind($$"""{"retries": {{value}}}""");

		AssertError<InvalidArgumentValueException>(outcome, "retries");
	}

	[Theory]
	// "type": "integer" of JSON Schema takes every number without a fraction, whatever its form
	[InlineData("int", "1.0", "1")]
	[InlineData("int", "1e3", "1000")]
	[InlineData("int", "1.5e1", "15")]
	[InlineData("int", "-0.0", "0")]
	[InlineData("int", "2147483647.0", "2147483647")]
	[InlineData("ulong", "18446744073709551615.0", "18446744073709551615")]
	[InlineData("byte", "2.55e2", "255")]
	[InlineData("sbyte", "-1.28E2", "-128")]
	public void Bind_AnIntegerInAnotherFormIsAccepted(string type, string value, string expected)
	{
		var parser = Compile($$"""
			[Option("r", "retries", "")] public {{type}} Retries { get; set; }
			[Option("l", "list", "")] public List<{{type}}> List { get; set; }
			""");

		var outcome = parser.Bind($$"""{"retries": {{value}}, "list": [{{value}}]}""");

		Assert.Empty(outcome.Errors);
		Assert.Equal(expected, Convert.ToString(outcome.Get<object>("Retries"), CultureInfo.InvariantCulture));
		Assert.Equal(expected, Convert.ToString(outcome.Get<System.Collections.IEnumerable>("List")!.Cast<object>().Single(), CultureInfo.InvariantCulture));
	}

	[Theory]
	[InlineData("byte", "2.56e2")]
	[InlineData("ulong", "-1.0")]
	[InlineData("ulong", "18446744073709551616.0")]
	public void Bind_AnIntegerInAnotherFormMustBeInRange(string type, string value)
	{
		var parser = Compile($$"""[Option("r", "retries", "")] public {{type}} Retries { get; set; }""");

		AssertError<InvalidArgumentValueException>(parser.Bind($$"""{"retries": {{value}}}"""), "retries");
	}

	[Theory]
	[InlineData("\"true\"")]
	[InlineData("1")]
	public void Bind_ABoolMustBeAJsonBoolean(string value)
	{
		var parser = Compile("""[Flag("v", "verbose", "")] public bool Verbose { get; set; }""");

		AssertError<InvalidArgumentValueException>(parser.Bind($$"""{"verbose": {{value}}}"""), "verbose");
		Assert.False(parser.Bind("""{"verbose": false}""").Get<bool>("Verbose"));
	}

	[Theory]
	[InlineData("\"Normal\"", null)]
	[InlineData("\"normal\"", "mode")]
	[InlineData("\"1\"", "mode")]
	[InlineData("1", "mode")]
	[InlineData("\"Quiet, Loud\"", "mode")]
	public void Bind_AnEnumOptionTakesOnlyTheNamesOfItsMembers(string value, string? errorKey)
	{
		var parser = Compile("""[Option("m", "mode", "")] public Level Mode { get; set; }""");

		var outcome = parser.Bind($$"""{"mode": {{value}}}""");

		if (errorKey is null)
		{
			Assert.Empty(outcome.Errors);
			Assert.Equal("Normal", outcome.Get<object>("Mode")!.ToString());
		}
		else
		{
			AssertError<InvalidArgumentValueException>(outcome, errorKey);
			Assert.Contains("Expected one of: Quiet, Normal, Loud.", outcome.Errors[0].Message, StringComparison.Ordinal);
		}
	}

	[Theory]
	[InlineData("\"Loud\"", "Loud")]
	[InlineData("0", "Quiet")]
	[InlineData("2", "Loud")]
	[InlineData("3", null)]
	[InlineData("-1", null)]
	[InlineData("null", null)]
	public void Bind_AnEnumFlagTakesANameOrALevel(string value, string? expected)
	{
		var parser = Compile("""[Flag("l", "level", "")] public Level Level { get; set; }""");

		var outcome = parser.Bind($$"""{"level": {{value}}}""");

		if (expected is null)
		{
			AssertError<InvalidArgumentValueException>(outcome, "level");
		}
		else
		{
			Assert.Empty(outcome.Errors);
			Assert.Equal(expected, outcome.Get<object>("Level")!.ToString());
		}
	}

	[Fact]
	public void Bind_NullableArgumentsTakeNull()
	{
		var parser = Compile("""
			[Option("r", "retries", "")] public int? Retries { get; set; } = 1;
			[Option("n", "name", "")] public string? Name { get; set; } = "x";
			[Flag("l", "level", "")] public Level? Level { get; set; } = global::ArgumentParser.Tests.Level.Loud;
			[Option("i", "items", "")] public List<int>? Items { get; set; } = new List<int> { 1 };
			""");

		var outcome = parser.Bind("""{"retries": null, "name": null, "level": null, "items": null}""");

		Assert.Empty(outcome.Errors);
		Assert.Null(outcome.Get<int?>("Retries"));
		Assert.Null(outcome.Get<string>("Name"));
		Assert.Null(outcome.Get<object>("Level"));
		Assert.Null(outcome.Get<object>("Items"));
	}

	[Theory]
	[InlineData("{\"id\": \"{0f8fad5b-d9cb-469f-a165-70867728950e}\"}", "id")]
	[InlineData("{\"id\": \"0f8fad5bd9cb469fa16570867728950e\"}", "id")]
	[InlineData("{\"letter\": \"xy\"}", "letter")]
	[InlineData("{\"letter\": \"\"}", "letter")]
	[InlineData("{\"when\": \"10/26/2026\"}", "when")]
	[InlineData("{\"span\": \"PT1H\"}", "span")]
	[InlineData("{\"text\": 5}", "text")]
	[InlineData("{\"ratio\": \"1.5\"}", "ratio")]
	[InlineData("{\"single\": 1e300}", "single")]
	[InlineData("{\"small\": -129}", "small")]
	public void Bind_ValuesTheSchemaDoesNotDescribe_AreErrors(string json, string key)
	{
		var parser = Compile(AllTypes);

		AssertError<InvalidArgumentValueException>(parser.Bind(json), key);
	}

	[Fact]
	public void Bind_TextIsReadTheInvariantWay_WhateverUseCurrentCultureSays()
	{
		var parser = GeneratedParser.Compile(ClassSource(
			"""[Option("w", "when", "")] public DateTime When { get; set; }""",
			Binder.Replace("EmitJsonBinder = true", "EmitJsonBinder = true, UseCurrentCulture = true", StringComparison.Ordinal)));
		var culture = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = new CultureInfo("de-DE");

			// the command line reads the text with the culture, a tool call never does
			Assert.Empty(parser.Parse("--when", "26.10.2026").Errors);
			AssertError<InvalidArgumentValueException>(parser.Bind("""{"when": "26.10.2026"}"""), "when");
			Assert.Empty(parser.Bind("""{"when": "2026-10-26"}""").Errors);
		}
		finally
		{
			CultureInfo.CurrentCulture = culture;
		}
	}

	[Fact]
	public void Bind_AParseMethodGetsTheString()
	{
		var parser = Compile("""
			[Option("p", "point", ""), ParsedWithMethod("TryParse")] public Point Where { get; set; } = new Point();
			[Option("r", "redirect", ""), ParsedWithMethod("ReadDescriptor")] public int? Redirect { get; set; } = 1;
			[Option("m", "mode", ""), ParsedWithMethod("ReadLevel")] public Level Mode { get; set; }
			private static bool ReadDescriptor(string input, out int? value)
			{
				value = null;
				return input == "NULL";
			}
			private static bool ReadLevel(string input, out Level value)
			{
				value = Level.Loud;
				return input == "loudest";
			}
			""");

		var good = parser.Bind("""{"where": "3", "redirect": "NULL", "mode": "loudest"}""");
		var bad = parser.Bind("""{"where": 3}""");

		Assert.Empty(good.Errors);
		Assert.Equal(3, good.GetPath<int>("Where.X"));
		Assert.Null(good.Get<int?>("Redirect"));
		Assert.Equal("Loud", good.Get<object>("Mode")!.ToString());
		AssertError<InvalidArgumentValueException>(bad, "where");
		AssertError<InvalidArgumentValueException>(parser.Bind("""{"mode": "Loud"}"""), "mode");
	}

	[Fact]
	public void Bind_AValueAParseMethodRejects_SaysWhichMethodReadsIt()
	{
		var parser = Compile("""[Option("p", "point", ""), ParsedWithMethod("TryParse")] public List<Point> Points { get; set; }""");

		var outcome = parser.Bind("""{"points": ["1", "x"]}""");

		AssertError<InvalidArgumentValueException>(outcome, "points");
		Assert.Contains("at index 1: \"x\". Expected a string that TryParse accepts.", outcome.Errors[0].Message, StringComparison.Ordinal);
		Assert.Equal([1], outcome.Get<System.Collections.IEnumerable>("Points")!.Cast<object>().Select(p => (int)p.GetType().GetProperty("X")!.GetValue(p)!));
	}

	[Fact]
	public void Bind_AnEnumWithoutMembers_TakesNoValue()
	{
		// it compiles, and no value can be given, as the schema says (an empty enum list)
		var parser = GeneratedParser.Compile(ClassSource(
			"""
			[Option("e", "empty", "")] public Nothing Empty { get; set; }
			[Flag("f", "flag", "")] public Nothing Flag { get; set; }
			""",
			Binder).Replace("public enum Level", "public enum Nothing { }\npublic enum Level", StringComparison.Ordinal));

		Assert.Empty(parser.Bind("{}").Errors);
		AssertError<InvalidArgumentValueException>(parser.Bind("""{"empty": "Anything"}"""), "empty");
		AssertError<InvalidArgumentValueException>(parser.Bind("""{"flag": 0}"""), "flag");
	}

	[Fact]
	public void Bind_NullableTypes()
	{
		var parser = Compile("""
			[Option("a", "when", "")] public DateTime? When { get; set; }
			[Option("b", "id", "")] public Guid? Id { get; set; }
			[Option("c", "mode", "")] public Level? Mode { get; set; }
			[Option("d", "letter", "")] public char? Letter { get; set; }
			[Option("e", "money", "")] public decimal? Money { get; set; }
			[Option("f", "ids", "")] public List<Guid>? Ids { get; set; }
			[Option("g", "points", ""), ParsedWithMethod("TryParse")] public List<Point>? Points { get; set; }
			""");

		var given = parser.Bind("""
			{"when": "2026-10-26", "id": "0f8fad5b-d9cb-469f-a165-70867728950e", "mode": "Quiet", "letter": "q", "money": 1.25,
				"ids": ["0f8fad5b-d9cb-469f-a165-70867728950e"], "points": ["4"]}
			""");
		var nulls = parser.Bind("""{"when": null, "id": null, "mode": null, "letter": null, "money": null, "ids": null, "points": null}""");

		Assert.Empty(given.Errors);
		Assert.Equal(new DateTime(2026, 10, 26), given.Get<DateTime?>("When"));
		Assert.Equal(Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"), given.Get<Guid?>("Id"));
		Assert.Equal("Quiet", given.Get<object>("Mode")!.ToString());
		Assert.Equal('q', given.Get<char?>("Letter"));
		Assert.Equal(1.25m, given.Get<decimal?>("Money"));
		Assert.Single(given.Get<List<Guid>>("Ids")!);
		Assert.Single(given.Get<System.Collections.IEnumerable>("Points")!.Cast<object>());
		Assert.Empty(nulls.Errors);
		Assert.All(["When", "Id", "Mode", "Letter", "Money", "Ids", "Points"], name => Assert.Null(nulls.Get<object>(name)));
	}

	[Fact]
	public void Bind_PropertiesNamedLikeTheLocalsOfTheGeneratedCode()
	{
		var names = new[] { "Value", "Values", "Errors", "List", "Item", "Index", "Key", "Pair", "Instance", "Bound", "Property", "Arguments", "ValueValue", "ValueValueWhole" };
		var parser = Compile(string.Join("\n", names.Select((name, i) => $"[Option(\"{(char)('a' + i)}\", \"o{i}\", \"\")] public int {name} {{ get; set; }}")));

		var outcome = parser.Bind("{" + string.Join(", ", names.Select((name, i) => $"\"{ToolKey(name)}\": {i + 1}")) + "}");

		Assert.Empty(outcome.Errors);
		Assert.Equal(Enumerable.Range(1, names.Length), names.Select(name => outcome.Get<int>(name)));
	}

	private static string ToolKey(string name) => ArgumentParser.Internal.ToolSpecProvider.Key(name);

	[Fact]
	public void Bind_KeywordNamesAndTypesInTheGlobalNamespace()
	{
		var parser = GeneratedParser.Compile("""
			using System.Collections.Generic;
			using ArgumentParser;

			public enum Keywords { @class, @event }
			public class GlobalPoint { public static bool Read(string input, out GlobalPoint value) { value = new GlobalPoint(); return input == "p"; } }

			namespace ArgumentParser.Tests
			{
				[ParameterCollection(HelpTextGeneration.GenerateTextOnly, "h", "Help", BehaviourOnError.ThrowNever, EmitJsonBinder = true)]
				public partial class TestClass
				{
					[Option("c", "class", "")] public Keywords @class { get; set; }
					[Option("e", "events", "")] public List<Keywords> @event { get; set; }
					[Option("p", "point", ""), ParsedWithMethod("Read")] public GlobalPoint Point { get; set; }
				}
			}
			""");

		var outcome = parser.Bind("""{"class": "event", "event": ["class", "event"], "point": "p"}""");

		Assert.Empty(outcome.Errors);
		Assert.Equal("event", outcome.Get<object>("class")!.ToString());
		Assert.Equal(["class", "event"], outcome.Get<System.Collections.IEnumerable>("event")!.Cast<object>().Select(v => v.ToString()));
		Assert.NotNull(outcome.Get<object>("Point"));
	}

	[Fact]
	public void Bind_ListsAreArrays_AValueThatCannotBeReadIsAnErrorAndTheOthersAreKept()
	{
		var parser = Compile("""
			[Option("n", "number", "")] public List<int>? Numbers { get; set; }
			[Option("m", "mode", "")] public List<Level> Modes { get; set; } = new List<Level> { Level.Quiet };
			[Positional(0, "", true)] public List<string> Files { get; set; } = new List<string>();
			""");

		var outcome = parser.Bind("""{"numbers": [1, "two", 3, null], "modes": [], "files": ["a", "b"]}""");

		Assert.Equal(2, outcome.Errors.Count);
		Assert.All(outcome.Errors, e => Assert.Equal("numbers", e.Key));
		Assert.Contains("at index 1: \"two\"", outcome.Errors[0].Message, StringComparison.Ordinal);
		Assert.Equal([1, 3], outcome.Get<List<int>>("Numbers")!);
		// an empty array replaces the initializer
		Assert.Empty(outcome.Get<System.Collections.IEnumerable>("Modes")!.Cast<object>());
		Assert.Equal(["a", "b"], outcome.Get<List<string>>("Files")!);
	}

	[Fact]
	public void Bind_ARequiredListNeedsOneValueAtLeast_AndAValueThatIsNoArrayIsAnError()
	{
		var parser = Compile("""[Positional(0, "", true)] public List<string> Files { get; set; } = new List<string>();""");

		AssertError<MissingRequiredArgumentException>(parser.Bind("""{"files": []}"""), "files");
		AssertError<InvalidArgumentValueException>(parser.Bind("""{"files": "a"}"""), "files");
		AssertError<MissingRequiredArgumentException>(parser.Bind("{}"), "files");
	}

	[Fact]
	public void Bind_AnUnknownKey_IsAnError()
	{
		var parser = Compile("""
			[Option("n", "name", "")] public string Name { get; set; } = "";
			[Option("d", "db", ""), ExcludeFromManifest] public string Db { get; set; } = "app.db";
			""");

		// the key is the property name in camelCase, not the long name; an excluded argument cannot be set
		var outcome = parser.Bind("""{"name": "x", "Name": "y", "db": "other.db"}""");

		Assert.Equal(["Name", "db"], outcome.Errors.Select(e => e.Key));
		Assert.All(outcome.Errors, e => Assert.IsType<UnexpectedArgumentException>(e));
		Assert.Equal("x", outcome.Get<string>("Name"));
		Assert.Equal("app.db", outcome.Get<string>("Db"));
	}

	[Fact]
	public void Bind_AKeyGivenTwice_IsAnError()
	{
		var parser = Compile("""[Option("n", "name", "")] public string Name { get; set; } = "";""");

		var outcome = parser.Bind("""{"name": "x", "name": "y"}""");

		AssertError<UnexpectedArgumentException>(outcome, "name");
		Assert.Equal("x", outcome.Get<string>("Name"));
	}

	[Theory]
	[InlineData("[1, 2]")]
	[InlineData("\"text\"")]
	public void Bind_ArgumentsThatAreNoObject_AreAnError(string json)
	{
		var parser = Compile("""[Option("n", "name", "")] public string Name { get; set; } = "";""");

		AssertError<InvalidArgumentValueException>(parser.Bind(json), null);
	}

	[Fact]
	public void Bind_NoArgumentsAtAll_IsAnEmptyObject()
	{
		var parser = Compile("""[Option("n", "name", "")] public string Name { get; set; } = "x";""");

		Assert.Empty(parser.Bind((string?)null).Errors);
		Assert.Empty(parser.Bind("null").Errors);
		Assert.Empty(parser.Bind((IEnumerable<KeyValuePair<string, JsonElement>>?)null).Errors);
	}

	[Fact]
	public void Bind_TheDictionaryOverload_TakesTheArgumentsOfAnMcpCall()
	{
		// the C# MCP SDK hands over the arguments of a call as an IDictionary<string, JsonElement>, other code may have an IReadOnlyDictionary
		var parser = Compile("""
			[Option("n", "name", "")] public string Name { get; set; } = "";
			public static object FromSdk(IDictionary<string, System.Text.Json.JsonElement> arguments) => Bind(arguments);
			public static object FromReadOnly(IReadOnlyDictionary<string, System.Text.Json.JsonElement> arguments) => Bind(arguments);
			""");
		var arguments = new Dictionary<string, JsonElement> { ["name"] = JsonDocument.Parse("\"rice\"").RootElement.Clone() };

		var outcome = parser.Bind(arguments);

		Assert.Empty(outcome.Errors);
		Assert.Equal("rice", outcome.Get<string>("Name"));
	}

	[Theory]
	[InlineData("[ParameterCollection(HelpTextGeneration.GenerateTextOnly, \"h\", \"Help\", BehaviourOnError.ThrowNever)]")]
	[InlineData("[ParameterCollection(HelpTextGeneration.GenerateTextOnly, \"h\", \"Help\", BehaviourOnError.ThrowNever, EmitMcpManifest = true)]")]
	public void Bind_IsOnlyEmittedWithEmitJsonBinder(string attribute)
	{
		var parser = GeneratedParser.Compile(ClassSource("""[Option("n", "name", "")] public string Name { get; set; } = "";""", attribute));

		Assert.Throws<InvalidOperationException>(() => parser.Bind("{}"));
	}

	[Fact]
	public void Bind_TheGeneratedCodeHasNoWarnings()
	{
		var source = ClassSource(AllTypes.Replace(" = new Uri(\"http://x\")", "", StringComparison.Ordinal) + """

			[Option("q", "numbers", "", required: true)] public List<int> Numbers { get; set; }
			[Option("r", "retries", "")] public int? Retries { get; set; }
			""");

		Assert.Empty(GeneratedParser.GeneratedCodeDiagnostics(source));
	}
	#endregion

	#region the root of a tree

	private const string FileTool = """
		using System.Collections.Generic;
		using ArgumentParser;

		namespace ArgumentParser.Tests;

		[CommandApp("myFileManagingTool", "Manage files", HelpTextGeneration.GenerateTextOnly, behaviourOnError: BehaviourOnError.ThrowNever, EmitJsonBinder = true)]
		public partial class FileTool
		{
			[Option("r", "root", "Directory to work in"), ParsedWithMethod("ReadRoot")]
			public string Root { get; private set; } = ".";
			[Option("t", "token", "Set by the host"), ExcludeFromManifest]
			public string Token { get; set; } = "";

			public DirectoriesCommand Directories { get; private set; }
			public FilesCommand Files { get; private set; }

			private static bool ReadRoot(string input, out string value)
			{
				value = "/" + input;
				return input.Length > 0;
			}
		}

		[Command("directories", "Work with directories")]
		public partial class DirectoriesCommand
		{
			public ListDirectories List { get; private set; }
			public CreateDirectory Create { get; private set; }
		}

		[Command("files", "Work with files")]
		public partial class FilesCommand
		{
			[Flag("n", "dryrun", "Only show what would be done")]
			public bool DryRun { get; set; }

			public DeleteFile Delete { get; private set; }
		}

		[ParameterCollection]
		[Command("list", "List the directories")]
		public partial class ListDirectories
		{
			[Flag("", "showhidden", "Include hidden directories")]
			public bool ShowHidden { get; set; }
		}

		[ParameterCollection]
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
			[Option("", "patterns", "")]
			public List<string> Patterns { get; set; }
		}
		""";

	[Fact]
	public void BindTool_BindsTheChainFromTheRootToTheCommand()
	{
		var parser = GeneratedParser.Compile(FileTool, "FileTool");

		var outcome = parser.BindTool("files__delete", """{"root": "tmp", "dryRun": true, "filePath": "a.txt", "patterns": ["*.log"]}""");

		Assert.Empty(outcome.Errors);
		// a private setter and a private parse method of the root work, as on the command line
		Assert.Equal("/tmp", outcome.Get<string>("Root"));
		Assert.True(outcome.GetPath<bool>("Files.DryRun"));
		Assert.Equal("a.txt", outcome.GetPath<string>("Files.Delete.FilePath"));
		Assert.Equal(["*.log"], outcome.GetPath<List<string>>("Files.Delete.Patterns")!);
		Assert.Null(outcome.Get<object>("Directories"));
		// Selected and SelectedCommand work as after Parse
		Assert.Equal("DeleteFile", outcome.Get<object>("Selected")!.GetType().Name);
		Assert.Equal("files delete", outcome.Get<CommandInfo>("SelectedCommand")!.ToString());
	}

	[Fact]
	public void BindTool_TheArgumentsOfACommandAreChecked()
	{
		var parser = GeneratedParser.Compile(FileTool, "FileTool");

		// showHidden belongs to another command, token is excluded, and filePath is required
		var outcome = parser.BindTool("files__delete", """{"showHidden": true, "token": "secret"}""");

		Assert.Equal(["showHidden", "token", "filePath"], outcome.Errors.Select(e => e.Key));
		Assert.IsType<UnexpectedArgumentException>(outcome.Errors[0]);
		Assert.IsType<UnexpectedArgumentException>(outcome.Errors[1]);
		Assert.IsType<MissingRequiredArgumentException>(outcome.Errors[2]);
		Assert.NotNull(outcome.GetPath<object>("Files.Delete"));
		Assert.Equal("", outcome.Get<string>("Token"));
	}

	[Theory]
	[InlineData("files__remove")]
	[InlineData("files")]
	[InlineData("files delete")]
	[InlineData("")]
	[InlineData(null)]
	public void BindTool_AnUnknownToolName_IsAnError(string? toolName)
	{
		var parser = GeneratedParser.Compile(FileTool, "FileTool");

		var outcome = parser.BindTool(toolName, "{}");

		var error = Assert.Single(outcome.Errors);
		Assert.IsType<UnknownCommandException>(error);
		Assert.Null(error.Key);
		Assert.Contains("Expected one of: directories__list, directories__create, files__delete.", error.Message, StringComparison.Ordinal);
		Assert.Null(outcome.Get<object>("Selected"));
	}

	[Fact]
	public void BindTool_AKeyGivenTwice_IsAnError()
	{
		var parser = GeneratedParser.Compile(FileTool, "FileTool");

		var outcome = parser.BindTool("directories__create", """{"name": "a", "root": "x", "name": "b"}""");

		AssertError<UnexpectedArgumentException>(outcome, "name");
		Assert.Equal("a", outcome.GetPath<string>("Directories.Create.Name"));
	}

	[Fact]
	public void BindTool_TheDictionaryOverload()
	{
		var parser = GeneratedParser.Compile(FileTool, "FileTool");
		var arguments = new Dictionary<string, JsonElement> { ["name"] = JsonDocument.Parse("\"docs\"").RootElement.Clone() };

		var outcome = parser.BindTool("directories__create", arguments);

		Assert.Empty(outcome.Errors);
		Assert.Equal("docs", outcome.GetPath<string>("Directories.Create.Name"));
		Assert.Equal(".", outcome.Get<string>("Root"));
	}

	[Fact]
	public void BindTool_IsOnlyEmittedWithEmitJsonBinderOnTheCommandApp()
	{
		var parser = GeneratedParser.Compile(FileTool.Replace(", EmitJsonBinder = true", "", StringComparison.Ordinal), "FileTool");

		Assert.Throws<InvalidOperationException>(() => parser.BindTool("files__delete", "{}"));
	}

	[Fact]
	public void BindTool_TheGeneratedCodeOfTheTreeHasNoWarnings()
	{
		Assert.Empty(GeneratedParser.GeneratedCodeDiagnostics(FileTool));
	}

	[Fact]
	public void Bind_ALeafWithItsOwnSetting_GetsABindOfItsOwnArgumentsOnly()
	{
		var parser = GeneratedParser.Compile(
			FileTool
				.Replace(", EmitJsonBinder = true", "", StringComparison.Ordinal)
				.Replace("[ParameterCollection]\n[Command(\"delete\"", "[ParameterCollection(EmitJsonBinder = true)]\n[Command(\"delete\"", StringComparison.Ordinal),
			"DeleteFile");

		var outcome = parser.Bind("""{"filePath": "a.txt", "root": "tmp"}""");

		AssertError<UnexpectedArgumentException>(outcome, "root");
		Assert.Equal("a.txt", outcome.Get<string>("FilePath"));
	}
	#endregion

	#region conformance with the command line

	private const string ConformanceMembers = """
		[Option("t", "text", "")] public string Text { get; set; } = "";
		[Option("n", "number", "")] public int Number { get; set; }
		[Option("u", "big", "")] public ulong Big { get; set; }
		[Option("r", "ratio", "")] public double Ratio { get; set; }
		[Option("c", "letter", "")] public char Letter { get; set; }
		[Option("g", "id", "")] public Guid Id { get; set; }
		[Option("a", "address", "")] public Uri Address { get; set; }
		[Option("w", "when", "")] public DateTime When { get; set; }
		[Option("s", "span", "")] public TimeSpan Span { get; set; }
		[Option("m", "mode", "")] public Level Mode { get; set; }
		[Option("p", "point", ""), ParsedWithMethod("TryParse")] public Point Where { get; set; }
		[Option("o", "optional", "")] public int? Optional { get; set; }
		[Option("i", "include", "")] public List<string> Include { get; set; }
		[Flag("v", "verbose", "")] public bool Verbose { get; set; }
		[Flag("l", "level", "")] public Level Level { get; set; }
		[Positional(0, "", true)] public string Input { get; set; } = "";
		[Positional(1, "")] public List<int> Rest { get; set; }
		""";

	public static TheoryData<string[], string> Conformance => new()
	{
		{
			["in.txt", "1", "2", "-t", "hi", "-n", "-5", "-u", "18446744073709551615", "-r", "0.25", "-c", "z", "-g", "0f8fad5b-d9cb-469f-a165-70867728950e",
				"-a", "https://example.com/x", "-w", "2026-10-26T14:30:15+02:00", "-s", "01:30", "-m", "Normal", "-p", "42", "-o", "0",
				"-i", "a", "-i", "b", "-v", "-ll"],
			"""
			{"input": "in.txt", "rest": [1, 2], "text": "hi", "number": -5, "big": 18446744073709551615, "ratio": 0.25, "letter": "z",
				"id": "0f8fad5b-d9cb-469f-a165-70867728950e", "address": "https://example.com/x", "when": "2026-10-26T14:30:15+02:00",
				"span": "01:30", "mode": "Normal", "where": "42", "optional": 0, "include": ["a", "b"], "verbose": true, "level": 2}
			"""
		},
		{ ["only"], """{"input": "only"}""" },
		{ ["x", "-l"], """{"input": "x", "level": "Normal"}""" },
	};

	[Theory]
	[MemberData(nameof(Conformance))]
	public void Bind_TheSameInputAsTheCommandLine_GivesAnEqualObject(string[] args, string json)
	{
		// The documented differences are left out here and tested above: text is always read the invariant way,
		// an enum option takes no numbers, and an empty array replaces an initializer (which the command line cannot express).
		var parser = Compile(ConformanceMembers);

		var parsed = parser.ParseCore(args);
		var bound = parser.Bind(json);

		Assert.Empty(parsed.Errors);
		Assert.Empty(bound.Errors);
		foreach (var property in parsed.Instance.GetType().GetProperties().Where(p => p.Name != "DisplayHelp"))
		{
			Assert.Equal(Describe(property.GetValue(parsed.Instance)), Describe(property.GetValue(bound.Instance)));
		}
	}

	// a value written as text, a list as its elements and a Point as its X, so values of the two objects can be compared
	private static string Describe(object? value) => value switch
	{
		null => "null",
		string text => text,
		System.Collections.IEnumerable list => "[" + string.Join(", ", list.Cast<object?>().Select(Describe)) + "]",
		DateTime date => date.ToString("O", CultureInfo.InvariantCulture) + date.Kind,
		_ when value.GetType().Name == "Point" => "Point " + value.GetType().GetProperty("X")!.GetValue(value),
		IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
		_ => value.ToString() ?? "",
	};
	#endregion
}
