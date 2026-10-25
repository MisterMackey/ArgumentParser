using System.Globalization;
using ArgumentParser.Internal;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ArgumentParser.Tests;

/// <summary>
/// Compiles a tree of commands together with the code generated for it, runs the parser of the root and checks
/// which command is selected, what its parameters hold, and which errors come back.
/// </summary>
public class GeneratedCommandTreeTests
{
	public GeneratedCommandTreeTests()
	{
		// the generated code parses with the current culture
		CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
	}

	private const string CaloriesTree = """
		using ArgumentParser;

		namespace ArgumentParser.Tests;

		[CommandApp("calories", "Track what you eat")]
		public partial class Calories
		{
			[Option("", "db", "Path to the database")]
			public string Db { get; set; } = "default.db";

			[Flag("v", "verbose", "Print extra output")]
			public bool Verbose { get; set; }

			public IngredientGroup? Ingredient { get; private set; }
			public MealGroup? Meal { get; private set; }
			public LogAdd? Log { get; private set; }
			public string? Scratch { get; set; }
		}

		[Command("ingredient", "Manage ingredients")]
		public partial class IngredientGroup
		{
			public IngredientAdd? Add { get; private set; }
			public IngredientUpdate? Update { get; private set; }
		}

		[Command("meal", "Manage meals")]
		public partial class MealGroup
		{
			[Option("d", "day", "Only consider this day")]
			public string Day { get; set; } = "";

			public MealList? List { get; private set; }
		}

		[ParameterCollection]
		[Command("add", "Add an ingredient", Annotations = CommandAnnotations.Idempotent)]
		public partial class IngredientAdd
		{
			[Option("n", "name", "Name of the ingredient", true)]
			public string Name { get; set; } = "";

			[Option("k", "kcal", "Calories per 100 grams")]
			public double Kcal { get; set; }
		}

		[ParameterCollection]
		[Command("update", "Change an ingredient", Annotations = CommandAnnotations.Idempotent | CommandAnnotations.Destructive)]
		public partial class IngredientUpdate
		{
			[Positional(0, "Id of the ingredient", true)]
			public int Id { get; set; }
		}

		[ParameterCollection]
		[Command("list", "List meals", Annotations = CommandAnnotations.ReadOnly)]
		public partial class MealList
		{
			[Flag("a", "all", "Include everything")]
			public bool All { get; set; }
		}

		[ParameterCollection]
		[Command("log", "Log a meal")]
		public partial class LogAdd
		{
			[Positional(0, "Name of the meal", true)]
			public string Meal { get; set; } = "";
		}
		""";

	private static GeneratedParser Calories() => GeneratedParser.Compile(CaloriesTree, "Calories");

	#region selecting a command

	[Fact]
	public void Parse_LeafBelowAGroup_IsSelectedAndFilled()
	{
		var outcome = Calories().Parse("ingredient", "add", "--name", "rice", "--kcal", "130");

		Assert.Empty(outcome.Errors);
		Assert.Equal("rice", outcome.GetPath<string>("Ingredient.Add.Name"));
		Assert.Equal(130d, outcome.GetPath<double>("Ingredient.Add.Kcal"));
	}

	[Fact]
	public void Parse_OnlyTheTypedPathIsPopulated()
	{
		var outcome = Calories().Parse("ingredient", "add", "-n", "rice");

		Assert.NotNull(outcome.GetPath<object>("Ingredient"));
		Assert.NotNull(outcome.GetPath<object>("Ingredient.Add"));
		Assert.Null(outcome.GetPath<object>("Ingredient.Update"));
		Assert.Null(outcome.GetPath<object>("Meal"));
		Assert.Null(outcome.GetPath<object>("Log"));
		Assert.Null(outcome.GetPath<object>("Scratch"));
	}

	[Fact]
	public void Parse_LeafDirectlyBelowTheRoot_IsSelected()
	{
		var outcome = Calories().Parse("log", "lunch");

		Assert.Empty(outcome.Errors);
		Assert.Equal("lunch", outcome.GetPath<string>("Log.Meal"));
		Assert.Null(outcome.GetPath<object>("Ingredient"));
	}

	[Fact]
	public void Parse_PositionalOfALeafBelowAGroup()
	{
		var outcome = Calories().Parse("ingredient", "update", "12");

		Assert.Empty(outcome.Errors);
		Assert.Equal(12, outcome.GetPath<int>("Ingredient.Update.Id"));
	}

	[Fact]
	public void Parse_ReturnsTheRootEvenWhenSomethingIsWrong()
	{
		var outcome = Calories().Parse("nonsense");

		Assert.NotNull(outcome.Instance);
		Assert.Equal("default.db", outcome.Get<string>("Db"));
	}
	#endregion

	#region options of the levels

	[Fact]
	public void Parse_OptionsOfTheRootComeBeforeTheCommandWord()
	{
		var outcome = Calories().Parse("--db", "food.db", "-v", "log", "lunch");

		Assert.Empty(outcome.Errors);
		Assert.Equal("food.db", outcome.Get<string>("Db"));
		Assert.True(outcome.Get<bool>("Verbose"));
		Assert.Equal("lunch", outcome.GetPath<string>("Log.Meal"));
	}

	[Fact]
	public void Parse_OptionsOfAGroupComeBetweenItsWordAndTheNext()
	{
		var outcome = Calories().Parse("--db", "food.db", "meal", "--day", "monday", "list", "--all");

		Assert.Empty(outcome.Errors);
		Assert.Equal("food.db", outcome.Get<string>("Db"));
		Assert.Equal("monday", outcome.GetPath<string>("Meal.Day"));
		Assert.True(outcome.GetPath<bool>("Meal.List.All"));
	}

	[Fact]
	public void Parse_OptionValueThatLooksLikeACommand_IsTheValue()
	{
		var outcome = Calories().Parse("--db", "log", "log", "lunch");

		Assert.Empty(outcome.Errors);
		Assert.Equal("log", outcome.Get<string>("Db"));
		Assert.Equal("lunch", outcome.GetPath<string>("Log.Meal"));
	}

	[Fact]
	public void Parse_OptionOfTheRootAfterTheCommandWord_BelongsToTheLeafAndIsUnknownThere()
	{
		var outcome = Calories().Parse("log", "lunch", "--db", "food.db");

		// the leaf does not know --db, and its value is one positional argument too many
		Assert.Equal(2, outcome.Errors.Count);
		Assert.All(outcome.Errors, error => Assert.IsType<UnexpectedArgumentException>(error));
		Assert.Contains("--db", outcome.Errors[0].Message);
		Assert.Equal("default.db", outcome.Get<string>("Db"));
		Assert.Equal("lunch", outcome.GetPath<string>("Log.Meal"));
	}

	[Fact]
	public void Parse_FlagOfALeafBeforeItsCommandWord_IsUnknownToTheLevel()
	{
		var outcome = Calories().Parse("--all", "meal", "list", "--all");

		var error = Assert.Single(outcome.Errors);
		Assert.IsType<UnexpectedArgumentException>(error);
		Assert.Contains("--all", error.Message);
		// the rest is still parsed
		Assert.True(outcome.GetPath<bool>("Meal.List.All"));
	}

	[Fact]
	public void Parse_SiblingsMayUseTheSameOptionName()
	{
		var parser = GeneratedParser.Compile("""
			using ArgumentParser;
			namespace ArgumentParser.Tests;

			[CommandApp("app")]
			public partial class App
			{
				[Flag("q", "quiet", "root")] public bool Quiet { get; set; }
				public Run? Run { get; private set; }
				public Test? Test { get; private set; }
			}

			[ParameterCollection]
			[Command("run")]
			public partial class Run
			{
				[Flag("v", "verbose", "run")] public bool Verbose { get; set; }
			}

			[ParameterCollection]
			[Command("test")]
			public partial class Test
			{
				[Flag("v", "verbose", "test")] public bool Verbose { get; set; }
			}
			""", "App");

		var run = parser.Parse("-q", "run", "-v");
		var test = parser.Parse("test", "--verbose");

		Assert.Empty(run.Errors);
		Assert.True(run.Get<bool>("Quiet"));
		Assert.True(run.GetPath<bool>("Run.Verbose"));
		Assert.Empty(test.Errors);
		Assert.False(test.Get<bool>("Quiet"));
		Assert.True(test.GetPath<bool>("Test.Verbose"));
	}
	#endregion

	#region errors

	[Fact]
	public void Parse_NoArguments_AsksForACommand()
	{
		var outcome = Calories().Parse();

		var error = Assert.Single(outcome.Errors);
		Assert.IsType<MissingCommandException>(error);
		Assert.Contains("ingredient, meal, log", error.Message);
	}

	[Fact]
	public void Parse_GroupWithoutACommand_AsksForOneOfItsOwn()
	{
		var outcome = Calories().Parse("ingredient");

		var error = Assert.Single(outcome.Errors);
		Assert.IsType<MissingCommandException>(error);
		Assert.Contains("add, update", error.Message);
		Assert.NotNull(outcome.GetPath<object>("Ingredient"));
	}

	[Fact]
	public void Parse_OptionsOnlyAreStillMissingTheCommand()
	{
		var outcome = Calories().Parse("--db", "food.db", "-v");

		Assert.IsType<MissingCommandException>(Assert.Single(outcome.Errors));
		Assert.Equal("food.db", outcome.Get<string>("Db"));
		Assert.True(outcome.Get<bool>("Verbose"));
	}

	[Fact]
	public void Parse_UnknownCommand_NamesItAndTheAlternatives()
	{
		var outcome = Calories().Parse("ingredient", "remove");

		var error = Assert.Single(outcome.Errors);
		Assert.IsType<UnknownCommandException>(error);
		Assert.Contains("'remove'", error.Message);
		Assert.Contains("add, update", error.Message);
		Assert.Null(outcome.GetPath<object>("Ingredient.Add"));
	}

	[Fact]
	public void Parse_CommandNamesAreCaseSensitive()
	{
		var outcome = Calories().Parse("Log", "lunch");

		Assert.IsType<UnknownCommandException>(Assert.Single(outcome.Errors));
	}

	[Fact]
	public void Parse_ErrorsOfEveryLevelAreCollected()
	{
		var outcome = Calories().Parse("--bogus", "meal", "--nonsense", "list", "--worse");

		Assert.Equal(3, outcome.Errors.Count);
		Assert.All(outcome.Errors, error => Assert.IsType<UnexpectedArgumentException>(error));
		Assert.NotNull(outcome.GetPath<object>("Meal.List"));
	}

	[Fact]
	public void ParseCore_MissingRequiredArgumentsOfALeaf_AreReportedBothWays()
	{
		var outcome = Calories().ParseCore("ingredient", "add", "--kcal", "5");

		var missing = Assert.Single(outcome.MissingRequired);
		Assert.IsType<MissingRequiredArgumentException>(missing);
		Assert.Contains(missing, outcome.Errors);
	}

	[Fact]
	public void ParseCore_MissingRequiredArgumentsOfAGroupAndALeaf_AreAllReported()
	{
		var parser = GeneratedParser.Compile("""
			using ArgumentParser;
			namespace ArgumentParser.Tests;

			[CommandApp("app")]
			public partial class App
			{
				[Option("r", "region", "region", true)] public string Region { get; set; } = "";
				public Run? Run { get; private set; }
			}

			[ParameterCollection]
			[Command("run")]
			public partial class Run
			{
				[Option("n", "name", "name", true)] public string Name { get; set; } = "";
			}
			""", "App");

		var outcome = parser.ParseCore("run");

		Assert.Equal(2, outcome.MissingRequired.Count);
		Assert.Equal(2, outcome.Errors.Count);
	}

	[Fact]
	public void ParseCore_OptionWithoutAValue_IsAnErrorButNotAMissingRequiredArgument()
	{
		var outcome = Calories().ParseCore("--db");

		Assert.Contains(outcome.Errors, error => error is MissingRequiredArgumentException);
		Assert.Empty(outcome.MissingRequired);
	}

	[Fact]
	public void Parse_NeverThrowsOrExits_WhateverIsWrong()
	{
		var parser = Calories();

		var outcome = parser.Parse("ingredient", "add");

		Assert.NotEmpty(outcome.Errors);
	}
	#endregion

	#region the Commands table

	private static IReadOnlyList<CommandInfo> Commands() => Calories().GetStaticProperty<IReadOnlyList<CommandInfo>>("Commands")!;

	[Fact]
	public void Commands_HasOneEntryForEveryLeafInTheOrderOfTheTree()
	{
		var commands = Commands();

		Assert.Equal(
			["ingredient add", "ingredient update", "meal list", "log"],
			commands.Select(c => string.Join(" ", c.Path)));
	}

	[Fact]
	public void Commands_CarryDescriptionAndAnnotations()
	{
		var commands = Commands().ToDictionary(c => c.ToString());

		Assert.Equal("Add an ingredient", commands["ingredient add"].Description);
		Assert.Equal(CommandAnnotations.Idempotent, commands["ingredient add"].Annotations);
		Assert.Equal(CommandAnnotations.Idempotent | CommandAnnotations.Destructive, commands["ingredient update"].Annotations);
		Assert.Equal(CommandAnnotations.ReadOnly, commands["meal list"].Annotations);
		Assert.Equal(CommandAnnotations.None, commands["log"].Annotations);
	}

	[Fact]
	public void Commands_ParseTheArgumentsOfTheirOwnCommand()
	{
		var add = Commands().Single(c => c.ToString() == "ingredient add");

		var (result, errors) = add.Parse(["--name", "rice", "--kcal", "130"]);

		Assert.Empty(errors);
		Assert.Equal("IngredientAdd", result.GetType().Name);
		Assert.Equal("rice", result.GetType().GetProperty("Name")!.GetValue(result));
	}

	[Fact]
	public void Commands_ReportErrorsInsteadOfThrowingOrExiting()
	{
		var add = Commands().Single(c => c.ToString() == "ingredient add");

		var (_, errors) = add.Parse([]);

		Assert.IsType<MissingRequiredArgumentException>(Assert.Single(errors));
	}

	[Fact]
	public void Commands_OfTheSmallestTree_HoldsItsOnlyLeaf()
	{
		// a root needs a child, so the smallest tree has one leaf
		var parser = GeneratedParser.Compile("""
			using ArgumentParser;
			namespace ArgumentParser.Tests;

			[CommandApp("app")]
			public partial class App { public Run? Run { get; private set; } }

			[ParameterCollection]
			[Command("run", "Run it")]
			public partial class Run { }
			""", "App");

		var command = Assert.Single(parser.GetStaticProperty<IReadOnlyList<CommandInfo>>("Commands")!);
		Assert.Equal("run", command.ToString());
	}
	#endregion

	#region nested classes, partial classes in several files and other declaration shapes

	[Fact]
	public void NestedCommandClasses_InTheirGroups_AreParsedByTheNestedClass()
	{
		// both leaves are called Add: neither the file name nor the class they end up in may clash
		var parser = GeneratedParser.Compile("""
			using ArgumentParser;
			namespace ArgumentParser.Tests;

			[CommandApp("app")]
			public partial class App
			{
				public Ingredient? Ingredient { get; private set; }
				public Meal? Meal { get; private set; }
			}

			[Command("ingredient")]
			public partial class Ingredient
			{
				public Add? AddCmd { get; private set; }

				[ParameterCollection]
				[Command("add")]
				public partial class Add
				{
					[Option("n", "name", "Name")] public string Name { get; set; } = "";
				}
			}

			[Command("meal")]
			public partial class Meal
			{
				public Add? AddCmd { get; private set; }

				[ParameterCollection]
				[Command("add")]
				public partial class Add
				{
					[Option("d", "day", "Day")] public string Day { get; set; } = "";
				}
			}
			""", "App");

		var ingredient = parser.Parse("ingredient", "add", "--name", "rice");
		var meal = parser.Parse("meal", "add", "--day", "monday");

		Assert.Empty(ingredient.Errors);
		Assert.Equal("rice", ingredient.GetPath<string>("Ingredient.AddCmd.Name"));
		Assert.Empty(meal.Errors);
		Assert.Equal("monday", meal.GetPath<string>("Meal.AddCmd.Day"));
		var commands = parser.GetStaticProperty<IReadOnlyList<CommandInfo>>("Commands")!;
		Assert.Equal(["ingredient add", "meal add"], commands.Select(c => c.ToString()));
	}

	[Fact]
	public void NestedGroupsAndRoots_InStructsRecordsAndClasses_AreGenerated()
	{
		var parser = GeneratedParser.Compile("""
			using ArgumentParser;
			namespace ArgumentParser.Tests;

			public partial struct Holder
			{
				public partial record Wrapper
				{
					[Command("group")]
					public partial class Group
					{
						public Leaf? Leaf { get; private set; }
					}

					[ParameterCollection]
					[Command("leaf")]
					public partial class Leaf
					{
						[Flag("f", "fast", "Fast")] public bool Fast { get; set; }
					}
				}
			}

			[CommandApp("app")]
			public partial class App
			{
				public Holder.Wrapper.Group? Group { get; private set; }
			}
			""", "App");

		var outcome = parser.Parse("group", "leaf", "-f");

		Assert.Empty(outcome.Errors);
		Assert.True(outcome.GetPath<bool>("Group.Leaf.Fast"));
	}

	[Fact]
	public void NestedRoot_IsGenerated()
	{
		var parser = GeneratedParser.Compile("""
			using ArgumentParser;
			namespace ArgumentParser.Tests;

			public partial class Outer
			{
				[CommandApp("app")]
				public partial class App
				{
					[Flag("v", "verbose", "Verbose")] public bool Verbose { get; set; }
					public Run? Run { get; private set; }
				}

				[ParameterCollection]
				[Command("run")]
				public partial class Run { }
			}
			""", "Outer+App");

		var outcome = parser.Parse("-v", "run");

		Assert.Empty(outcome.Errors);
		Assert.True(outcome.Get<bool>("Verbose"));
		Assert.NotNull(outcome.GetPath<object>("Run"));
	}

	[Fact]
	public void OptionsOfAGroupAndTheRoot_AreReadFromEveryPartOfTheClass_AlsoInOtherFiles()
	{
		var parser = GeneratedParser.Compile(
			"""
			using ArgumentParser;
			namespace ArgumentParser.Tests;

			[CommandApp("app")]
			public partial class App
			{
				[Option("a", "first", "In the file with the attribute")] public string First { get; set; } = "";
				public Group? Group { get; private set; }
			}

			[Command("group")]
			public partial class Group
			{
				public Leaf? Leaf { get; private set; }
			}

			[ParameterCollection]
			[Command("leaf")]
			public partial class Leaf { }
			""",
			"App",
			true,
			"""
			using ArgumentParser;
			namespace ArgumentParser.Tests;

			public partial class App
			{
				[Option("b", "second", "In another file")] public string Second { get; set; } = "";
				[Flag("v", "verbose", "Verbose")] public bool Verbose { get; set; }
			}

			public partial class Group
			{
				[Option("g", "gopt", "Option of the group, in another file")] public string GroupOption { get; set; } = "";
			}
			""");

		var outcome = parser.Parse("--first", "1", "--second", "2", "-v", "group", "--gopt", "3", "leaf");

		Assert.Empty(outcome.Errors);
		Assert.Equal("1", outcome.Get<string>("First"));
		Assert.Equal("2", outcome.Get<string>("Second"));
		Assert.True(outcome.Get<bool>("Verbose"));
		Assert.Equal("3", outcome.GetPath<string>("Group.GroupOption"));
	}

	[Fact]
	public void ExplicitInterfaceImplementation_IsNotAChildCommand()
	{
		var parser = GeneratedParser.Compile("""
			using ArgumentParser;
			namespace ArgumentParser.Tests;

			public interface IHasRun { Run? Run { get; set; } }

			[CommandApp("app")]
			public partial class App : IHasRun
			{
				public Run? Real { get; private set; }
				Run? IHasRun.Run { get; set; }
			}

			[ParameterCollection]
			[Command("run")]
			public partial class Run { }
			""", "App");

		var outcome = parser.Parse("run");

		Assert.Empty(outcome.Errors);
		Assert.NotNull(outcome.GetPath<object>("Real"));
	}

	[Fact]
	public void NegativeAnnotations_ProduceCodeThatCompiles()
	{
		var parser = GeneratedParser.Compile("""
			using ArgumentParser;
			namespace ArgumentParser.Tests;

			[CommandApp("app")]
			public partial class App { public Run? Run { get; private set; } }

			[ParameterCollection]
			[Command("run", "Run it", Annotations = (CommandAnnotations)(-1))]
			public partial class Run { }
			""", "App");

		var command = Assert.Single(parser.GetStaticProperty<IReadOnlyList<CommandInfo>>("Commands")!);

		Assert.Equal((CommandAnnotations)(-1), command.Annotations);
	}

	[Fact]
	public void InternalClasses_InATree_Compile()
	{
		// a generated part without an access modifier takes the one the user wrote
		var parser = GeneratedParser.Compile("""
			using ArgumentParser;
			namespace ArgumentParser.Tests;

			[CommandApp("app")]
			internal partial class App { public Group? Group { get; private set; } }

			[Command("group")]
			internal sealed partial class Group { public Outer.Run? Run { get; private set; } }

			internal static partial class Outer
			{
				[ParameterCollection]
				[Command("run")]
				internal partial class Run { [Option("n", "name", "A name")] public string Name { get; set; } = ""; }
			}
			""", "App");

		var outcome = parser.Parse("group", "run", "-n", "x");

		Assert.Empty(outcome.Errors);
		Assert.Equal("x", outcome.GetPath<string>("Group.Run.Name"));
	}

	[Fact]
	public void NamespaceWithASegmentNamedArgumentParser_DoesNotHideTheLibrary()
	{
		// inside ArgumentParser.Tests.ArgumentParser the name ArgumentParser means that namespace, not the library
		var parser = GeneratedParser.Compile("""
			using ArgumentParser;
			namespace ArgumentParser.Tests.ArgumentParser;

			[CommandApp("app")]
			public partial class App
			{
				[Flag("v", "verbose", "Verbose")] public bool Verbose { get; set; }
				public Run? Run { get; private set; }
			}

			[ParameterCollection]
			[Command("run", "Run it")]
			public partial class Run { [Positional(0, "What")] public string What { get; set; } = ""; }
			""", "ArgumentParser.App");

		var outcome = parser.Parse("-v", "run", "it");

		Assert.Empty(outcome.Errors);
		Assert.True(outcome.Get<bool>("Verbose"));
		Assert.Equal("it", outcome.GetPath<string>("Run.What"));
	}

	[Fact]
	public void ClassesNamedAfterKeywords_AreEscaped()
	{
		var parser = GeneratedParser.Compile("""
			using ArgumentParser;
			namespace ArgumentParser.Tests;

			[CommandApp("app")]
			public partial class @class { public @event.@object? Group { get; private set; } }

			public static partial class @event
			{
				[Command("group")]
				public partial class @object { public @int? Leaf { get; private set; } }
			}

			[ParameterCollection]
			[Command("leaf")]
			public partial class @int { [Option("n", "name", "A name")] public string Name { get; set; } = ""; }
			""", "class");

		var outcome = parser.Parse("group", "leaf", "-n", "x");

		Assert.Empty(outcome.Errors);
		Assert.Equal("x", outcome.GetPath<string>("Group.Leaf.Name"));
	}
	#endregion

	#region what is generated

	private static IReadOnlyDictionary<string, string> Generate(string source)
	{
		var compilation = TestHelper.CreateCompilation(source);
		var driver = CSharpGeneratorDriver.Create(new ParserGenerator()).RunGenerators(compilation);
		return driver.GetRunResult().Results.Single().GeneratedSources.ToDictionary(s => s.HintName, s => s.SourceText.ToString());
	}

	[Fact]
	public void Generator_AddsAFilePerRootAndGroupAndNothingForLeaves()
	{
		var files = Generate(CaloriesTree);

		Assert.Contains("ArgumentParser.Tests.Calories_Command.g.cs", files.Keys);
		Assert.Contains("ArgumentParser.Tests.IngredientGroup_Command.g.cs", files.Keys);
		Assert.Contains("ArgumentParser.Tests.MealGroup_Command.g.cs", files.Keys);
		Assert.DoesNotContain(files.Keys, name => name.Contains("IngredientAdd_Command"));
		// the leaves keep the file of their own parser
		Assert.Contains("ArgumentParser.Tests.IngredientAdd_Parser.g.cs", files.Keys);
	}

	[Fact]
	public void Generator_OnlyTheRootHasParseAndCommands()
	{
		var files = Generate(CaloriesTree);

		Assert.Contains("public static (Calories result, List<global::ArgumentParser.ArgumentParserException> errors) Parse(string[] args)", files["ArgumentParser.Tests.Calories_Command.g.cs"]);
		Assert.Contains("Commands", files["ArgumentParser.Tests.Calories_Command.g.cs"]);
		Assert.DoesNotContain("public static", files["ArgumentParser.Tests.IngredientGroup_Command.g.cs"]);
		Assert.DoesNotContain("Commands", files["ArgumentParser.Tests.IngredientGroup_Command.g.cs"]);
		Assert.Contains("ParseCore(string[] args)", files["ArgumentParser.Tests.IngredientGroup_Command.g.cs"]);
	}

	[Fact]
	public void Generator_FileNamesContainTheTypesTheClassIsNestedIn()
	{
		var files = Generate("""
			using ArgumentParser;
			namespace N;

			[CommandApp("app")]
			public partial class App { public Group? Group { get; private set; } }

			[Command("group")]
			public partial class Group
			{
				public Add? Add { get; private set; }

				[ParameterCollection]
				[Command("add")]
				public partial class Add { }
			}
			""");

		Assert.Contains("N.App_Command.g.cs", files.Keys);
		Assert.Contains("N.Group_Command.g.cs", files.Keys);
		Assert.Contains("N.Group.Add_Parser.g.cs", files.Keys);
		Assert.Contains("partial class Group", files["N.Group.Add_Parser.g.cs"]);
	}

	[Fact]
	public void Generator_GeneratesNothingForATreeWithAnError()
	{
		var files = Generate(CaloriesTree.Replace("[Command(\"update\"", "[Command(\"add\""));

		Assert.DoesNotContain(files.Keys, name => name.EndsWith("_Command.g.cs", StringComparison.Ordinal));
	}

	[Fact]
	public void Generator_GeneratesNothingForCommandsWithoutARoot()
	{
		var files = Generate("""
			using ArgumentParser;
			[Command("group")]
			public partial class Group { public Leaf? Leaf { get; private set; } }

			[ParameterCollection]
			[Command("leaf")]
			public partial class Leaf { }
			""");

		Assert.DoesNotContain(files.Keys, name => name.EndsWith("_Command.g.cs", StringComparison.Ordinal));
	}

	[Fact]
	public void Generator_TreeInTheGlobalNamespace_Compiles()
	{
		var parser = GeneratedParser.Compile("""
			using ArgumentParser;

			[CommandApp("app")]
			public partial class App { public Run? Run { get; private set; } }

			[ParameterCollection]
			[Command("run")]
			public partial class Run { }
			""", "App");

		Assert.NotNull(parser);
	}

	[Fact]
	public void Generator_NodesInSeveralNamespaces_Compile()
	{
		var parser = GeneratedParser.Compile("""
			using ArgumentParser;

			namespace ArgumentParser.Tests
			{
				[CommandApp("app")]
				public partial class App { public One.Group? One { get; private set; } public Two.Group? Two { get; private set; } }
			}

			namespace One
			{
				[Command("one")]
				public partial class Group { public Add? Add { get; private set; } }

				[ParameterCollection]
				[Command("add")]
				public partial class Add { [Flag("f", "fast", "go fast")] public bool Fast { get; set; } }
			}

			namespace Two
			{
				[Command("two")]
				public partial class Group { public Add? Add { get; private set; } }

				[ParameterCollection]
				[Command("add")]
				public partial class Add { [Option("n", "name", "a name")] public string Name { get; set; } = ""; }
			}
			""", "App");

		var outcome = parser.Parse("two", "add", "--name", "x");

		Assert.Empty(outcome.Errors);
		Assert.Equal("x", outcome.GetPath<string>("Two.Add.Name"));
		Assert.Null(outcome.GetPath<object>("One"));
	}

	[Fact]
	public void Generator_NamesAndDescriptionsWithQuotesAndBackslashes_AreEscaped()
	{
		// ARG014 only forbids whitespace and a leading dash, so a command word may hold quotes and backslashes too
		var parser = GeneratedParser.Compile("""
			using ArgumentParser;
			namespace ArgumentParser.Tests;

			[CommandApp("app")]
			public partial class App
			{
				[Option("d", "dir", "the \"dir\" \\ here")] public string Dir { get; set; } = "";
				public Group? Group { get; private set; }
			}

			[Command("g\"r\\p", "A \"group\"")]
			public partial class Group { public Run? Run { get; private set; } }

			[ParameterCollection]
			[Command("r\\un\"", "Runs \"it\" \\ now")]
			public partial class Run { }
			""", "App");

		var command = Assert.Single(parser.GetStaticProperty<IReadOnlyList<CommandInfo>>("Commands")!);
		var outcome = parser.Parse("-d", "x", "g\"r\\p", "r\\un\"");

		Assert.Equal("Runs \"it\" \\ now", command.Description);
		Assert.Equal(["g\"r\\p", "r\\un\""], command.Path);
		Assert.Empty(outcome.Errors);
		Assert.NotNull(outcome.GetPath<object>("Group.Run"));
	}
	#endregion
}
