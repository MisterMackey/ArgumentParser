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
		// the assertions format the parsed values with the current culture; tests about cultures set their own
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
		var time = result.Get<DateTime>("Time");
		Assert.Equal(new DateTime(2025, 5, 5, 12, 0, 0, DateTimeKind.Utc), time);
		Assert.Equal(DateTimeKind.Utc, time.Kind);
	}

	[Theory]
	[InlineData("double", "1,5")]
	[InlineData("float", "1,5")]
	[InlineData("decimal", "1,5")]
	[InlineData("double", "1,000")]
	[InlineData("int", "1,000")]
	[InlineData("long", "1.000")]
	[InlineData("DateTime", "01/02/2026")]
	[InlineData("DateTime", "26-10-2026")]
	[InlineData("TimeSpan", "1:30:15,5")]
	public void Option_CultureDependentText_IsRejectedWhateverTheCurrentCulture(string type, string input)
	{
		CultureInfo.CurrentCulture = new CultureInfo("nl-NL");
		var parser = GeneratedParser.Compile(ParserSource.Build($$"""
			[Option("v", "Value", "a value")]
			public {{type}} Value { get; set; }
			"""));

		var result = parser.Parse("--Value", input);

		Assert.IsType<InvalidArgumentValueException>(Assert.Single(result.Errors));
	}

	[Fact]
	public void Arguments_InvariantText_IsReadTheSameWhateverTheCurrentCulture()
	{
		CultureInfo.CurrentCulture = new CultureInfo("nl-NL");
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("a", "Amount", "an amount")]
			public double Amount { get; set; }
			[Option("d", "Day", "a day")]
			public DateTime Day { get; set; }
			[Positional(0, "a price")]
			public decimal Price { get; set; }
			"""));

		var result = parser.Parse("--Amount", "1.5", "--Day", "2026-01-02", "1.25");

		Assert.Empty(result.Errors);
		Assert.Equal(1.5, result.Get<double>("Amount"));
		Assert.Equal(new DateTime(2026, 1, 2), result.Get<DateTime>("Day"));
		Assert.Equal(1.25m, result.Get<decimal>("Price"));
	}

	[Fact]
	public void Arguments_UseCurrentCulture_ReadsTheTextWithTheCurrentCulture()
	{
		CultureInfo.CurrentCulture = new CultureInfo("nl-NL");
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("a", "Amount", "an amount")]
			public double Amount { get; set; }
			[Option("d", "Day", "a day")]
			public DateTime Day { get; set; }
			[Positional(0, "a price")]
			public decimal Price { get; set; }
			""", useCurrentCulture: true));

		var result = parser.Parse("--Amount", "1,5", "--Day", "02-01-2026", "1.000,25");

		Assert.Empty(result.Errors);
		Assert.Equal(1.5, result.Get<double>("Amount"));
		Assert.Equal(new DateTime(2026, 1, 2), result.Get<DateTime>("Day"));
		Assert.Equal(1000.25m, result.Get<decimal>("Price"));
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

	private const string LevelParseMethodMembers = """
		[Option("l", "Level", "level"), ParsedWithMethod("ReadLevel")]
		public Level Level { get; set; }
		[Option("s", "levels", "levels"), ParsedWithMethod("ReadLevel")]
		public List<Level>? Levels { get; set; }
		[Positional(0, "level")]
		[ParsedWithMethod("ReadLevel")]
		public Level First { get; set; }

		private static bool ReadLevel(string input, out Level value)
		{
			value = input switch { "lo" => Level.Low, "hi" => Level.High, _ => Level.None };
			return input is "lo" or "hi";
		}
		""";

	[Fact]
	public void Enum_WithParseMethod_TheMethodWinsOverTheNamesOfTheMembers()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(LevelParseMethodMembers, extraTypes: EnumType));

		var result = parser.Parse("hi", "--Level", "lo", "-s", "hi", "-s", "lo");

		Assert.Empty(result.Errors);
		Assert.Equal("Low", result.Get<object>("Level")!.ToString());
		Assert.Equal("High", result.Get<object>("First")!.ToString());
		Assert.Equal(["High", "Low"], Values(result, "Levels"));
	}

	[Fact]
	public void Enum_WithParseMethod_ANameTheMethodRejectsIsAnError()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(LevelParseMethodMembers, extraTypes: EnumType));

		// High is a member of the enum, but the method only knows hi and lo
		var result = parser.Parse("lo", "--Level", "High", "-s", "2");

		Assert.Equal(2, result.Errors.Count);
		Assert.All(result.Errors, e => Assert.IsType<InvalidArgumentValueException>(e));
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

	#region nullable properties

	private const string NullableMembers = """
		[Option("n", "Number", "a number")]
		public int? Number { get; set; }
		[Option("d", "Date", "a date")]
		public DateTime? Date { get; set; }
		[Option("g", "Id", "an id")]
		public Guid? Id { get; set; }
		[Option("c", "Colour", "a colour")]
		public Colour? Colour { get; set; }
		[Option("b", "Switch", "a boolean")]
		public bool? Switch { get; set; }
		[Option("u", "Link", "a link")]
		public Uri? Link { get; set; }
		[Option("s", "Text", "a text")]
		public string? Text { get; set; }
		[Option("k", "Kept", "has a default")]
		public int? Kept { get; set; } = 5;
		[Positional(0, "a positional number")]
		public double? Ratio { get; set; }
		[Flag("v", "Verbose", "a flag")]
		public bool? Verbose { get; set; }
		[Flag("l", "Level", "a level")]
		public Colour? Level { get; set; }
		""";

	private const string ColourType = "public enum Colour { Red, Green, Blue }";

	[Fact]
	public void Nullable_ArgumentsThatAreNotGiven_AreNullOrKeepTheirDefault()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(NullableMembers, extraTypes: ColourType));

		var result = parser.Parse();

		Assert.Empty(result.Errors);
		foreach (var name in new[] { "Number", "Date", "Id", "Colour", "Switch", "Link", "Text", "Ratio", "Verbose", "Level" })
		{
			Assert.Null(result.Get<object>(name));
		}
		Assert.Equal(5, result.Get<int?>("Kept"));
	}

	[Fact]
	public void Nullable_ArgumentsThatAreGiven_HaveTheirValue()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(NullableMembers, extraTypes: ColourType));
		var id = Guid.NewGuid();

		var result = parser.Parse("-n", "-7", "-d", "2026-10-26", "-g", id.ToString(), "-c", "Green", "-b", "false", "-u", "http://x.test/", "-s", "text", "-k", "0", "2.5", "-vl");

		Assert.Empty(result.Errors);
		Assert.Equal(-7, result.Get<int?>("Number"));
		Assert.Equal(new DateTime(2026, 10, 26), result.Get<DateTime?>("Date"));
		Assert.Equal(id, result.Get<Guid?>("Id"));
		Assert.Equal("Green", result.Get<object>("Colour")!.ToString());
		Assert.Equal(false, result.Get<bool?>("Switch"));
		Assert.Equal(new Uri("http://x.test/"), result.Get<Uri>("Link"));
		Assert.Equal("text", result.Get<string>("Text"));
		Assert.Equal(0, result.Get<int?>("Kept"));
		Assert.Equal(2.5, result.Get<double?>("Ratio"));
		Assert.Equal(true, result.Get<bool?>("Verbose"));
		Assert.Equal("Green", result.Get<object>("Level")!.ToString());
	}

	[Fact]
	public void Nullable_GeneratedCode_HasNoWarnings()
	{
		var diagnostics = GeneratedParser.GeneratedCodeDiagnostics(ParserSource.Build(NullableMembers, extraTypes: ColourType));

		Assert.Empty(diagnostics);
	}

	[Fact]
	public void Nullable_FlagLevel_IsTheNumberOfTimesTheFlagIsGiven()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(NullableMembers, extraTypes: ColourType));

		var result = parser.Parse("-ll");

		Assert.Equal("Blue", result.Get<object>("Level")!.ToString());
	}

	[Fact]
	public void Nullable_InvalidValue_IsAnErrorAndLeavesTheDefault()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(NullableMembers, extraTypes: ColourType));

		var result = parser.Parse("-n", "1,5", "-k", "many", "-d", "26/10/2026");

		Assert.Equal(3, result.Errors.Count);
		Assert.All(result.Errors, e => Assert.IsType<InvalidArgumentValueException>(e));
		Assert.Null(result.Get<int?>("Number"));
		Assert.Equal(5, result.Get<int?>("Kept"));
		Assert.Null(result.Get<DateTime?>("Date"));
	}

	[Fact]
	public void Nullable_RequiredOption_IsStillRequired()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("n", "Number", "a number", required: true)]
			public int? Number { get; set; }
			"""));

		var missing = parser.Parse();
		var given = parser.Parse("-n", "3");

		Assert.IsType<MissingRequiredArgumentException>(Assert.Single(missing.Errors));
		Assert.Empty(given.Errors);
		Assert.Equal(3, given.Get<int?>("Number"));
	}

	private const string DescriptorMembers = """
		[Option("r", "redirectOutputStream", "File descriptor to send output to, NULL for none", required: true)]
		[ParsedWithMethod("ParseDescriptor")]
		public int? RedirectOutputStream { get; set; } = 1;

		private static bool ParseDescriptor(string input, out int? value)
		{
			if (input == "NULL")
			{
				value = null;
				return true;
			}
			var parsed = int.TryParse(input, out var descriptor);
			value = parsed ? descriptor : null;
			return parsed;
		}
		""";

	[Theory]
	[InlineData("NULL", null)]
	[InlineData("2", 2)]
	public void ParsedWithMethod_OnTheDeclaringClass_CanAnswerNullForANullableProperty(string argument, int? expected)
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(DescriptorMembers));

		var result = parser.Parse("--redirectOutputStream", argument);

		Assert.Empty(result.Errors);
		Assert.Equal(expected, result.Get<int?>("RedirectOutputStream"));
	}

	[Fact]
	public void ParsedWithMethod_OnTheDeclaringClass_KeepsTheDefaultWhenTheOptionIsMissingAndReportsBadInput()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(DescriptorMembers.Replace(", required: true", "")));

		var missing = parser.Parse();
		var bad = parser.Parse("--redirectOutputStream", "stdout");

		Assert.Empty(missing.Errors);
		Assert.Equal(1, missing.Get<int?>("RedirectOutputStream"));
		Assert.IsType<InvalidArgumentValueException>(Assert.Single(bad.Errors));
		Assert.Equal(1, bad.Get<int?>("RedirectOutputStream"));
	}

	[Fact]
	public void ParsedWithMethod_OnBothTheDeclaringClassAndThePropertyType_UsesTheDeclaringClass()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("p", "Point", "a point")]
			[ParsedWithMethod("TryParse")]
			public Point? Point { get; set; }

			private static bool TryParse(string input, out Point? result)
			{
				result = new Point { Name = "declaring class: " + input };
				return true;
			}
			""", extraTypes: """
			public class Point
			{
				public string Name { get; set; } = "";
				public static bool TryParse(string input, out Point? result)
				{
					result = new Point { Name = "property type: " + input };
					return true;
				}
			}
			"""));

		var result = parser.Parse("--Point", "1");

		var point = result.Get<object>("Point")!;
		Assert.Equal("declaring class: 1", (string)point.GetType().GetProperty("Name")!.GetValue(point)!);
	}

	[Fact]
	public void Nullable_OptionsOfAGroupAndALeafInATree_AreParsedLikeThoseOfAClassOnItsOwn()
	{
		var parser = GeneratedParser.Compile("""
			using ArgumentParser;

			namespace ArgumentParser.Tests;

			[CommandApp("app", "An app", HelpTextGeneration.GenerateTextOnly, behaviourOnError: BehaviourOnError.ThrowNever)]
			public partial class TestClass
			{
				[Option("r", "retries", "how often")]
				public int? Retries { get; set; }

				public Run? Run { get; private set; }
			}

			[ParameterCollection]
			[Command("run", "Runs")]
			public partial class Run
			{
				[Option("t", "timeout", "seconds")]
				public double? Timeout { get; set; }
			}
			""");

		var set = parser.ParseCommand("-r", "3", "run", "-t", "1.5");
		var unset = parser.ParseCommand("run");

		Assert.Empty(set.Errors);
		Assert.Equal(3, set.GetPath<int?>("Retries"));
		Assert.Equal(1.5, set.GetPath<double?>("Run.Timeout"));
		Assert.Empty(unset.Errors);
		Assert.Null(unset.GetPath<int?>("Retries"));
		Assert.Null(unset.GetPath<double?>("Run.Timeout"));
	}

	#endregion

	#region list parameters

	private const string ListOptionMembers = """
		[Option("i", "include", "patterns to include")]
		public List<string>? Include { get; set; }
		[Option("n", "number", "numbers")]
		public List<int>? Numbers { get; set; }
		[Option("l", "level", "levels")]
		public List<Level>? Levels { get; set; }
		[Option("o", "out", "a single value")]
		public string Out { get; set; } = "";
		[Flag("v", "verbose", "be verbose")]
		public bool Verbose { get; set; }
		""";

	private static List<string>? Values(ParseOutcome outcome, string path)
		=> outcome.GetPath<System.Collections.IEnumerable>(path)?.Cast<object>().Select(v => v.ToString()!).ToList();

	private const string PointListType = """
		public class Point
		{
			public int X { get; set; }
			public int Y { get; set; }
			public override string ToString() => $"{X}/{Y}";
			public static bool TryParse(string input, out Point result)
			{
				result = new Point();
				var parts = input.Split(',');
				if (parts.Length != 2 || !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y))
				{
					return false;
				}
				result = new Point { X = x, Y = y };
				return true;
			}
		}
		""";

	[Fact]
	public void ListOption_RepeatedUnderBothNames_CollectsTheValuesInOrder()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(ListOptionMembers, extraTypes: EnumType));

		var outcome = parser.Parse("--include", "a", "-i", "b", "--include", "c");

		Assert.Empty(outcome.Errors);
		Assert.Equal(["a", "b", "c"], Values(outcome, "Include"));
	}

	[Fact]
	public void ListOption_GivenOnce_HasOneElement()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(ListOptionMembers, extraTypes: EnumType));

		var outcome = parser.Parse("-i", "only");

		Assert.Empty(outcome.Errors);
		Assert.Equal(["only"], Values(outcome, "Include"));
	}

	[Fact]
	public void ListOption_ValuesAreNotSplitOnCommas()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(ListOptionMembers, extraTypes: EnumType));

		var outcome = parser.Parse("-i", "a,b", "-i", "c d");

		Assert.Equal(["a,b", "c d"], Values(outcome, "Include"));
	}

	[Fact]
	public void ListOption_ValueThatLooksLikeAnOption_IsTheValue()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(ListOptionMembers, extraTypes: EnumType));

		var outcome = parser.Parse("-i", "-value", "-i", "--other");

		Assert.Empty(outcome.Errors);
		Assert.Equal(["-value", "--other"], Values(outcome, "Include"));
	}

	[Fact]
	public void ListOption_NotGiven_IsNullBecauseNobodyAssignedIt()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(ListOptionMembers, extraTypes: EnumType));

		var outcome = parser.Parse("--out", "x");

		Assert.Empty(outcome.Errors);
		Assert.Null(outcome.Get<object>("Include"));
		Assert.Null(outcome.Get<object>("Numbers"));
		Assert.Null(outcome.Get<object>("Levels"));
	}

	[Fact]
	public void ListOption_NotGiven_KeepsTheDefaultOfTheUser()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("i", "include", "patterns")]
			public List<string> Include { get; set; } = new() { "default" };
			[Option("e", "exclude", "patterns")]
			public List<string> Exclude { get; set; } = new();
			"""));

		var outcome = parser.Parse();

		Assert.Equal(["default"], Values(outcome, "Include"));
		Assert.Empty(Values(outcome, "Exclude")!);
	}

	[Fact]
	public void ListOption_Given_ReplacesTheDefaultOfTheUser()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("i", "include", "patterns")]
			public List<string> Include { get; set; } = new() { "default" };
			"""));

		var outcome = parser.Parse("-i", "a", "-i", "b");

		Assert.Equal(["a", "b"], Values(outcome, "Include"));
	}

	[Fact]
	public void ListOption_InAnotherCallToParse_DoesNotKeepTheValuesOfTheFirst()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(ListOptionMembers, extraTypes: EnumType));

		var first = parser.Parse("-i", "a", "-i", "b");
		var second = parser.Parse("-i", "c");

		Assert.Equal(["a", "b"], Values(first, "Include"));
		Assert.Equal(["c"], Values(second, "Include"));
	}

	[Fact]
	public void ListOptions_AreCollectedSeparatelyFromEachOtherAndFromOtherArguments()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(ListOptionMembers, extraTypes: EnumType));

		var outcome = parser.Parse("-n", "1", "-i", "a", "-v", "-l", "Low", "--out", "x", "-n", "2", "-l", "High", "-i", "b");

		Assert.Empty(outcome.Errors);
		Assert.Equal(["a", "b"], Values(outcome, "Include"));
		Assert.Equal(["1", "2"], Values(outcome, "Numbers"));
		Assert.Equal(["Low", "High"], Values(outcome, "Levels"));
		Assert.Equal("x", outcome.Get<string>("Out"));
		Assert.True(outcome.Get<bool>("Verbose"));
	}

	[Theory]
	[InlineData("int", "1", "-2", "1,-2")]
	[InlineData("long", "9000000000", "1", "9000000000,1")]
	[InlineData("double", "1.5", "2.25", "1.5,2.25")]
	[InlineData("decimal", "3.25", "4", "3.25,4")]
	[InlineData("bool", "true", "false", "True,False")]
	[InlineData("char", "x", "y", "x,y")]
	[InlineData("string", "a b", "c", "a b,c")]
	[InlineData("Guid", "d3b07384-d9a0-4c9f-a1b1-2b8f7c8e9a11", "00000000-0000-0000-0000-000000000000", "d3b07384-d9a0-4c9f-a1b1-2b8f7c8e9a11,00000000-0000-0000-0000-000000000000")]
	[InlineData("Uri", "https://example.com/a", "https://example.com/b", "https://example.com/a,https://example.com/b")]
	[InlineData("TimeSpan", "01:02:03", "00:00:04", "01:02:03,00:00:04")]
	[InlineData("DateTime", "2024-01-02", "2024-03-04", "01/02/2024 00:00:00,03/04/2024 00:00:00")]
	[InlineData("Level", "Low", "High", "Low,High")]
	public void ListOption_ParsesEverySupportedElementType(string type, string first, string second, string expected)
	{
		var parser = GeneratedParser.Compile(ParserSource.Build($$"""
			[Option("v", "Value", "values")]
			public List<{{type}}>? Value { get; set; }
			""", extraTypes: EnumType));

		var outcome = parser.Parse("-v", first, "--Value", second);

		Assert.Empty(outcome.Errors);
		Assert.Equal(expected, string.Join(",", Values(outcome, "Value")!));
	}

	[Theory]
	[InlineData("int", "1", "2", "1,2")]
	[InlineData("double", "1.5", "2.25", "1.5,2.25")]
	[InlineData("string", "a b", "c", "a b,c")]
	[InlineData("Level", "Low", "High", "Low,High")]
	public void ListPositional_ParsesTheSupportedElementTypes(string type, string first, string second, string expected)
	{
		var parser = GeneratedParser.Compile(ParserSource.Build($$"""
			[Positional(0, "values")]
			public List<{{type}}>? Value { get; set; }
			""", extraTypes: EnumType));

		var outcome = parser.Parse(first, second);

		Assert.Empty(outcome.Errors);
		Assert.Equal(expected, string.Join(",", Values(outcome, "Value")!));
	}

	[Fact]
	public void ListOption_ElementThatCannotBeParsed_IsAnErrorAndTheOthersAreKept()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(ListOptionMembers, extraTypes: EnumType));

		var outcome = parser.Parse("-n", "1", "-n", "x", "-n", "3", "-l", "Nope", "-l", "High");

		Assert.Equal(["1", "3"], Values(outcome, "Numbers"));
		Assert.Equal(["High"], Values(outcome, "Levels"));
		Assert.Equal(2, outcome.Errors.Count);
		Assert.All(outcome.Errors, e => Assert.IsType<InvalidArgumentValueException>(e));
		Assert.Contains("x", outcome.Errors[0].Message);
	}

	[Fact]
	public void ListOption_OnlyInvalidElements_LeavesThePropertyAsItWas()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(ListOptionMembers, extraTypes: EnumType));

		var outcome = parser.Parse("-n", "x");

		Assert.Single(outcome.Errors);
		Assert.Null(outcome.Get<object>("Numbers"));
	}

	[Fact]
	public void ListOption_Ambiguous_NumbersAreRejectedPerElementLikeAScalar()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("d", "double", "doubles")]
			public List<double>? Doubles { get; set; }
			"""));

		var outcome = parser.Parse("-d", "1.5", "-d", "1,5", "-d", "1,000");

		Assert.Equal(["1.5"], Values(outcome, "Doubles"));
		Assert.Equal(2, outcome.Errors.Count);
	}

	[Fact]
	public void ListOption_WithUseCurrentCulture_ReadsEveryElementWithTheCurrentCulture()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("d", "double", "doubles")]
			public List<double>? Doubles { get; set; }
			""", useCurrentCulture: true));
		CultureInfo.CurrentCulture = new CultureInfo("nl-NL");

		var outcome = parser.Parse("-d", "1,5", "-d", "2,25");

		Assert.Empty(outcome.Errors);
		// the values are formatted with the same culture
		Assert.Equal(["1,5", "2,25"], Values(outcome, "Doubles"));
	}

	[Fact]
	public void ListOption_MissingValueAtTheEnd_IsAnErrorAndKeepsTheEarlierValues()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(ListOptionMembers, extraTypes: EnumType));

		var outcome = parser.Parse("-i", "a", "-i");

		Assert.IsType<MissingRequiredArgumentException>(Assert.Single(outcome.Errors));
		Assert.Equal(["a"], Values(outcome, "Include"));
	}

	[Fact]
	public void ListOption_Required_IsMissingWithoutValuesAndSatisfiedByOne()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("i", "include", "patterns", true)]
			public List<string>? Include { get; set; }
			"""));

		var without = parser.ParseCore();
		var withOne = parser.ParseCore("-i", "a");
		var withTwo = parser.ParseCore("-i", "a", "--include", "b");

		Assert.IsType<MissingRequiredArgumentException>(Assert.Single(without.MissingRequired));
		Assert.Empty(withOne.Errors);
		Assert.Empty(withTwo.Errors);
		Assert.Equal(["a", "b"], Values(withTwo, "Include"));
	}

	[Fact]
	public void ListOption_RequiredAndOnlyInvalidElements_IsStillSatisfied()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("n", "number", "numbers", true)]
			public List<int>? Numbers { get; set; }
			"""));

		var outcome = parser.ParseCore("-n", "x");

		Assert.Empty(outcome.MissingRequired);
		Assert.IsType<InvalidArgumentValueException>(Assert.Single(outcome.Errors));
	}

	[Fact]
	public void ListPositional_LastPositional_CollectsEveryRemainingValue()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Positional(0, "the command")]
			public string Command { get; set; } = "";
			[Positional(1, "the files")]
			public List<string>? Files { get; set; }
			"""));

		var outcome = parser.Parse("copy", "a", "b", "c");

		Assert.Empty(outcome.Errors);
		Assert.Equal("copy", outcome.Get<string>("Command"));
		Assert.Equal(["a", "b", "c"], Values(outcome, "Files"));
	}

	[Fact]
	public void ListPositional_OnlyPositional_CollectsEveryValue()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Positional(0, "the files")]
			public List<string>? Files { get; set; }
			"""));

		var outcome = parser.Parse("a", "b", "c");

		Assert.Empty(outcome.Errors);
		Assert.Equal(["a", "b", "c"], Values(outcome, "Files"));
	}

	[Fact]
	public void ListPositional_GivenOnce_HasOneElement()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Positional(0, "the command")]
			public string Command { get; set; } = "";
			[Positional(1, "the files")]
			public List<string>? Files { get; set; }
			"""));

		var outcome = parser.Parse("copy", "a");

		Assert.Equal(["a"], Values(outcome, "Files"));
	}

	[Fact]
	public void ListPositional_NoValues_IsNullOrKeepsTheDefault()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Positional(0, "the command")]
			public string Command { get; set; } = "";
			[Positional(1, "the files")]
			public List<string>? Files { get; set; }
			[Option("e", "extra", "extra files")]
			public List<string> Extra { get; set; } = new() { "default" };
			"""));

		var outcome = parser.Parse("copy");

		Assert.Empty(outcome.Errors);
		Assert.Null(outcome.Get<object>("Files"));
		Assert.Equal(["default"], Values(outcome, "Extra"));
	}

	[Fact]
	public void ListPositional_Given_ReplacesTheDefaultOfTheUser()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Positional(0, "the files")]
			public List<string> Files { get; set; } = new() { "default" };
			"""));

		var outcome = parser.Parse("a", "b");

		Assert.Equal(["a", "b"], Values(outcome, "Files"));
	}

	[Fact]
	public void ListPositional_OptionsAndFlagsBetweenTheValues_DoNotEndTheList()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Positional(0, "the command")]
			public string Command { get; set; } = "";
			[Positional(1, "the files")]
			public List<string>? Files { get; set; }
			[Option("o", "out", "a single value")]
			public string Out { get; set; } = "";
			[Flag("v", "verbose", "be verbose")]
			public bool Verbose { get; set; }
			"""));

		var outcome = parser.Parse("copy", "a", "-v", "b", "--out", "x", "c");

		Assert.Empty(outcome.Errors);
		Assert.Equal("copy", outcome.Get<string>("Command"));
		Assert.Equal(["a", "b", "c"], Values(outcome, "Files"));
		Assert.Equal("x", outcome.Get<string>("Out"));
		Assert.True(outcome.Get<bool>("Verbose"));
	}

	[Fact]
	public void ListPositional_ElementThatCannotBeParsed_IsAnErrorAndTheOthersAreKept()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Positional(0, "the numbers")]
			public List<int>? Numbers { get; set; }
			"""));

		var outcome = parser.Parse("1", "x", "3");

		Assert.Equal(["1", "3"], Values(outcome, "Numbers"));
		Assert.IsType<InvalidArgumentValueException>(Assert.Single(outcome.Errors));
	}

	[Fact]
	public void ListPositional_Required_NeedsAtLeastOneValue()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Positional(0, "the files", true)]
			public List<string>? Files { get; set; }
			"""));

		var without = parser.ParseCore();
		var withOne = parser.ParseCore("a");
		var withThree = parser.ParseCore("a", "b", "c");

		Assert.IsType<MissingRequiredArgumentException>(Assert.Single(without.MissingRequired));
		Assert.Empty(withOne.Errors);
		Assert.Empty(withThree.Errors);
		Assert.Equal(["a", "b", "c"], Values(withThree, "Files"));
	}

	[Fact]
	public void ListPositional_ScalarPositionalsBeforeTheList_StillTakeOneValueEach()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Positional(0, "source")]
			public string Source { get; set; } = "";
			[Positional(1, "count")]
			public int Count { get; set; }
			[Positional(2, "the rest")]
			public List<string>? Rest { get; set; }
			"""));

		var outcome = parser.Parse("src", "3", "x", "y");

		Assert.Empty(outcome.Errors);
		Assert.Equal("src", outcome.Get<string>("Source"));
		Assert.Equal(3, outcome.Get<int>("Count"));
		Assert.Equal(["x", "y"], Values(outcome, "Rest"));
	}

	[Fact]
	public void ScalarPositionals_ExtraValue_IsStillAnUnexpectedArgument()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Positional(0, "source")]
			public string Source { get; set; } = "";
			[Positional(1, "target")]
			public string Target { get; set; } = "";
			"""));

		var outcome = parser.Parse("a", "b", "c");

		Assert.IsType<UnexpectedArgumentException>(Assert.Single(outcome.Errors));
	}

	[Fact]
	public void ListOptionAndListPositional_InOneClass_AreCollectedSeparately()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("t", "tag", "tags")]
			public List<string>? Tags { get; set; }
			[Positional(0, "the files")]
			public List<string>? Files { get; set; }
			"""));

		var outcome = parser.Parse("a", "-t", "x", "b", "--tag", "y", "c");

		Assert.Empty(outcome.Errors);
		Assert.Equal(["x", "y"], Values(outcome, "Tags"));
		Assert.Equal(["a", "b", "c"], Values(outcome, "Files"));
	}

	[Fact]
	public void ParsedWithMethod_OnAListOption_ParsesEveryElementWithTheMethodOfTheElementType()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("p", "point", "points")]
			[ParsedWithMethod("TryParse")]
			public List<Point>? Points { get; set; }
			""", extraTypes: PointListType));

		var outcome = parser.Parse("-p", "1,2", "--point", "3,4", "-p", "five", "-p", "5,6");

		Assert.Equal(["1/2", "3/4", "5/6"], Values(outcome, "Points"));
		var error = Assert.IsType<InvalidArgumentValueException>(Assert.Single(outcome.Errors));
		Assert.Contains("five", error.Message);
	}

	[Fact]
	public void ParsedWithMethod_OnAListPositional_ParsesEveryElementWithTheMethodOfTheElementType()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Positional(0, "points")]
			[ParsedWithMethod("TryParse")]
			public List<Point>? Points { get; set; }
			""", extraTypes: PointListType));

		var outcome = parser.Parse("1,2", "3,4", "bad", "5,6");

		Assert.Equal(["1/2", "3/4", "5/6"], Values(outcome, "Points"));
		Assert.IsType<InvalidArgumentValueException>(Assert.Single(outcome.Errors));
	}

	[Fact]
	public void ParsedWithMethod_OnTheDeclaringClassOfAList_ParsesEveryElement()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("x", "hex", "hex numbers")]
			[ParsedWithMethod("ParseHex")]
			public List<int>? Hex { get; set; }
			[Positional(0, "more hex numbers")]
			[ParsedWithMethod("ParseHex")]
			public List<int>? MoreHex { get; set; }
			private static bool ParseHex(string input, out int value)
				=> int.TryParse(input, System.Globalization.NumberStyles.HexNumber, null, out value);
			"""));

		var outcome = parser.Parse("-x", "ff", "--hex", "10", "a", "1f");

		Assert.Empty(outcome.Errors);
		Assert.Equal(["255", "16"], Values(outcome, "Hex"));
		Assert.Equal(["10", "31"], Values(outcome, "MoreHex"));
	}

	[Fact]
	public void ListArguments_GeneratedCode_HasNoWarnings()
	{
		var diagnostics = GeneratedParser.GeneratedCodeDiagnostics(ParserSource.Build("""
			[Option("i", "include", "patterns")]
			public System.Collections.Generic.List<string>? Include { get; set; }
			[Option("n", "number", "numbers", true)]
			public System.Collections.Generic.List<int> Numbers { get; set; } = new();
			[Option("l", "level", "levels")]
			public System.Collections.Generic.List<Level>? Levels { get; set; }
			[Positional(0, "the files")]
			public System.Collections.Generic.List<string>? Files { get; set; }
			""", extraTypes: EnumType));

		Assert.Empty(diagnostics);
	}

	[Fact]
	public void ListArguments_InATree_AreCollectedOnEveryLevel()
	{
		var parser = GeneratedParser.Compile("""
			using ArgumentParser;

			namespace ArgumentParser.Tests;

			[CommandApp("app", "An app", HelpTextGeneration.GenerateTextOnly, behaviourOnError: BehaviourOnError.ThrowNever)]
			public partial class TestClass
			{
				[Option("i", "include", "patterns")]
				public List<string>? Include { get; set; }

				public Files? Files { get; private set; }
			}

			[Command("files", "Handles files")]
			public partial class Files
			{
				[Option("x", "exclude", "patterns")]
				public List<string>? Exclude { get; set; }

				public Copy? Copy { get; private set; }
			}

			[ParameterCollection]
			[Command("copy", "Copies")]
			public partial class Copy
			{
				[Option("t", "tag", "tags")]
				public List<int>? Tags { get; set; }

				[Positional(0, "target")]
				public string Target { get; set; } = "";

				[Positional(1, "the files")]
				public List<string>? Sources { get; set; }
			}
			""");

		var outcome = parser.ParseCommand("-i", "a", "--include", "b", "files", "-x", "c", "-x", "d", "copy", "-t", "1", "dest", "f1", "--tag", "2", "f2", "f3");

		Assert.Empty(outcome.Errors);
		Assert.Equal(["a", "b"], Values(outcome, "Include"));
		Assert.Equal(["c", "d"], Values(outcome, "Files.Exclude"));
		Assert.Equal(["1", "2"], Values(outcome, "Files.Copy.Tags"));
		Assert.Equal("dest", outcome.GetPath<string>("Files.Copy.Target"));
		Assert.Equal(["f1", "f2", "f3"], Values(outcome, "Files.Copy.Sources"));
	}

	[Fact]
	public void ListArguments_InATreeThatAreNotGiven_AreNull()
	{
		var parser = GeneratedParser.Compile("""
			using ArgumentParser;

			namespace ArgumentParser.Tests;

			[CommandApp("app", "An app", HelpTextGeneration.GenerateTextOnly, behaviourOnError: BehaviourOnError.ThrowNever)]
			public partial class TestClass
			{
				[Option("i", "include", "patterns")]
				public List<string>? Include { get; set; }

				public Run? Run { get; private set; }
			}

			[ParameterCollection]
			[Command("run", "Runs")]
			public partial class Run
			{
				[Positional(0, "the files")]
				public List<string>? Files { get; set; }
			}
			""");

		var outcome = parser.ParseCommand("run");

		Assert.Empty(outcome.Errors);
		Assert.Null(outcome.GetPath<object>("Include"));
		Assert.Null(outcome.GetPath<object>("Run.Files"));
	}

	[Fact]
	public void ListPositional_OfALeafThatHasItsOwnParse_IsCollectedWithoutATree()
	{
		var parser = GeneratedParser.Compile("""
			using ArgumentParser;

			namespace ArgumentParser.Tests;

			[CommandApp("app", "An app", HelpTextGeneration.GenerateTextOnly, behaviourOnError: BehaviourOnError.ThrowNever)]
			public partial class Root
			{
				public TestClass? Run { get; private set; }
			}

			[ParameterCollection(HelpTextGeneration.GenerateTextOnly, "h", "Help", BehaviourOnError.ThrowNever)]
			[Command("run", "Runs")]
			public partial class TestClass
			{
				[Positional(0, "the files")]
				public List<string>? Files { get; set; }
			}
			""");

		var alone = parser.Parse("a", "b");

		Assert.Empty(alone.Errors);
		Assert.Equal(["a", "b"], Values(alone, "Files"));
	}

	[Fact]
	public void ListArguments_HelpText_SaysThatTheArgumentCanBeRepeated()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("i", "include", "patterns to include")]
			public List<string>? Include { get; set; }
			[Option("o", "out", "a single value")]
			public string Out { get; set; } = "";
			[Positional(0, "the files")]
			public List<string>? Files { get; set; }
			"""));

		var lines = parser.GetStaticMember<string>("HelpText")!.Split('\n');

		Assert.Contains("repeated", lines.Single(l => l.Contains("patterns to include")));
		Assert.Contains("repeated", lines.Single(l => l.Contains("the files")));
		Assert.DoesNotContain("repeated", lines.Single(l => l.Contains("a single value")));
	}

	[Fact]
	public void ListArguments_CommandHelpText_SaysThatTheArgumentCanBeRepeated()
	{
		var parser = GeneratedParser.Compile("""
			using ArgumentParser;

			namespace ArgumentParser.Tests;

			[CommandApp("app", "An app", HelpTextGeneration.GenerateTextOnly, behaviourOnError: BehaviourOnError.ThrowNever)]
			public partial class TestClass
			{
				public Run? Run { get; private set; }
			}

			[ParameterCollection]
			[Command("run", "Runs")]
			public partial class Run
			{
				[Option("t", "tag", "the tags")]
				public List<string>? Tags { get; set; }
				[Option("o", "out", "a single value")]
				public string Out { get; set; } = "";
				[Positional(0, "the files")]
				public List<string>? Files { get; set; }
			}
			""", className: "Run");

		var lines = parser.GetStaticMember<string>("CommandHelpText")!.Split('\n');

		Assert.Contains("repeated", lines.Single(l => l.Contains("the tags")));
		Assert.Contains("repeated", lines.Single(l => l.Contains("the files")));
		Assert.DoesNotContain("repeated", lines.Single(l => l.Contains("a single value")));
	}

	#endregion

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

	[Fact]
	public void HelpArgument_CustomNamesGivenAsNamedArguments_AreTheOnesRecognised()
	{
		var parser = GeneratedParser.Compile("""
			using System;
			using ArgumentParser;

			namespace ArgumentParser.Tests;

			[ParameterCollection(helpArgumentLongName: "Assist", helpArgumentShortName: "?", helpTextGeneration: HelpTextGeneration.GenerateArgumentOnly, behaviourOnError: BehaviourOnError.ThrowNever)]
			public partial class TestClass
			{
				[Flag("v", "Verbose", "be loud")]
				public bool Verbose { get; set; }
			}
			""");

		Assert.True(parser.Parse("-?").Get<bool>("DisplayHelp"));
		Assert.True(parser.Parse("--Assist").Get<bool>("DisplayHelp"));
		Assert.False(parser.Parse("-v").Get<bool>("DisplayHelp"));
		Assert.NotEmpty(parser.Parse("--Help").Errors);
	}

	[Fact]
	public void HelpText_RequiredOptionsShowWhicheverNamesTheyHave()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build("""
			[Option("", "Output", "long only", required: true)]
			public string LongOnly { get; set; } = "";
			[Option("s", "", "short only", required: true)]
			public string ShortOnly { get; set; } = "";
			[Option("b", "Both", "both names", required: true)]
			public string Both { get; set; } = "";
			"""));

		var helpText = parser.GetStaticMember<string>("HelpText");

		Assert.Contains("\tOption: --Output : long only", helpText);
		Assert.Contains("\tOption: -s : short only", helpText);
		Assert.Contains("\tOption: -b | --Both : both names", helpText);
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
	public void BehaviourOnError_ThrowIfMissingRequired_DoesNotThrowForAnOptionWithoutValue()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(RequiredOptionAndNumber, behaviourOnError: "ThrowIfMissingRequired"));

		// the tokenizer reports the dangling -n as a MissingRequiredArgumentException, but -o was given,
		// so no required argument is missing and nothing may be thrown
		var result = parser.Parse("-o", "x", "-n");

		Assert.IsType<MissingRequiredArgumentException>(Assert.Single(result.Errors));
	}

	// Parse with these settings calls Environment.Exit, which would kill the test host. ParseCore must be safe.
	private static GeneratedParser CompileExitingParser()
	{
		return GeneratedParser.Compile(ParserSource.Build(
			RequiredOptionAndNumber,
			behaviourOnError: "DisplayHelpAndExit",
			helpTextGeneration: "GenerateAll"));
	}

	[Fact]
	public void ParseCore_ReturnsErrorsWithoutExiting()
	{
		var parser = CompileExitingParser();

		var result = parser.ParseCore("-n", "abc");

		Assert.Equal(2, result.Errors.Count);
		Assert.Single(result.Errors.OfType<InvalidArgumentValueException>());
		Assert.Single(result.Errors.OfType<MissingRequiredArgumentException>());
	}

	[Fact]
	public void ParseCore_ReportsAHelpRequestWithoutExiting()
	{
		var parser = CompileExitingParser();

		var result = parser.ParseCore("-o", "x", "--Help");

		Assert.True(result.Get<bool>("DisplayHelp"));
		Assert.Empty(result.Errors);
	}

	[Fact]
	public void ParseCore_DoesNotThrowWhenParseWould()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(RequiredOptionAndNumber, behaviourOnError: "ThrowIfAnyError"));

		Assert.Throws<AggregateException>(() => parser.Parse("-n", "abc"));
		var result = parser.ParseCore("-n", "abc");

		Assert.Equal(2, result.Errors.Count);
	}

	[Fact]
	public void ParseCore_ReportsMissingRequiredSeparately()
	{
		var parser = CompileExitingParser();

		var missing = parser.ParseCore("-n", "1");
		var dangling = parser.ParseCore("-o", "x", "-n");

		Assert.IsType<MissingRequiredArgumentException>(Assert.Single(missing.MissingRequired));
		Assert.Equal(missing.Errors, missing.MissingRequired);
		// the tokenizer's "option requires a value" error is not a missing required argument
		Assert.IsType<MissingRequiredArgumentException>(Assert.Single(dangling.Errors));
		Assert.Empty(dangling.MissingRequired);
	}

	[Fact]
	public void ParseCore_FillsTheInstanceLikeParse()
	{
		var parser = GeneratedParser.Compile(ParserSource.Build(RequiredOptionAndNumber));

		var core = parser.ParseCore("-o", "x", "-n", "5");
		var parse = parser.Parse("-o", "x", "-n", "5");

		Assert.Equal(parse.Get<string>("Out"), core.Get<string>("Out"));
		Assert.Equal(parse.Get<int>("Number"), core.Get<int>("Number"));
		Assert.Empty(core.Errors);
	}

	[Fact]
	public void Generated_Code_CompilesWithoutImplicitUsings()
	{
		GeneratedParser.Compile(ParserSource.Build("""
			[Option("o", "Out", "out")]
			public string Out { get; set; } = "";
			"""), implicitUsings: false);
	}

	[Fact]
	public void Generated_Code_CompilesForAClassInTheGlobalNamespace()
	{
		var parser = GeneratedParser.Compile("""
			using ArgumentParser;

			[ParameterCollection(HelpTextGeneration.GenerateTextOnly, "h", "Help", BehaviourOnError.ThrowNever)]
			public partial class GlobalClass
			{
				[Option("o", "Out", "out")]
				public string Out { get; set; } = "";
			}
			""", className: "GlobalClass");

		Assert.DoesNotContain("<global namespace>", parser.GeneratedSource, StringComparison.Ordinal);
		Assert.DoesNotContain("namespace ", parser.GeneratedSource, StringComparison.Ordinal);
	}

	[Fact]
	public void ClassInSeveralFiles_ParsesTheArgumentsOfEveryPart()
	{
		var parser = GeneratedParser.Compile(
			ParserSource.Build("""
				[Option("o", "Output", "in the file with the attribute")]
				public string Output { get; set; } = "";
				[Positional(1, "second")]
				public string Second { get; set; } = "";
				"""),
			"TestClass",
			true,
			"""
			using ArgumentParser;

			namespace ArgumentParser.Tests;

			public partial class TestClass
			{
				[Flag("v", "Verbose", "in another file")]
				public bool Verbose { get; set; }
				[Positional(0, "first")]
				public string First { get; set; } = "";
			}
			""");

		var outcome = parser.Parse("one", "-v", "--Output", "out", "two");

		Assert.Empty(outcome.Errors);
		Assert.Equal("out", outcome.Get<string>("Output"));
		Assert.True(outcome.Get<bool>("Verbose"));
		Assert.Equal("one", outcome.Get<string>("First"));
		Assert.Equal("two", outcome.Get<string>("Second"));
	}
}
