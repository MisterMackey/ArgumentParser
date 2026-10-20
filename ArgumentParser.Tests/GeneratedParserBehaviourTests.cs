using System.Globalization;

namespace ArgumentParser.Tests;

/// <summary>
/// Compiles the generated parser together with a small test class, runs Parse and checks what comes back.
/// Unlike the other generator tests these do not look at the generated text.
/// </summary>
public class GeneratedParserBehaviourTests
{
	public GeneratedParserBehaviourTests()
	{
		// the generated code parses with the current culture
		CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
	}

	private const string EnumType = "public enum Level { None = 0, Low = 1, High = 2 }";

	[Theory]
	[InlineData("int", "42", "42")]
	[InlineData("double", "2.5", "2.5")]
	[InlineData("float", "1.5", "1.5")]
	[InlineData("long", "-9000000000", "-9000000000")]
	[InlineData("short", "-12", "-12")]
	[InlineData("decimal", "3.25", "3.25")]
	[InlineData("byte", "200", "200")]
	[InlineData("sbyte", "-5", "-5")]
	[InlineData("char", "x", "x")]
	[InlineData("uint", "4000000000", "4000000000")]
	[InlineData("ulong", "18000000000000000000", "18000000000000000000")]
	[InlineData("ushort", "65000", "65000")]
	[InlineData("bool", "true", "True")]
	[InlineData("string", "some text", "some text")]
	[InlineData("Guid", "d3b07384-d9a0-4c9f-a1b1-2b8f7c8e9a11", "d3b07384-d9a0-4c9f-a1b1-2b8f7c8e9a11")]
	[InlineData("Uri", "https://example.com/a", "https://example.com/a")]
	[InlineData("TimeSpan", "01:02:03", "01:02:03")]
	public void Option_ParsesEverySupportedType(string type, string input, string expected)
	{
		var parser = GeneratedParser.Compile(ParserSource.Build($$"""
			[Option("v", "Value", "a value")]
			public {{type}} Value { get; set; } = default!;
			"""));

		var viaLongName = parser.Parse("--Value", input);
		var viaShortName = parser.Parse("-v", input);

		Assert.Empty(viaLongName.Errors);
		Assert.Equal(expected, viaLongName.Get<object>("Value")!.ToString());
		Assert.Empty(viaShortName.Errors);
		Assert.Equal(expected, viaShortName.Get<object>("Value")!.ToString());
	}

	[Fact]
	public void Names_ShortNameOfOneArgumentEqualToLongNameOfAnother_AreKeptApart()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("v", "Value", "first")]
			public string First { get; set; } = "";
			[Option("x", "v", "second")]
			public string Second { get; set; } = "";
			"""));

		var viaShort = parser.Parse("-v", "one");
		var viaLong = parser.Parse("--v", "two");

		Assert.Empty(viaShort.Errors);
		Assert.Equal("one", viaShort.Get<string>("First"));
		Assert.Equal("", viaShort.Get<string>("Second"));
		Assert.Empty(viaLong.Errors);
		Assert.Equal("", viaLong.Get<string>("First"));
		Assert.Equal("two", viaLong.Get<string>("Second"));
	}

	[Fact]
	public void Option_ParsesDateTime()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("t", "Time", "a timestamp")]
			public DateTime Time { get; set; }
			"""));

		var result = parser.Parse("--Time", "2025-05-05T12:00:00Z");

		Assert.Empty(result.Errors);
		Assert.Equal(DateTime.Parse("2025-05-05T12:00:00Z"), result.Get<DateTime>("Time"));
	}

	[Theory]
	[InlineData("int", "abc")]
	[InlineData("int", "1.5")]
	[InlineData("int", "")]
	[InlineData("double", "abc")]
	[InlineData("float", "abc")]
	[InlineData("long", "abc")]
	[InlineData("short", "40000")]
	[InlineData("decimal", "abc")]
	[InlineData("byte", "300")]
	[InlineData("sbyte", "200")]
	[InlineData("char", "ab")]
	[InlineData("uint", "-1")]
	[InlineData("ulong", "-1")]
	[InlineData("ushort", "70000")]
	[InlineData("bool", "maybe")]
	[InlineData("Guid", "not-a-guid")]
	[InlineData("TimeSpan", "not-a-timespan")]
	[InlineData("DateTime", "not-a-date")]
	public void Option_InvalidValue_ReportsErrorAndLeavesDefault(string type, string input)
	{
		var parser = GeneratedParser.Compile(ParserSource.Build($$"""
			[Option("v", "Value", "a value")]
			public {{type}} Value { get; set; }
			"""));

		var result = parser.Parse("--Value", input);

		var error = Assert.Single(result.Errors);
		Assert.IsType<InvalidArgumentValueException>(error);
		Assert.Contains("Value", error.Message);
		Assert.Equal(Activator.CreateInstance(Type.GetType(ResolveKeyword(type))!), result.Get<object>("Value"));
	}

	private static string ResolveKeyword(string keyword) => keyword switch
	{
		"int" => "System.Int32",
		"double" => "System.Double",
		"float" => "System.Single",
		"long" => "System.Int64",
		"short" => "System.Int16",
		"decimal" => "System.Decimal",
		"byte" => "System.Byte",
		"sbyte" => "System.SByte",
		"char" => "System.Char",
		"uint" => "System.UInt32",
		"ulong" => "System.UInt64",
		"ushort" => "System.UInt16",
		"bool" => "System.Boolean",
		"Guid" => "System.Guid",
		"TimeSpan" => "System.TimeSpan",
		"DateTime" => "System.DateTime",
		_ => throw new ArgumentOutOfRangeException(nameof(keyword), keyword, null)
	};

	[Fact]
	public void Option_AcceptsValueThatLooksLikeAFlag()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("c", "Count", "a number")]
			public int Count { get; set; }
			[Option("o", "Out", "a string")]
			public string Out { get; set; } = "";
			"""));

		var result = parser.Parse("--Count", "-5", "-o", "-v");

		Assert.Empty(result.Errors);
		Assert.Equal(-5, result.Get<int>("Count"));
		Assert.Equal("-v", result.Get<string>("Out"));
	}

	[Fact]
	public void Option_MissingValue_ReportsError()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("o", "Out", "a string")]
			public string Out { get; set; } = "";
			"""));

		var result = parser.Parse("--Out");

		Assert.Single(result.Errors);
	}

	[Fact]
	public void Option_NamedDifferentlyFromProperty_SetsProperty()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option(longName: "Target", description: "where to write")]
			public string Destination { get; set; } = "";
			"""));

		var result = parser.Parse("--Target", "here");

		Assert.Empty(result.Errors);
		Assert.Equal("here", result.Get<string>("Destination"));
	}

	[Fact]
	public void Flag_Bool_IsFalseByDefaultAndTrueWhenGiven()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Flag("v", "Verbose", "be loud")]
			public bool Verbose { get; set; }
			"""));

		Assert.False(parser.Parse().Get<bool>("Verbose"));
		Assert.True(parser.Parse("-v").Get<bool>("Verbose"));
		Assert.True(parser.Parse("--Verbose").Get<bool>("Verbose"));
		Assert.True(parser.Parse("-vvv").Get<bool>("Verbose"));
	}

	[Theory]
	[InlineData("Low", "-l")]
	[InlineData("High", "-ll")]
	[InlineData("High", "-l", "-l")]
	[InlineData("High", "-l", "--Level")]
	public void Flag_Enum_UsesRepeatCountAsLevel(string expected, params string[] args)
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Flag("l", "Level", "level")]
			public Level Level { get; set; }
			""", extraTypes: EnumType));

		var result = parser.Parse(args);

		Assert.Empty(result.Errors);
		Assert.Equal(expected, result.Get<object>("Level")!.ToString());
	}

	[Fact]
	public void Flag_Enum_NotGiven_StaysDefault()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Flag("l", "Level", "level")]
			public Level Level { get; set; }
			""", extraTypes: EnumType));

		Assert.Equal("None", parser.Parse().Get<object>("Level")!.ToString());
	}

	[Fact]
	public void Flags_CanBeClusteredWithTrailingOption()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Flag("a", "Alpha", "a")]
			public bool Alpha { get; set; }
			[Flag("b", "Beta", "b")]
			public bool Beta { get; set; }
			[Option("o", "Out", "out")]
			public string Out { get; set; } = "";
			"""));

		var result = parser.Parse("-abo", "file");

		Assert.Empty(result.Errors);
		Assert.True(result.Get<bool>("Alpha"));
		Assert.True(result.Get<bool>("Beta"));
		Assert.Equal("file", result.Get<string>("Out"));
	}

	[Theory]
	[InlineData("Low", "Low")]
	[InlineData("High", "High")]
	[InlineData("2", "High")]
	public void Option_Enum_ParsesNameOrNumber(string input, string expected)
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("l", "Level", "level")]
			public Level Level { get; set; }
			""", extraTypes: EnumType));

		var result = parser.Parse("--Level", input);

		Assert.Empty(result.Errors);
		Assert.Equal(expected, result.Get<object>("Level")!.ToString());
	}

	[Fact]
	public void Option_Enum_UnknownName_ReportsError()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("l", "Level", "level")]
			public Level Level { get; set; }
			""", extraTypes: EnumType));

		var result = parser.Parse("--Level", "Medium");

		Assert.IsType<InvalidArgumentValueException>(Assert.Single(result.Errors));
	}

	[Fact]
	public void Positionals_AreAssignedInOrderWhereverTheyAppear()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Positional(0, "first")]
			public string First { get; set; } = "";
			[Positional(1, "second")]
			public int Second { get; set; }
			[Flag("v", "Verbose", "be loud")]
			public bool Verbose { get; set; }
			[Option("o", "Out", "out")]
			public string Out { get; set; } = "";
			"""));

		foreach (var args in new[]
		{
			new[] { "alpha", "7", "-v", "--Out", "x" },
			new[] { "-v", "--Out", "x", "alpha", "7" },
			new[] { "alpha", "--Out", "x", "-v", "7" },
		})
		{
			var result = parser.Parse(args);

			Assert.Empty(result.Errors);
			Assert.Equal("alpha", result.Get<string>("First"));
			Assert.Equal(7, result.Get<int>("Second"));
			Assert.True(result.Get<bool>("Verbose"));
			Assert.Equal("x", result.Get<string>("Out"));
		}
	}

	[Fact]
	public void Positional_InvalidValue_ReportsError()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Positional(0, "count")]
			public int Count { get; set; }
			"""));

		var result = parser.Parse("many");

		Assert.IsType<InvalidArgumentValueException>(Assert.Single(result.Errors));
	}

	[Fact]
	public void UnknownAndSurplusArguments_AreReported()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Positional(0, "first")]
			public string First { get; set; } = "";
			"""));

		var result = parser.Parse("one", "two", "--nope", "-x");

		Assert.Equal(3, result.Errors.Count);
		Assert.All(result.Errors, e => Assert.IsType<UnexpectedArgumentException>(e));
		Assert.Equal("one", result.Get<string>("First"));
	}

	[Fact]
	public void RequiredOption_Missing_ReportsMissingRequired()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("o", "Out", "out", required: true)]
			public string Out { get; set; } = "";
			"""));

		var result = parser.Parse();

		var error = Assert.Single(result.Errors);
		Assert.IsType<MissingRequiredArgumentException>(error);
		Assert.Contains("Out", error.Message);
	}

	[Fact]
	public void RequiredOption_Given_ReportsNothing()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("o", "Out", "out", required: true)]
			public string Out { get; set; } = "";
			"""));

		Assert.Empty(parser.Parse("-o", "x").Errors);
		Assert.Empty(parser.Parse("--Out", "x").Errors);
	}

	[Fact]
	public void RequiredPositional_Missing_ReportsMissingRequired()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Positional(0, "input", true)]
			public string Input { get; set; } = "";
			"""));

		var result = parser.Parse();

		var error = Assert.Single(result.Errors);
		Assert.IsType<MissingRequiredArgumentException>(error);
		Assert.Contains("Input", error.Message);
	}

	[Fact]
	public void Parse_CalledTwice_DoesNotRememberRequiredArgumentsFromTheFirstCall()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("o", "Out", "out", required: true)]
			public string Out { get; set; } = "";
			[Positional(0, "input", true)]
			public string Input { get; set; } = "";
			"""));

		Assert.Empty(parser.Parse("-o", "x", "in").Errors);
		var second = parser.Parse();

		Assert.Equal(2, second.Errors.Count);
		Assert.All(second.Errors, e => Assert.IsType<MissingRequiredArgumentException>(e));
	}

	[Fact]
	public void Parse_CalledTwice_InstancesAreIndependent()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Flag("v", "Verbose", "be loud")]
			public bool Verbose { get; set; }
			[Option("o", "Out", "out")]
			public string Out { get; set; } = "";
			"""));

		var first = parser.Parse("-v", "-o", "x");
		var second = parser.Parse();

		Assert.True(first.Get<bool>("Verbose"));
		Assert.Equal("x", first.Get<string>("Out"));
		Assert.False(second.Get<bool>("Verbose"));
		Assert.Equal("", second.Get<string>("Out"));
	}

	[Fact]
	public void ParsedWithMethod_UsesTheNamedMethod()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("p", "Point", "a point")]
			[ParsedWithMethod("TryParse")]
			public Point Point { get; set; } = new();
			""", extraTypes: """
			public class Point
			{
				public int X { get; set; }
				public int Y { get; set; }
				public static bool TryParse(string input, out Point result)
				{
					result = new Point();
					var parts = input.Split(',');
					return parts.Length == 2
						&& int.TryParse(parts[0], out var x)
						&& int.TryParse(parts[1], out var y)
						&& ((result = new Point { X = x, Y = y }) is not null);
				}
			}
			"""));

		var good = parser.Parse("--Point", "3,4");
		var bad = parser.Parse("--Point", "three");

		Assert.Empty(good.Errors);
		var point = good.Get<object>("Point")!;
		Assert.Equal(3, (int)point.GetType().GetProperty("X")!.GetValue(point)!);
		Assert.Equal(4, (int)point.GetType().GetProperty("Y")!.GetValue(point)!);
		Assert.IsType<InvalidArgumentValueException>(Assert.Single(bad.Errors));
	}

	[Fact]
	public void MultipleParameterCollections_InOneCompilation_AreParsedIndependently()
	{
		var parser = GeneratedParser.Compile("""
			using ArgumentParser;
			namespace ArgumentParser.Tests;

			[ParameterCollection(HelpTextGeneration.GenerateTextOnly, "h", "Help", BehaviourOnError.ThrowNever)]
			public partial class First
			{
				[Option("a", "Alpha", "alpha", true)]
				public string Alpha { get; set; } = "";
			}

			[ParameterCollection(HelpTextGeneration.GenerateTextOnly, "h", "Help", BehaviourOnError.ThrowNever)]
			public partial class Second
			{
				[Option("b", "Beta", "beta")]
				public int Beta { get; set; }
			}
			""", className: "Second");

		var result = parser.Parse("--Beta", "9");

		Assert.Empty(result.Errors);
		Assert.Equal(9, result.Get<int>("Beta"));
	}

	[Fact]
	public void Description_WithQuotesAndBackslashes_StillProducesCompilableParser()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("o", "Out", "write to \"C:\\temp\" (or elsewhere)")]
			public string Out { get; set; } = "";
			"""));

		var result = parser.Parse("-o", "x");

		Assert.Empty(result.Errors);
		Assert.Contains("C:", parser.GetStaticMember<string>("HelpText"));
	}

	[Fact]
	public void Description_WithTripleQuotes_StillProducesCompilableParser()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("o", "Out", "say \"\"\"hi\"\"\" or '''")]
			public string Out { get; set; } = "";
			"""));

		Assert.Empty(parser.Parse("-o", "x").Errors);
		Assert.Contains("\"\"\"hi\"\"\"", parser.GetStaticMember<string>("HelpText"));
	}

	[Fact]
	public void HelpText_ListsEveryArgument()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("o", "Out", "where to write", required: true)]
			public string Out { get; set; } = "";
			[Positional(0, "the input file")]
			public string Input { get; set; } = "";
			[Flag("v", "Verbose", "be loud")]
			public bool Verbose { get; set; }
			"""));

		var helpText = parser.GetStaticMember<string>("HelpText");

		Assert.NotNull(helpText);
		Assert.Contains("--Out", helpText);
		Assert.Contains("where to write", helpText);
		Assert.Contains("the input file", helpText);
		Assert.Contains("--Verbose", helpText);
		Assert.Contains("be loud", helpText);
	}

	[Fact]
	public void HelpArgumentOnly_SetsDisplayHelpWithoutExiting()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Flag("v", "Verbose", "be loud")]
			public bool Verbose { get; set; }
			""", helpTextGeneration: "GenerateArgumentOnly"));

		Assert.False(parser.Parse().Get<bool>("DisplayHelp"));
		Assert.True(parser.Parse("-h").Get<bool>("DisplayHelp"));
		Assert.True(parser.Parse("--Help").Get<bool>("DisplayHelp"));
	}

	private const string RequiredOptionAndNumber = """
		[Option("o", "Out", "out", required: true)]
		public string Out { get; set; } = "";
		[Option("n", "Number", "number")]
		public int Number { get; set; }
		""";

	[Fact]
	public void BehaviourOnError_ThrowIfAnyError_ThrowsForMissingRequired()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(RequiredOptionAndNumber, behaviourOnError: "ThrowIfAnyError"));

		var ex = Assert.Throws<AggregateException>(() => parser.Parse("-n", "1"));

		Assert.IsType<MissingRequiredArgumentException>(Assert.Single(ex.InnerExceptions));
	}

	[Fact]
	public void BehaviourOnError_ThrowIfAnyError_ThrowsForInvalidValue()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(RequiredOptionAndNumber, behaviourOnError: "ThrowIfAnyError"));

		var ex = Assert.Throws<AggregateException>(() => parser.Parse("-o", "x", "-n", "abc"));

		Assert.IsType<InvalidArgumentValueException>(Assert.Single(ex.InnerExceptions));
	}

	[Fact]
	public void BehaviourOnError_ThrowIfAnyError_ReturnsNormallyWithoutErrors()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(RequiredOptionAndNumber, behaviourOnError: "ThrowIfAnyError"));

		var result = parser.Parse("-o", "x", "-n", "5");

		Assert.Empty(result.Errors);
		Assert.Equal(5, result.Get<int>("Number"));
	}

	[Fact]
	public void BehaviourOnError_ThrowIfAnyError_ThrowsAggregateOfAllErrors()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(RequiredOptionAndNumber, behaviourOnError: "ThrowIfAnyError"));

		var ex = Assert.Throws<AggregateException>(() => parser.Parse("-n", "abc"));

		Assert.Equal(2, ex.InnerExceptions.Count);
		Assert.Single(ex.InnerExceptions.OfType<InvalidArgumentValueException>());
		Assert.Single(ex.InnerExceptions.OfType<MissingRequiredArgumentException>());
	}

	[Fact]
	public void BehaviourOnError_ThrowIfMissingRequired_ThrowsForMissingRequired()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(RequiredOptionAndNumber, behaviourOnError: "ThrowIfMissingRequired"));

		var ex = Assert.Throws<AggregateException>(() => parser.Parse("-n", "1"));

		Assert.IsType<MissingRequiredArgumentException>(Assert.Single(ex.InnerExceptions));
	}

	[Fact]
	public void BehaviourOnError_ThrowIfMissingRequired_DoesNotThrowForInvalidValue()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(RequiredOptionAndNumber, behaviourOnError: "ThrowIfMissingRequired"));

		var result = parser.Parse("-o", "x", "-n", "abc");

		Assert.IsType<InvalidArgumentValueException>(Assert.Single(result.Errors));
	}

	[Fact]
	public void Generated_Code_CompilesWithoutImplicitUsings()
	{
		GeneratedParser.Compile(ParserSource.Build("""
			[Option("o", "Out", "out")]
			public string Out { get; set; } = "";
			"""), implicitUsings: false);
	}
}
