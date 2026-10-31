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
		// the assertions format the parsed values with the current culture; tests about cultures set their own
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

		[ParameterCollection(Annotations = CommandAnnotations.Idempotent)]
		[Command("add", "Add an ingredient")]
		public partial class IngredientAdd
		{
			[Option("n", "name", "Name of the ingredient", true)]
			public string Name { get; set; } = "";

			[Option("k", "kcal", "Calories per 100 grams")]
			public double Kcal { get; set; }
		}

		[ParameterCollection(Annotations = CommandAnnotations.Idempotent | CommandAnnotations.Destructive)]
		[Command("update", "Change an ingredient")]
		public partial class IngredientUpdate
		{
			[Positional(0, "Id of the ingredient", true)]
			public int Id { get; set; }
		}

		[ParameterCollection(Annotations = CommandAnnotations.ReadOnly)]
		[Command("list", "List meals")]
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
		var outcome = Calories().ParseCommand("ingredient", "add", "--name", "rice", "--kcal", "130");

		Assert.Empty(outcome.Errors);
		Assert.Equal("rice", outcome.GetPath<string>("Ingredient.Add.Name"));
		Assert.Equal(130d, outcome.GetPath<double>("Ingredient.Add.Kcal"));
	}

	[Fact]
	public void Parse_OnlyTheTypedPathIsPopulated()
	{
		var outcome = Calories().ParseCommand("ingredient", "add", "-n", "rice");

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
		var outcome = Calories().ParseCommand("log", "lunch");

		Assert.Empty(outcome.Errors);
		Assert.Equal("lunch", outcome.GetPath<string>("Log.Meal"));
		Assert.Null(outcome.GetPath<object>("Ingredient"));
	}

	[Fact]
	public void Parse_PositionalOfALeafBelowAGroup()
	{
		var outcome = Calories().ParseCommand("ingredient", "update", "12");

		Assert.Empty(outcome.Errors);
		Assert.Equal(12, outcome.GetPath<int>("Ingredient.Update.Id"));
	}

	[Fact]
	public void Parse_ReturnsTheRootEvenWhenSomethingIsWrong()
	{
		var outcome = Calories().ParseCommand("nonsense");

		Assert.NotNull(outcome.Instance);
		Assert.Equal("default.db", outcome.Get<string>("Db"));
	}
	#endregion

	#region options of the levels

	[Fact]
	public void Parse_OptionsOfTheRootComeBeforeTheCommandWord()
	{
		var outcome = Calories().ParseCommand("--db", "food.db", "-v", "log", "lunch");

		Assert.Empty(outcome.Errors);
		Assert.Equal("food.db", outcome.Get<string>("Db"));
		Assert.True(outcome.Get<bool>("Verbose"));
		Assert.Equal("lunch", outcome.GetPath<string>("Log.Meal"));
	}

	[Fact]
	public void Parse_OptionsOfAGroupComeBetweenItsWordAndTheNext()
	{
		var outcome = Calories().ParseCommand("--db", "food.db", "meal", "--day", "monday", "list", "--all");

		Assert.Empty(outcome.Errors);
		Assert.Equal("food.db", outcome.Get<string>("Db"));
		Assert.Equal("monday", outcome.GetPath<string>("Meal.Day"));
		Assert.True(outcome.GetPath<bool>("Meal.List.All"));
	}

	[Fact]
	public void Parse_OptionValueThatLooksLikeACommand_IsTheValue()
	{
		var outcome = Calories().ParseCommand("--db", "log", "log", "lunch");

		Assert.Empty(outcome.Errors);
		Assert.Equal("log", outcome.Get<string>("Db"));
		Assert.Equal("lunch", outcome.GetPath<string>("Log.Meal"));
	}

	[Fact]
	public void Parse_OptionOfTheRootAfterTheCommandWord_BelongsToTheLeafAndIsUnknownThere()
	{
		var outcome = Calories().ParseCommand("log", "lunch", "--db", "food.db");

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
		var outcome = Calories().ParseCommand("--all", "meal", "list", "--all");

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

		var run = parser.ParseCommand("-q", "run", "-v");
		var test = parser.ParseCommand("test", "--verbose");

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
		var outcome = Calories().ParseCommand();

		var error = Assert.Single(outcome.Errors);
		Assert.IsType<MissingCommandException>(error);
		Assert.Contains("ingredient, meal, log", error.Message);
	}

	[Fact]
	public void Parse_GroupWithoutACommand_AsksForOneOfItsOwn()
	{
		var outcome = Calories().ParseCommand("ingredient");

		var error = Assert.Single(outcome.Errors);
		Assert.IsType<MissingCommandException>(error);
		Assert.Contains("add, update", error.Message);
		Assert.NotNull(outcome.GetPath<object>("Ingredient"));
	}

	[Fact]
	public void Parse_OptionsOnlyAreStillMissingTheCommand()
	{
		var outcome = Calories().ParseCommand("--db", "food.db", "-v");

		Assert.IsType<MissingCommandException>(Assert.Single(outcome.Errors));
		Assert.Equal("food.db", outcome.Get<string>("Db"));
		Assert.True(outcome.Get<bool>("Verbose"));
	}

	[Fact]
	public void Parse_UnknownCommand_NamesItAndTheAlternatives()
	{
		var outcome = Calories().ParseCommand("ingredient", "remove");

		var error = Assert.Single(outcome.Errors);
		Assert.IsType<UnknownCommandException>(error);
		Assert.Contains("'remove'", error.Message);
		Assert.Contains("add, update", error.Message);
		Assert.Null(outcome.GetPath<object>("Ingredient.Add"));
	}

	[Fact]
	public void Parse_CommandNamesAreCaseSensitive()
	{
		var outcome = Calories().ParseCommand("Log", "lunch");

		Assert.IsType<UnknownCommandException>(Assert.Single(outcome.Errors));
	}

	[Fact]
	public void Parse_ErrorsOfEveryLevelAreCollected()
	{
		var outcome = Calories().ParseCommand("--bogus", "meal", "--nonsense", "list", "--worse");

		Assert.Equal(3, outcome.Errors.Count);
		Assert.All(outcome.Errors, error => Assert.IsType<UnexpectedArgumentException>(error));
		Assert.NotNull(outcome.GetPath<object>("Meal.List"));
	}

	[Fact]
	public void ParseCommand_MissingRequiredArgumentsOfALeaf_AreReportedBothWays()
	{
		var outcome = Calories().ParseCommand("ingredient", "add", "--kcal", "5");

		var missing = Assert.Single(outcome.MissingRequired);
		Assert.IsType<MissingRequiredArgumentException>(missing);
		Assert.Contains(missing, outcome.Errors);
	}

	[Fact]
	public void ParseCommand_MissingRequiredArgumentsOfAGroupAndALeaf_AreAllReported()
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

		var outcome = parser.ParseCommand("run");

		Assert.Equal(2, outcome.MissingRequired.Count);
		Assert.Equal(2, outcome.Errors.Count);
	}

	[Fact]
	public void ParseCommand_OptionWithoutAValue_IsAnErrorButNotAMissingRequiredArgument()
	{
		var outcome = Calories().ParseCommand("--db");

		Assert.Contains(outcome.Errors, error => error is MissingRequiredArgumentException);
		Assert.Empty(outcome.MissingRequired);
	}

	[Fact]
	public void Parse_NeverThrowsOrExits_WhateverIsWrong()
	{
		var parser = Calories();

		var outcome = parser.ParseCommand("ingredient", "add");

		Assert.NotEmpty(outcome.Errors);
	}
	#endregion

	#region the selected command

	[Fact]
	public void Selected_IsTheLeafThatWasParsed()
	{
		var outcome = Calories().ParseCommand("ingredient", "add", "-n", "rice");

		Assert.Same(outcome.GetPath<object>("Ingredient.Add"), outcome.Get<object>("Selected"));
		Assert.Equal("ingredient add", outcome.Get<CommandInfo>("SelectedCommand")!.ToString());
	}

	[Theory]
	[InlineData(new[] { "ingredient", "update", "12" }, "IngredientUpdate", "ingredient update")]
	[InlineData(new[] { "--db", "x.db", "meal", "-d", "monday", "list" }, "MealList", "meal list")]
	[InlineData(new[] { "log", "lunch" }, "LogAdd", "log")]
	public void Selected_IsFoundAtEveryDepth(string[] args, string leafClass, string path)
	{
		var outcome = Calories().ParseCommand(args);

		Assert.Empty(outcome.Errors);
		Assert.Equal(leafClass, outcome.Get<object>("Selected")!.GetType().Name);
		Assert.Equal(path, outcome.Get<CommandInfo>("SelectedCommand")!.ToString());
	}

	[Fact]
	public void SelectedCommand_IsTheEntryOfCommands()
	{
		var parser = Calories();
		var outcome = parser.ParseCommand("meal", "list");

		var commands = parser.GetStaticProperty<IReadOnlyList<CommandInfo>>("Commands")!;
		Assert.Same(commands.Single(c => c.ToString() == "meal list"), outcome.Get<CommandInfo>("SelectedCommand"));
	}

	[Theory]
	[InlineData("")]
	[InlineData("ingredient")]
	[InlineData("ingredient remove")]
	[InlineData("--db x.db")]
	public void Selected_IsNullWhenNoLeafWasReached(string commandLine)
	{
		var outcome = Calories().ParseCommand(commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries));

		Assert.NotEmpty(outcome.Errors);
		Assert.Null(outcome.Get<object>("Selected"));
		Assert.Null(outcome.Get<CommandInfo>("SelectedCommand"));
	}

	[Fact]
	public void Selected_IsTheLeafEvenWhenItsArgumentsHaveErrors()
	{
		var outcome = Calories().ParseCommand("ingredient", "add", "--kcal", "lots");

		Assert.NotEmpty(outcome.Errors);
		Assert.Equal("IngredientAdd", outcome.Get<object>("Selected")!.GetType().Name);
	}

	[Fact]
	public void Selected_OfTheSmallestTree_IsItsOnlyLeaf()
	{
		var parser = GeneratedParser.Compile("""
			using ArgumentParser;
			namespace ArgumentParser.Tests;

			[CommandApp("app")]
			public partial class App { public Run? Run { get; private set; } }

			[ParameterCollection]
			[Command("run", "Run it")]
			public partial class Run { }
			""", "App");

		Assert.Equal("Run", parser.ParseCommand("run").Get<object>("Selected")!.GetType().Name);
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
		Assert.Equal(CommandAnnotations.Destructive | CommandAnnotations.OpenWorld, commands["log"].Annotations);
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

	#region help of the levels and the settings of the tree

	private static GeneratedParser SmallTree(string commandApp) => GeneratedParser.Compile($$"""
		using ArgumentParser;
		namespace ArgumentParser.Tests;

		{{commandApp}}
		public partial class App
		{
			[Flag("v", "verbose", "More output")] public bool Verbose { get; set; }
			public Run? Run { get; private set; }
		}

		[ParameterCollection]
		[Command("run", "Run it")]
		public partial class Run
		{
			[Positional(0, "What to run", true)] public string Target { get; set; } = "";
		}
		""", "App");

	[Fact]
	public void Help_OnTheRoot_ShowsTheApplicationItsCommandsAndItsOptions()
	{
		var outcome = Calories().ParseCommand("-h");

		Assert.True(outcome.HelpRequested);
		Assert.StartsWith("calories - Track what you eat\n", outcome.HelpText, StringComparison.Ordinal);
		Assert.Contains("\tcalories [options] <command> [command args]\n", outcome.HelpText, StringComparison.Ordinal);
		Assert.Contains("Commands:\n\tingredient: Manage ingredients\n\tmeal: Manage meals\n\tlog: Log a meal\n", outcome.HelpText, StringComparison.Ordinal);
		Assert.Contains("\tOption: --db : Path to the database\n", outcome.HelpText, StringComparison.Ordinal);
		Assert.Contains("\tFlag: -h | --Help : Display help text\n", outcome.HelpText, StringComparison.Ordinal);
		Assert.DoesNotContain("Options of", outcome.HelpText, StringComparison.Ordinal);
	}

	[Fact]
	public void Help_OnAGroup_ShowsItsCommandsItsOptionsAndThoseOfTheLevelsAbove()
	{
		var outcome = Calories().ParseCommand("meal", "--Help");

		Assert.True(outcome.HelpRequested);
		Assert.StartsWith("calories meal - Manage meals\n", outcome.HelpText, StringComparison.Ordinal);
		Assert.Contains("\tcalories [calories options] meal [options] <command> [command args]\n", outcome.HelpText, StringComparison.Ordinal);
		Assert.Contains("Commands:\n\tlist: List meals\n", outcome.HelpText, StringComparison.Ordinal);
		Assert.Contains("Options:\n\tOption: -d | --day : Only consider this day\n", outcome.HelpText, StringComparison.Ordinal);
		Assert.Contains("Options of calories, before 'meal':\n\tFlag: -v | --verbose : Print extra output\n\tOption: --db : Path to the database\n", outcome.HelpText, StringComparison.Ordinal);
	}

	[Fact]
	public void Help_OnALeaf_ShowsItsFullPathItsArgumentsAndTheOptionsOfEveryLevelAbove()
	{
		var outcome = Calories().ParseCommand("meal", "list", "-h");

		Assert.True(outcome.HelpRequested);
		Assert.Empty(outcome.Errors);
		Assert.StartsWith("calories meal list - List meals\n", outcome.HelpText, StringComparison.Ordinal);
		Assert.Contains("\tcalories [calories options] meal [meal options] list [optional args] (required args)\n", outcome.HelpText, StringComparison.Ordinal);
		Assert.Contains("Optional arguments:\n\tFlag: -a | --all : Include everything\n\tFlag: -h | --Help : Display help text\n", outcome.HelpText, StringComparison.Ordinal);
		Assert.Contains("Options of calories, before 'meal':", outcome.HelpText, StringComparison.Ordinal);
		Assert.Contains("Options of meal, before 'list':\n\tOption: -d | --day : Only consider this day\n", outcome.HelpText, StringComparison.Ordinal);
	}

	[Fact]
	public void Help_OnALeaf_ListsRequiredPositionalsAndOptions()
	{
		var update = Calories().ParseCommand("ingredient", "update", "-h");
		var add = Calories().ParseCommand("ingredient", "add", "-h");

		// a level without options is left out of the usage and of the sections of the levels above
		Assert.Contains("\tcalories [calories options] ingredient update [optional args] (required args)\n", update.HelpText, StringComparison.Ordinal);
		Assert.DoesNotContain("Options of ingredient", update.HelpText, StringComparison.Ordinal);
		Assert.Contains("Required arguments:\n\tPosition: 0: Id of the ingredient\n", update.HelpText, StringComparison.Ordinal);
		Assert.Contains("Required arguments:\n\tOption: -n | --name : Name of the ingredient\n", add.HelpText, StringComparison.Ordinal);
		// help is requested even though the required name is missing
		Assert.True(add.HelpRequested);
	}

	[Fact]
	public void Help_GivenBeforeACommandWord_ShowsTheLevelItIsGivenOn()
	{
		var outcome = Calories().ParseCommand("-h", "meal", "list");

		Assert.True(outcome.HelpRequested);
		Assert.StartsWith("calories - ", outcome.HelpText, StringComparison.Ordinal);
		Assert.Null(outcome.GetPath<object>("Meal"));
	}

	[Fact]
	public void Error_GoesWithTheHelpOfTheDeepestLevelThatWasReached()
	{
		var unknown = Calories().ParseCommand("meal", "nonsense");
		var invalid = Calories().ParseCommand("ingredient", "update", "x");
		var missing = Calories().ParseCommand();

		Assert.False(unknown.HelpRequested);
		Assert.StartsWith("calories meal - ", unknown.HelpText, StringComparison.Ordinal);
		Assert.StartsWith("calories ingredient update - ", invalid.HelpText, StringComparison.Ordinal);
		Assert.StartsWith("calories - ", missing.HelpText, StringComparison.Ordinal);
	}

	[Fact]
	public void Help_TextsArePublicConstantsOfEveryLevelAndPartOfTheCommands()
	{
		var parser = Calories();
		var add = Commands().Single(c => c.ToString() == "ingredient add");

		Assert.Equal(parser.ParseCommand("-h").HelpText, parser.GetStaticMember<string>("CommandHelpText"));
		Assert.Equal(parser.ParseCommand("ingredient", "add", "-h").HelpText, add.HelpText);
		Assert.StartsWith("calories ingredient add - Add an ingredient\n", add.HelpText, StringComparison.Ordinal);
	}

	[Fact]
	public void Help_OfALeafInATree_DoesNotSetItsOwnDisplayHelp()
	{
		var outcome = Calories().ParseCommand("ingredient", "add", "-n", "rice", "-h");

		Assert.True(outcome.HelpRequested);
		Assert.False(outcome.GetPath<bool>("Ingredient.Add.DisplayHelp"));
		Assert.Equal("rice", outcome.GetPath<string>("Ingredient.Add.Name"));
	}

	[Fact]
	public void OwnParseOfALeafInATree_StillSetsItsDisplayHelp()
	{
		var leaf = GeneratedParser.Compile(CaloriesTree, "IngredientAdd");

		var outcome = leaf.ParseCore("-n", "rice", "-h");

		Assert.True(outcome.Get<bool>("DisplayHelp"));
		Assert.Equal("rice", outcome.Get<string>("Name"));
	}

	[Fact]
	public void HelpNames_OfTheCommandApp_AreUsedOnEveryLevelInsteadOfThoseOfTheLeaf()
	{
		var parser = SmallTree("[CommandApp(\"app\", helpArgumentShortName: \"?\", helpArgumentLongName: \"info\", behaviourOnError: BehaviourOnError.ThrowNever)]");

		Assert.True(parser.ParseCommand("-?").HelpRequested);
		Assert.True(parser.ParseCommand("run", "--info").HelpRequested);
		Assert.Contains("\tFlag: -? | --info : Display help text\n", parser.ParseCommand("run", "--info").HelpText, StringComparison.Ordinal);
		// the help names of the leaf's own ParameterCollection attribute are not known in the tree
		var leafOwn = parser.ParseCommand("run", "x", "-h");
		Assert.False(leafOwn.HelpRequested);
		Assert.IsType<UnexpectedArgumentException>(Assert.Single(leafOwn.Errors));
	}

	[Fact]
	public void HelpTextOnly_GeneratesTheTextsButNoHelpArgument()
	{
		var parser = SmallTree("[CommandApp(\"app\", \"An app\", HelpTextGeneration.GenerateTextOnly)]");

		var outcome = parser.ParseCommand("-h");

		Assert.False(outcome.HelpRequested);
		Assert.IsType<UnexpectedArgumentException>(outcome.Errors[0]);
		Assert.StartsWith("app - An app\n", outcome.HelpText, StringComparison.Ordinal);
		Assert.DoesNotContain("Display help text", outcome.HelpText, StringComparison.Ordinal);
		Assert.Equal(outcome.HelpText, parser.GetStaticMember<string>("CommandHelpText"));
	}

	[Fact]
	public void HelpNone_GeneratesNoTexts()
	{
		var parser = SmallTree("[CommandApp(\"app\", \"An app\", HelpTextGeneration.None, behaviourOnError: BehaviourOnError.ThrowNever)]");

		var outcome = parser.ParseCommand("run");

		Assert.Null(outcome.HelpText);
		Assert.Null(parser.GetStaticMember<string>("CommandHelpText"));
		Assert.Equal("", Assert.Single(parser.GetStaticProperty<IReadOnlyList<CommandInfo>>("Commands")!).HelpText);
		Assert.Contains(parser.Parse("run").Errors, e => e is MissingRequiredArgumentException);
	}

	[Fact]
	public void Parse_AppliesTheBehaviourOnErrorOfTheCommandApp()
	{
		var never = SmallTree("[CommandApp(\"app\", behaviourOnError: BehaviourOnError.ThrowNever)]");
		var anyError = SmallTree("[CommandApp(\"app\", behaviourOnError: BehaviourOnError.ThrowIfAnyError)]");
		var missingRequired = SmallTree("[CommandApp(\"app\", behaviourOnError: BehaviourOnError.ThrowIfMissingRequired)]");

		Assert.NotEmpty(never.Parse("nonsense").Errors);
		Assert.Empty(never.Parse("-v", "run", "x").Errors);
		Assert.Throws<AggregateException>(() => anyError.Parse("nonsense"));
		Assert.Empty(anyError.Parse("run", "x").Errors);
		var thrown = Assert.Throws<AggregateException>(() => missingRequired.Parse("run"));
		Assert.IsType<MissingRequiredArgumentException>(Assert.Single(thrown.InnerExceptions));
		Assert.NotEmpty(missingRequired.Parse("nonsense").Errors);
	}

	[Fact]
	public void Commands_ParseWithTheHelpArgumentOfTheTreeWithoutAnError()
	{
		var add = Commands().Single(c => c.ToString() == "ingredient add");

		var (_, errors) = add.Parse(["--name", "rice", "-h"]);

		Assert.Empty(errors);
	}
	#endregion

	#region cultures

	private const string CultureTree = """
		using ArgumentParser;

		namespace ArgumentParser.Tests;

		[CommandApp("tool", "A tool", UseCurrentCulture = true)]
		public partial class Tool
		{
			[Option("s", "scale", "Scale")]
			public double Scale { get; set; }

			public Group? Group { get; private set; }
		}

		[Command("group", "A group")]
		public partial class Group
		{
			[Option("o", "offset", "Offset")]
			public decimal Offset { get; set; }

			public Run? Run { get; private set; }
		}

		[ParameterCollection]
		[Command("run", "Run it")]
		public partial class Run
		{
			[Option("a", "amount", "Amount")]
			public double Amount { get; set; }
		}
		""";

	[Fact]
	public void Parse_ByDefault_EveryLevelRejectsCultureDependentText()
	{
		CultureInfo.CurrentCulture = new CultureInfo("nl-NL");

		var rejected = Calories().ParseCommand("ingredient", "add", "-n", "rice", "--kcal", "1,5");
		var accepted = Calories().ParseCommand("ingredient", "add", "-n", "rice", "--kcal", "1.5");

		Assert.IsType<InvalidArgumentValueException>(Assert.Single(rejected.Errors));
		Assert.Empty(accepted.Errors);
		Assert.Equal(1.5, accepted.GetPath<double>("Ingredient.Add.Kcal"));
	}

	[Fact]
	public void Parse_UseCurrentCultureOfTheCommandApp_AppliesToEveryLevelAndTheLeaf()
	{
		CultureInfo.CurrentCulture = new CultureInfo("nl-NL");
		var parser = GeneratedParser.Compile(CultureTree, "Tool");

		var outcome = parser.ParseCommand("--scale", "1,5", "group", "--offset", "2,25", "run", "--amount", "3,5");

		Assert.Empty(outcome.Errors);
		Assert.Equal(1.5, outcome.GetPath<double>("Scale"));
		Assert.Equal(2.25m, outcome.GetPath<decimal>("Group.Offset"));
		Assert.Equal(3.5, outcome.GetPath<double>("Group.Run.Amount"));
	}

	[Fact]
	public void ParseCore_OfALeafInATreeWithUseCurrentCulture_KeepsTheSettingOfTheLeaf()
	{
		CultureInfo.CurrentCulture = new CultureInfo("nl-NL");
		var leaf = GeneratedParser.Compile(CultureTree, "Run");

		var rejected = leaf.ParseCore("--amount", "3,5");
		var accepted = leaf.ParseCore("--amount", "3.5");

		Assert.IsType<InvalidArgumentValueException>(Assert.Single(rejected.Errors));
		Assert.Equal(3.5, accepted.Get<double>("Amount"));
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

		var ingredient = parser.ParseCommand("ingredient", "add", "--name", "rice");
		var meal = parser.ParseCommand("meal", "add", "--day", "monday");

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

		var outcome = parser.ParseCommand("group", "leaf", "-f");

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

		var outcome = parser.ParseCommand("-v", "run");

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

		var outcome = parser.ParseCommand("--first", "1", "--second", "2", "-v", "group", "--gopt", "3", "leaf");

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

		var outcome = parser.ParseCommand("run");

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

			[ParameterCollection(Annotations = (CommandAnnotations)(-1))]
			[Command("run", "Run it")]
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

		var outcome = parser.ParseCommand("group", "run", "-n", "x");

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

		var outcome = parser.ParseCommand("-v", "run", "it");

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

		var outcome = parser.ParseCommand("group", "leaf", "-n", "x");

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
	public void Generator_TreeCodeHasNoWarnings_WithNullableEnabledAndWithoutImplicitUsings()
	{
		// a user project that treats warnings as errors must accept the generated code, so not even a warning may come from it
		Assert.Empty(GeneratedParser.GeneratedCodeDiagnostics(CaloriesTree).Select(d => d.ToString()));
	}

	[Theory]
	[InlineData("public partial class Run { [Option(\"x\", \"one\", \"a\")] public string A { get; set; } = \"\"; [Option(\"x\", \"two\", \"b\")] public string B { get; set; } = \"\"; }", "ARG003")]
	[InlineData("public class Run { }", "ARG001")]
	public void Generator_LeafWithErrors_LeavesItsTreeUngeneratedInsteadOfAddingCompilerErrors(string leaf, string rule)
	{
		var source = $$"""
			using ArgumentParser;
			namespace ArgumentParser.Tests;

			[CommandApp("app")]
			public partial class App { public Run? Run { get; private set; } }

			[ParameterCollection]
			[Command("run")]
			{{leaf}}
			""";

		Assert.DoesNotContain(Generate(source).Keys, name => name.EndsWith("_Command.g.cs", StringComparison.Ordinal));
		Assert.Empty(GeneratedParser.GeneratedCodeDiagnostics(source));
		var compilation = TestHelper.CreateCompilation(source);
		CSharpGeneratorDriver.Create(new ParserGenerator()).RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
		Assert.Contains(diagnostics, d => d.Id == rule);
	}

	[Fact]
	public void Generator_AddsAFilePerNodeAndLeavesKeepTheirOwnParser()
	{
		var files = Generate(CaloriesTree);

		Assert.Contains("ArgumentParser.Tests.Calories_Command.g.cs", files.Keys);
		Assert.Contains("ArgumentParser.Tests.IngredientGroup_Command.g.cs", files.Keys);
		Assert.Contains("ArgumentParser.Tests.MealGroup_Command.g.cs", files.Keys);
		Assert.Contains("ArgumentParser.Tests.IngredientAdd_Command.g.cs", files.Keys);
		Assert.Contains("ArgumentParser.Tests.IngredientAdd_Parser.g.cs", files.Keys);
		// the part of the tree calls the parser of the leaf, and only the parser's part carries the GeneratedCode attribute
		Assert.Contains("ParseCore(args, commandFlags, CommandHelpArgument, false)", files["ArgumentParser.Tests.IngredientAdd_Command.g.cs"]);
		Assert.DoesNotContain("GeneratedCodeAttribute", files["ArgumentParser.Tests.IngredientAdd_Command.g.cs"]);
	}

	[Fact]
	public void Generator_OnlyTheRootHasParseAndCommands()
	{
		var files = Generate(CaloriesTree);

		Assert.Contains("public static (Calories result, List<global::ArgumentParser.ArgumentParserException> errors) Parse(string[] args)", files["ArgumentParser.Tests.Calories_Command.g.cs"]);
		Assert.Contains("Commands", files["ArgumentParser.Tests.Calories_Command.g.cs"]);
		Assert.DoesNotContain("public static", files["ArgumentParser.Tests.IngredientGroup_Command.g.cs"]);
		Assert.DoesNotContain("Commands {", files["ArgumentParser.Tests.IngredientGroup_Command.g.cs"]);
		Assert.Contains("ParseCommand(string[] args)", files["ArgumentParser.Tests.IngredientGroup_Command.g.cs"]);
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

		var outcome = parser.ParseCommand("two", "add", "--name", "x");

		Assert.Empty(outcome.Errors);
		Assert.Equal("x", outcome.GetPath<string>("Two.Add.Name"));
		Assert.Null(outcome.GetPath<object>("One"));
	}

	[Fact]
	public void Generator_NamesAndDescriptionsWithQuotesAndBackslashes_AreEscaped()
	{
		// ARG014 keeps quotes and backslashes out of command words, but the name of the application and the descriptions can hold them
		var parser = GeneratedParser.Compile("""
			using ArgumentParser;
			namespace ArgumentParser.Tests;

			[CommandApp("my \"app\" \\ x", "The \"app\"")]
			public partial class App
			{
				[Option("d", "dir", "the \"dir\" \\ here")] public string Dir { get; set; } = "";
				public Group? Group { get; private set; }
			}

			[Command("grp", "A \"group\"")]
			public partial class Group { public Run? Run { get; private set; } }

			[ParameterCollection]
			[Command("run", "Runs \"it\" \\ now")]
			public partial class Run { }
			""", "App");

		var command = Assert.Single(parser.GetStaticProperty<IReadOnlyList<CommandInfo>>("Commands")!);
		var outcome = parser.ParseCommand("-d", "x", "grp", "run");

		Assert.Equal("Runs \"it\" \\ now", command.Description);
		Assert.Contains("my \"app\" \\ x grp run", command.HelpText, StringComparison.Ordinal);
		Assert.Empty(outcome.Errors);
		Assert.NotNull(outcome.GetPath<object>("Group.Run"));
	}
	#endregion
}
