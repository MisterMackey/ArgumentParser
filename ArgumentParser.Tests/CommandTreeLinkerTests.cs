using System.Globalization;
using ArgumentParser.Internal;
using ArgumentParser.Internal.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArgumentParser.Tests;

/// <summary>
/// Tests for linking the nodes of a compilation into trees (CommandTreeLinker) and for the rules that need the whole tree.
/// </summary>
public class CommandTreeLinkerTests
{
	private const string Preamble = """
		using System;
		using ArgumentParser;

		namespace Tree;

		public class Plain { }

		""";

	private sealed record Linked(IReadOnlyList<CommandNodeModel> Models, LinkResult Result, string Source)
	{
		public IReadOnlyList<string> RuleIds => Result.Problems.AsReadOnly().Select(item => item.RuleId).ToList();

		public IReadOnlyList<LinkProblem> Problems => Result.Problems.AsReadOnly().ToList();

		public IReadOnlyList<CommandTree> Trees => Result.Trees.AsReadOnly().ToList();

		public IReadOnlyList<TreeNode> NodesOf(CommandTree tree) => tree.Nodes.AsReadOnly().ToList();

		public IReadOnlyList<DiagnosticInfo> Diagnostics()
		{
			var diagnostics = CommandTreeLinker.ToDiagnostics(Result, Models);
			return diagnostics.AsReadOnly().ToList();
		}

		public string TextAt(DiagnosticInfo diagnostic)
		{
			var span = diagnostic.Location!.TextSpan;
			return Source.Substring(span.Start, span.Length);
		}
	}

	private static Linked Link(string types, string preamble = Preamble)
	{
		var source = preamble + types;
		var compilation = TestHelper.CreateCompilation(source);
		var tree = compilation.SyntaxTrees.Single();
		var semanticModel = compilation.GetSemanticModel(tree);
		var models = new List<CommandNodeModel>();
		foreach (var declaration in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
		{
			var symbol = (INamedTypeSymbol)semanticModel.GetDeclaredSymbol(declaration)!;
			if (symbol.GetAttributes().Any(a => a.AttributeClass?.Name is "CommandAppAttribute" or "CommandAttribute"))
			{
				models.Add(CommandNodeExtractor.Extract(declaration, symbol, semanticModel));
			}
		}
		return new Linked(models, CommandTreeLinker.Link(models.Select(m => m.Source)), source);
	}

	private const string CaloriesTree = """
		[CommandApp("calories", "Track what you eat")]
		public partial class Calories
		{
			[Option("", "db", "Path to the database")]
			public string Db { get; set; } = "";

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
			[Option("d", "day", "Only this day")]
			public string Day { get; set; } = "";

			public MealList? List { get; private set; }
		}

		[ParameterCollection]
		[Command("add", "Add an ingredient")]
		public partial class IngredientAdd { }

		[ParameterCollection]
		[Command("update", "Change an ingredient")]
		public partial class IngredientUpdate { }

		[ParameterCollection]
		[Command("list", "List meals")]
		public partial class MealList { }

		[ParameterCollection]
		[Command("log", "Log a meal")]
		public partial class LogAdd { }
		""";

	#region linking

	[Fact]
	public void ValidTree_LinksTheNodesParentFirstInTheOrderTheChildrenAreDeclared()
	{
		var linked = Link(CaloriesTree);

		Assert.Empty(linked.Problems);
		var tree = Assert.Single(linked.Trees);
		Assert.False(tree.HasErrors);
		var nodes = linked.NodesOf(tree);
		Assert.Equal(
			["Tree.Calories", "Tree.IngredientGroup", "Tree.IngredientAdd", "Tree.IngredientUpdate", "Tree.MealGroup", "Tree.MealList", "Tree.LogAdd"],
			nodes.Select(n => n.Source.Node.TypeName));
		Assert.Equal(CommandNodeKind.Root, tree.Root.Source.Node.Kind);
	}

	[Fact]
	public void ValidTree_GivesEveryNodeItsPathWithoutTheNameOfTheApplication()
	{
		var tree = Assert.Single(Link(CaloriesTree).Trees);

		var paths = tree.Nodes.AsReadOnly()
			.ToDictionary(n => n.Source.Node.ClassName, n => string.Join(" ", n.Path.AsReadOnly()));
		Assert.Equal("", paths["Calories"]);
		Assert.Equal("ingredient", paths["IngredientGroup"]);
		Assert.Equal("ingredient add", paths["IngredientAdd"]);
		Assert.Equal("ingredient update", paths["IngredientUpdate"]);
		Assert.Equal("meal list", paths["MealList"]);
		Assert.Equal("log", paths["LogAdd"]);
	}

	[Fact]
	public void ValidTree_ChildrenCarryThePropertyTheTypeAndTheCommandName()
	{
		var tree = Assert.Single(Link(CaloriesTree).Trees);

		var children = tree.Root.Children.AsReadOnly().ToList();
		Assert.Equal(["Ingredient", "Meal", "Log"], children.Select(c => c.PropertyName));
		Assert.Equal(["Tree.IngredientGroup", "Tree.MealGroup", "Tree.LogAdd"], children.Select(c => c.TypeName));
		Assert.Equal(["ingredient", "meal", "log"], children.Select(c => c.CommandName));
	}

	[Fact]
	public void ValidTree_KeepsTheOptionsOfGroupsAndRootAndNothingForLeaves()
	{
		var tree = Assert.Single(Link(CaloriesTree).Trees);

		var options = tree.Nodes.AsReadOnly()
			.ToDictionary(n => n.Source.Node.ClassName, n => n.Source.Arguments.Options.Count);
		Assert.Equal(1, options["Calories"]);
		Assert.Equal(1, options["MealGroup"]);
		Assert.Equal(0, options["IngredientGroup"]);
		Assert.Equal(0, options["LogAdd"]);
	}

	[Fact]
	public void ValidTree_IsTheSameWhateverTheOrderOfTheNodes()
	{
		var linked = Link(CaloriesTree);

		var reversed = CommandTreeLinker.Link(linked.Models.Reverse().Select(m => m.Source));

		Assert.Equal(linked.Result, reversed);
		Assert.Equal(linked.Result.GetHashCode(), reversed.GetHashCode());
	}

	[Fact]
	public void ValidTree_IsTheSameWhenCodeOnlyMoves()
	{
		var linked = Link(CaloriesTree);
		var moved = Link("// shifts everything below\n" + CaloriesTree);

		Assert.NotEqual(linked.Models[0], moved.Models[0]);
		Assert.Equal(linked.Result, moved.Result);
	}

	[Fact]
	public void ClassDeclaredInSeveralParts_IsOneNode()
	{
		// every part of Group is read, each into a model of its own with its own location, and its child is in the second part
		var linked = Link("""
			[CommandApp("app")]
			public partial class App { public Group? G { get; private set; } }

			[Command("group")]
			public partial class Group { }

			public partial class Group { public Leaf? L { get; private set; } }

			[ParameterCollection]
			[Command("leaf")]
			public partial class Leaf { }
			""");
		var groupModels = linked.Models.Where(m => m.Node.ClassName == "Group").ToList();
		var reversed = CommandTreeLinker.Link(Enumerable.Reverse(linked.Models).Select(m => m.Source));

		Assert.Equal(2, groupModels.Count);
		Assert.NotEqual(groupModels[0].ClassLocation, groupModels[1].ClassLocation);
		Assert.Empty(linked.Problems);
		var tree = Assert.Single(linked.Trees);
		Assert.Equal(["App", "Group", "Leaf"], linked.NodesOf(tree).Select(n => n.Source.Node.ClassName));
		Assert.Equal(linked.Result, reversed);
	}

	[Fact]
	public void PropertiesWithTypesThatAreNotNodes_AreNotChildren()
	{
		var linked = Link("""
			[CommandApp("app")]
			public partial class App
			{
				public Group? G { get; private set; }
				public Plain? Other { get; set; }
				public string Name { get; set; } = "";
			}

			[Command("group")]
			public partial class Group { public Leaf? L { get; private set; } }

			[ParameterCollection]
			[Command("leaf")]
			public partial class Leaf { }
			""");

		var tree = Assert.Single(linked.Trees);
		Assert.Equal(["G"], tree.Root.Children.AsReadOnly().Select(item => item.PropertyName));
		Assert.Empty(linked.Problems);
	}

	[Theory]
	[InlineData("[Option(\"o\", \"other\", \"an option\")]")]
	[InlineData("[Flag(\"o\", \"other\", \"a flag\")]")]
	[InlineData("[Positional(0, \"a positional\")]")]
	[InlineData("[Option(\"o\", \"other\", \"an option\")] [ParsedWithMethod(\"Read\")]")]
	public void ARG026_ArgumentOfACommandClassType_IsNotAChildAndOnlyThatIsReported(string attributes)
	{
		var linked = Link($$"""
			[CommandApp("app")]
			public partial class App
			{
				{{attributes}}
				public Leaf? Other { get; set; }
			}

			[ParameterCollection]
			[Command("leaf")]
			public partial class Leaf { }
			""");

		// the root has no children and nothing reaches the leaf, but saying so would only repeat ARG026
		var app = linked.Models.Single(m => m.Node.ClassName == "App");
		var diagnostic = Assert.Single(app.Diagnostics.AsReadOnly());
		Assert.Equal("ARG026", diagnostic.Id);
		Assert.Empty(linked.RuleIds);
		var tree = Assert.Single(linked.Trees);
		Assert.True(tree.HasErrors);
		Assert.Empty(tree.Root.Children.AsReadOnly());
	}

	[Fact]
	public void ARG026_GroupWithAnArgumentOfACommandClassType_KeepsItsOtherChildren()
	{
		var linked = Link("""
			[CommandApp("app")]
			public partial class App
			{
				[Option("o", "other", "an option")]
				public Other? Wrong { get; set; }
				public Leaf? Leaf { get; private set; }
			}

			[ParameterCollection] [Command("leaf")] public partial class Leaf { }
			[ParameterCollection] [Command("other")] public partial class Other { }
			""");

		Assert.Empty(linked.RuleIds);
		var tree = Assert.Single(linked.Trees);
		Assert.Equal(["Leaf"], tree.Root.Children.AsReadOnly().Select(c => c.PropertyName));
	}

	[Fact]
	public void ARG026_UnreachableGroupWithAnArgumentOfACommandClassType_IsStillUnreachable()
	{
		var linked = Link("""
			[Command("group")]
			public partial class Group
			{
				[Option("o", "other", "an option")]
				public Leaf? Wrong { get; set; }
			}

			[ParameterCollection] [Command("leaf")] public partial class Leaf { }
			""");

		var problem = Assert.Single(linked.Problems);
		Assert.Equal("ARG019", problem.RuleId);
		Assert.Equal("Tree.Group", problem.TypeName);
	}

	[Fact]
	public void SeveralRoots_GiveSeveralTrees()
	{
		var linked = Link("""
			[CommandApp("one")]
			public partial class One { public OneLeaf? Leaf { get; private set; } }

			[CommandApp("two")]
			public partial class Two { public TwoLeaf? Leaf { get; private set; } }

			[ParameterCollection]
			[Command("a")]
			public partial class OneLeaf { }

			[ParameterCollection]
			[Command("a")]
			public partial class TwoLeaf { }
			""");

		Assert.Empty(linked.Problems);
		Assert.Equal(["Tree.One", "Tree.Two"], linked.Trees.Select(t => t.Root.Source.Node.TypeName));
		Assert.All(linked.Trees, tree => Assert.Equal(2, tree.Nodes.Count));
	}

	[Fact]
	public void RootAlone_GivesATreeWithOnlyTheRoot()
	{
		var linked = Link("[CommandApp(\"app\")] public partial class App { }");

		var tree = Assert.Single(linked.Trees);
		Assert.Single(linked.NodesOf(tree));
	}

	[Fact]
	public void NoNodes_GiveNothing()
	{
		var result = CommandTreeLinker.Link(Array.Empty<CommandNodeSource>());

		Assert.Equal(0, result.Trees.Count);
		Assert.Equal(0, result.Problems.Count);
	}
	#endregion

	#region ARG017 duplicate names

	[Fact]
	public void ARG017_SiblingsWithTheSameName_AreReportedOnBothProperties()
	{
		var linked = Link("""
			[CommandApp("app")]
			public partial class App
			{
				public One? First { get; private set; }
				public Two? Second { get; private set; }
				public Three? Third { get; private set; }
			}

			[ParameterCollection] [Command("same")] public partial class One { }
			[ParameterCollection] [Command("same")] public partial class Two { }
			[ParameterCollection] [Command("other")] public partial class Three { }
			""");

		Assert.Equal(["ARG017", "ARG017"], linked.RuleIds);
		var diagnostics = linked.Diagnostics();
		Assert.Equal(["First", "Second"], diagnostics.Select(linked.TextAt));
		Assert.All(diagnostics, d => Assert.Contains("'same'", d.ToDiagnostic().GetMessage()));
		Assert.All(diagnostics, d => Assert.Contains("'App'", d.ToDiagnostic().GetMessage()));
		Assert.True(Assert.Single(linked.Trees).HasErrors);
	}

	[Fact]
	public void ARG017_TheSameNameUnderDifferentParents_IsFine()
	{
		var linked = Link("""
			[CommandApp("app")]
			public partial class App
			{
				public Ingredient? Ingredient { get; private set; }
				public Meal? Meal { get; private set; }
			}

			[Command("ingredient")] public partial class Ingredient { public IngredientAdd? Add { get; private set; } }
			[Command("meal")] public partial class Meal { public MealAdd? Add { get; private set; } }
			[ParameterCollection] [Command("add")] public partial class IngredientAdd { }
			[ParameterCollection] [Command("add")] public partial class MealAdd { }
			""");

		Assert.Empty(linked.Problems);
		Assert.False(Assert.Single(linked.Trees).HasErrors);
	}
	#endregion

	#region ARG018 command class used more than once

	[Fact]
	public void ARG018_ClassWithTwoParents_IsReportedOnEveryPropertyThatUsesIt()
	{
		var linked = Link("""
			[CommandApp("app")]
			public partial class App
			{
				public One? One { get; private set; }
				public Two? Two { get; private set; }
			}

			[Command("one")] public partial class One { public Shared? Shared { get; private set; } }
			[Command("two")] public partial class Two { public Shared? Shared { get; private set; } }
			[ParameterCollection] [Command("shared")] public partial class Shared { }
			""");

		Assert.Equal(["ARG018", "ARG018"], linked.RuleIds);
		Assert.Equal(["One", "Two"], linked.Problems.Select(p => p.Arguments[1]));
		Assert.All(linked.Problems, p => Assert.Equal("Shared", p.Arguments[0]));
		Assert.True(Assert.Single(linked.Trees).HasErrors);
	}

	[Fact]
	public void ARG018_ClassUsedByTwoPropertiesOfOneParent_IsReportedOnBothAndIsNotADuplicateName()
	{
		// one command declared twice, not two commands with the same name: ARG017 is about the latter
		var linked = Link("""
			[CommandApp("app")]
			public partial class App
			{
				public Run? Run { get; private set; }
				public Run? Again { get; private set; }
			}

			[ParameterCollection] [Command("run")] public partial class Run { }
			""");

		Assert.Equal(["ARG018", "ARG018"], linked.RuleIds);
		Assert.Equal(["Run", "Again"], linked.Problems.Select(p => p.PropertyName));
		var message = linked.Diagnostics()[0].ToDiagnostic().GetMessage(CultureInfo.InvariantCulture);
		Assert.Contains("also the type of another child command property", message, StringComparison.Ordinal);
	}

	[Fact]
	public void ARG018_RootAsAChild_IsReported()
	{
		var linked = Link("""
			[CommandApp("app")]
			public partial class App
			{
				public Group? Group { get; private set; }
			}

			[Command("group")] public partial class Group { public App? Back { get; private set; } public Leaf? Leaf { get; private set; } }
			[ParameterCollection] [Command("leaf")] public partial class Leaf { }
			""");

		var problem = Assert.Single(linked.Problems);
		Assert.Equal("ARG018", problem.RuleId);
		Assert.Equal("Back", problem.PropertyName);
		Assert.Equal("Tree.Group", problem.TypeName);
		Assert.True(Assert.Single(linked.Trees).HasErrors);
	}

	[Fact]
	public void ARG018_ClassThatIsItsOwnChild_IsReportedAndLinkingEnds()
	{
		var linked = Link("""
			[CommandApp("app")]
			public partial class App { public Group? Group { get; private set; } }

			[Command("group")] public partial class Group { public Group? Again { get; private set; } public Leaf? Leaf { get; private set; } }
			[ParameterCollection] [Command("leaf")] public partial class Leaf { }
			""");

		// Group has two parents: App and itself
		Assert.Equal(["ARG018", "ARG018"], linked.RuleIds);
		Assert.Equal(3, Assert.Single(linked.Trees).Nodes.Count);
	}
	#endregion

	#region ARG019 not part of a tree

	[Fact]
	public void ARG019_CommandWithoutARoot_IsAWarningAndNoTree()
	{
		var linked = Link("[ParameterCollection] [Command(\"lonely\")] public partial class Lonely { }");

		Assert.Equal(["ARG019"], linked.RuleIds);
		Assert.Empty(linked.Trees);
		var diagnostic = Assert.Single(linked.Diagnostics());
		Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
		Assert.Equal("Lonely", linked.TextAt(diagnostic));
	}

	[Fact]
	public void ARG019_CommandThatNobodyDeclares_IsReportedNextToATreeThatIsFine()
	{
		var linked = Link("""
			[CommandApp("app")]
			public partial class App { public Leaf? Leaf { get; private set; } }

			[ParameterCollection] [Command("leaf")] public partial class Leaf { }
			[ParameterCollection] [Command("stray")] public partial class Stray { }
			""");

		var problem = Assert.Single(linked.Problems);
		Assert.Equal("ARG019", problem.RuleId);
		Assert.Equal("Tree.Stray", problem.TypeName);
		// a warning does not stop the tree from being generated
		Assert.False(Assert.Single(linked.Trees).HasErrors);
	}

	[Fact]
	public void ARG019_CycleThatNoRootReaches_IsReportedForEveryMemberAndLinkingEnds()
	{
		var linked = Link("""
			[Command("a")] public partial class A { public B? B { get; private set; } }
			[Command("b")] public partial class B { public A? A { get; private set; } }
			[Command("self")] public partial class Self { public Self? Again { get; private set; } }
			""");

		Assert.Equal(["ARG019", "ARG019", "ARG019"], linked.RuleIds);
	}
	#endregion

	#region ARG020 command group without commands

	[Fact]
	public void ARG020_GroupAndRootWithoutChildren_AreReported()
	{
		var linked = Link("""
			[CommandApp("app")]
			public partial class App { public Empty? Empty { get; private set; } public Plain? Other { get; set; } }

			[Command("empty")] public partial class Empty { public Plain? Nothing { get; set; } }
			""");

		Assert.Equal(["ARG020"], linked.RuleIds);
		Assert.Equal("Tree.Empty", Assert.Single(linked.Problems).TypeName);
		Assert.True(Assert.Single(linked.Trees).HasErrors);

		var rootOnly = Link("[CommandApp(\"app\")] public partial class App { }");
		Assert.Equal(["ARG020"], rootOnly.RuleIds);
	}

	[Fact]
	public void ARG020_LeafWithoutChildren_IsNotReported()
	{
		var linked = Link("""
			[CommandApp("app")]
			public partial class App { public Leaf? Leaf { get; private set; } }

			[ParameterCollection] [Command("leaf")] public partial class Leaf { }
			""");

		Assert.Empty(linked.Problems);
	}
	#endregion

	#region ARG021 leaf with children

	[Fact]
	public void ARG021_LeafWithAChildCommand_IsReportedOnTheProperty()
	{
		var linked = Link("""
			[CommandApp("app")]
			public partial class App { public Leaf? Leaf { get; private set; } }

			[ParameterCollection]
			[Command("leaf")]
			public partial class Leaf { public Inner? Inner { get; private set; } }

			[ParameterCollection] [Command("inner")] public partial class Inner { }
			""");

		var problem = Assert.Single(linked.Problems);
		Assert.Equal("ARG021", problem.RuleId);
		Assert.Equal("Inner", problem.PropertyName);
		Assert.Equal(["Leaf", "Inner"], problem.Arguments.AsReadOnly());
		Assert.True(Assert.Single(linked.Trees).HasErrors);
	}
	#endregion

	#region ARG022 child property cannot be set

	[Theory]
	[InlineData("{ get; }", true)]
	[InlineData("{ get; init; }", true)]
	[InlineData("{ get; private init; }", true)]
	[InlineData("{ get; private set; }", false)]
	[InlineData("{ get; set; }", false)]
	[InlineData("{ get; protected set; }", false)]
	[InlineData("{ get; internal set; }", false)]
	public void ARG022_ChildPropertyAccessors_AreCheckedForASetterTheGeneratedCodeCanCall(string accessors, bool reported)
	{
		var linked = Link($$"""
			[CommandApp("app")]
			public partial class App { public Leaf? Leaf {{accessors}} }

			[ParameterCollection] [Command("leaf")] public partial class Leaf { }
			""");

		Assert.Equal(reported ? new[] { "ARG022" } : Array.Empty<string>(), linked.RuleIds);
		Assert.Equal(reported, Assert.Single(linked.Trees).HasErrors);
	}

	[Fact]
	public void ARG022_IsReportedOnTheProperty()
	{
		var linked = Link("""
			[CommandApp("app")]
			public partial class App { public Leaf? Leaf { get; } }

			[ParameterCollection] [Command("leaf")] public partial class Leaf { }
			""");

		var diagnostic = Assert.Single(linked.Diagnostics());
		Assert.Equal("ARG022", diagnostic.Id);
		Assert.Equal("Leaf", linked.TextAt(diagnostic));
	}
	#endregion

	#region ARG027 argument name used by a parent command

	[Fact]
	public void ARG027_LeafReusingANameOfTheRoot_IsReportedOnTheLeafPropertyForEachName()
	{
		var linked = Link("""
			[CommandApp("app")]
			public partial class App
			{
				[Flag("v", "verbose", "root")] public bool Verbose { get; set; }
				public Run? Run { get; private set; }
			}

			[ParameterCollection]
			[Command("run")]
			public partial class Run
			{
				[Flag("v", "verbose", "leaf")] public bool Loud { get; set; }
			}
			""");

		Assert.Equal(["ARG027", "ARG027"], linked.RuleIds);
		Assert.All(linked.Problems, p => Assert.Equal(("Tree.Run", "Loud"), (p.TypeName, p.PropertyName)));
		Assert.True(Assert.Single(linked.Trees).HasErrors);
		var diagnostics = linked.Diagnostics();
		Assert.All(diagnostics, d => Assert.Equal("[Flag(\"v\", \"verbose\", \"leaf\")] public bool Loud { get; set; }", linked.TextAt(d)));
		var messages = diagnostics.Select(d => d.ToDiagnostic().GetMessage(CultureInfo.InvariantCulture)).ToList();
		Assert.Contains(messages, m => m.Contains("Property 'Loud' of 'Run' has the short name 'v', which 'App' above it", StringComparison.Ordinal));
		Assert.Contains(messages, m => m.Contains("the long name 'verbose'", StringComparison.Ordinal));
	}

	[Fact]
	public void ARG027_NameUsedTwoLevelsUp_IsReportedWithTheNearestCommandThatUsesIt()
	{
		var linked = Link("""
			[CommandApp("app")]
			public partial class App
			{
				[Option("d", "db", "root")] public string Db { get; set; } = "";
				[Option("c", "config", "root")] public string Config { get; set; } = "";
				public Group? Group { get; private set; }
			}

			[Command("group")]
			public partial class Group
			{
				[Option("x", "db", "group")] public string Database { get; set; } = "";
				public Leaf? Leaf { get; private set; }
			}

			[ParameterCollection]
			[Command("leaf")]
			public partial class Leaf
			{
				[Option("y", "db", "leaf")] public string Db { get; set; } = "";
				[Flag("c", "", "leaf")] public bool Clean { get; set; }
			}
			""");

		var problems = linked.Problems.Select(p => (p.TypeName, p.PropertyName, Owner: p.Arguments[4])).ToList();
		Assert.Equal(
			[("Tree.Group", "Database", "App"), ("Tree.Leaf", "Db", "Group"), ("Tree.Leaf", "Clean", "App")],
			problems);
	}

	[Fact]
	public void ARG027_SiblingsAndCommandsInOtherBranches_MayShareNames()
	{
		var linked = Link("""
			[CommandApp("app")]
			public partial class App
			{
				public Build? Build { get; private set; }
				public Group? Group { get; private set; }
			}

			[ParameterCollection]
			[Command("build")]
			public partial class Build
			{
				[Flag("v", "verbose", "build")] public bool Verbose { get; set; }
			}

			[Command("group")]
			public partial class Group
			{
				[Flag("v", "verbose", "group")] public bool Verbose { get; set; }
				public Test? Test { get; private set; }
			}

			[ParameterCollection]
			[Command("test")]
			public partial class Test
			{
				[Option("o", "out", "test")] public string Out { get; set; } = "";
			}
			""");

		Assert.Empty(linked.RuleIds);
	}

	[Theory]
	[InlineData("[Option(\"\", \"out\", \"root\")]", "[Option(\"\", \"other\", \"leaf\")]", false)]
	[InlineData("[Option(\"v\", \"\", \"root\")]", "[Option(\"x\", \"v\", \"leaf\")]", false)]
	[InlineData("[Option(\"o\", \"out\", \"root\")]", "[Flag(\"o\", \"other\", \"leaf\")]", true)]
	[InlineData("[Option(\"o\", \"out\", \"root\")]", "[Option(\"O\", \"Out\", \"leaf\")]", false)]
	public void ARG027_NamesAreComparedByKindAndExactly(string rootArgument, string leafArgument, bool reported)
	{
		var leafType = leafArgument.StartsWith("[Flag", StringComparison.Ordinal) ? "bool" : "string";
		var linked = Link($$"""
			[CommandApp("app")]
			public partial class App
			{
				{{rootArgument}} public string Root { get; set; } = "";
				public Leaf? Leaf { get; private set; }
			}

			[ParameterCollection]
			[Command("leaf")]
			public partial class Leaf
			{
				{{leafArgument}} public {{leafType}} Value { get; set; }
			}
			""");

		Assert.Equal(reported ? new[] { "ARG027" } : Array.Empty<string>(), linked.RuleIds);
	}
	#endregion

	#region errors of the nodes

	[Fact]
	public void NodeWithAnError_MarksItsTreeAsHavingErrors()
	{
		var linked = Link("""
			[CommandApp("app")]
			public partial class App { public Leaf? Leaf { get; private set; } }

			[ParameterCollection] [Command("bad name")] public partial class Leaf { }
			""");

		Assert.Empty(linked.Problems);
		Assert.True(Assert.Single(linked.Trees).HasErrors);
	}

	[Fact]
	public void NodeWithAnErrorInAnotherTree_DoesNotMarkThisTree()
	{
		var linked = Link("""
			[CommandApp("one")]
			public partial class One { public OneLeaf? Leaf { get; private set; } }

			[CommandApp("two")]
			public partial class Two { public TwoLeaf? Leaf { get; private set; } }

			[ParameterCollection] [Command("a")] public partial class OneLeaf { }
			[ParameterCollection] [Command("bad name")] public partial class TwoLeaf { }
			""");

		Assert.Equal([false, true], linked.Trees.Select(t => t.HasErrors));
	}

	[Fact]
	public void DuplicateOptionNamesOnAGroup_AreReportedByTheNode()
	{
		var linked = Link("""
			[Command("group")]
			public partial class Group
			{
				[Option("o", "one", "first")] public string One { get; set; } = "";
				[Option("o", "two", "second")] public string Two { get; set; } = "";
				public Leaf? Leaf { get; private set; }
			}

			[ParameterCollection] [Command("leaf")] public partial class Leaf { }
			""");

		var group = linked.Models.Single(m => m.Node.ClassName == "Group");
		Assert.Equal(["ARG003", "ARG003"], group.Diagnostics.AsReadOnly().Select(item => item.Id));
		Assert.True(group.HasErrors);
	}
	#endregion

	#region declaration shapes

	[Fact]
	public void InheritedChildProperty_IsNotSeen_SoTheGroupHasNoCommands()
	{
		// documented limitation: only what the class declares itself counts (see ARG020 and the attribute docs)
		var linked = Link("""
			public class AppBase { public Leaf? Leaf { get; protected set; } }

			[CommandApp("app")]
			public partial class App : AppBase { }

			[ParameterCollection] [Command("leaf")] public partial class Leaf { }
			""");

		Assert.Equal(["ARG019", "ARG020"], linked.RuleIds.OrderBy(id => id, StringComparer.Ordinal));
	}

	[Fact]
	public void NestedNodes_AreLinkedByTheirQualifiedType()
	{
		var linked = Link("""
			[CommandApp("app")]
			public partial class App { public Group? Group { get; private set; } }

			[Command("group")]
			public partial class Group
			{
				public Leaf? Leaf { get; private set; }

				[ParameterCollection]
				[Command("leaf")]
				public partial class Leaf { }
			}
			""");

		Assert.Empty(linked.Problems);
		var tree = Assert.Single(linked.Trees);
		Assert.Equal(["Tree.App", "Tree.Group", "Tree.Group.Leaf"], linked.NodesOf(tree).Select(n => n.Source.Node.TypeName));
	}
	#endregion

	#region robustness of the pipeline

	[Theory]
	[InlineData("[CommandApp(\"app\")] public partial class App<T> { public Leaf? Leaf { get; private set; } }")]
	[InlineData("[CommandApp(\"app\")] public abstract partial class App { public Leaf? Leaf { get; private set; } }")]
	[InlineData("[CommandApp(\"app\")] public partial class App { public required Leaf Leaf { get; set; } }")]
	public void Pipeline_ClassDeclaredInAWayThatCannotWork_IsReportedAndDoesNotCrashTheGenerator(string root)
	{
		var (diagnostics, _) = TestHelper.GetGeneratedOutput(Preamble + root + "\n[ParameterCollection] [Command(\"leaf\")] public partial class Leaf { }");

		Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
		Assert.Contains(diagnostics, d => d.Id is "ARG023" or "ARG024" or "ARG025");
	}

	[Fact]
	public void Pipeline_GenericGroup_IsReportedOnTheGroup()
	{
		var ids = PipelineIds("""
			[CommandApp("app")]
			public partial class App { public Group<int>? Group { get; private set; } }

			[Command("group")]
			public partial class Group<T> { public Leaf? Leaf { get; private set; } }

			[ParameterCollection] [Command("leaf")] public partial class Leaf { }
			""");

		Assert.Contains("ARG023", ids);
	}
	#endregion

	#region the generator pipeline

	private static IReadOnlyList<string> PipelineIds(string types)
	{
		var (diagnostics, _) = TestHelper.GetGeneratedOutput(Preamble + types);
		return diagnostics.Where(d => d.Id.StartsWith("ARG", StringComparison.Ordinal)).Select(d => d.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
	}

	[Fact]
	public void Pipeline_ValidTree_ReportsNothing()
	{
		Assert.Empty(PipelineIds(CaloriesTree));
	}

	[Fact]
	public void Pipeline_ReportsTheRulesOfTheTreeAsDiagnostics()
	{
		var ids = PipelineIds("""
			[CommandApp("app")]
			public partial class App
			{
				public One? First { get; private set; }
				public Two? Second { get; }
			}

			[ParameterCollection] [Command("same")] public partial class One { }
			[ParameterCollection] [Command("same")] public partial class Two { }
			[ParameterCollection] [Command("stray")] public partial class Stray { }
			""");

		Assert.Equal(["ARG017", "ARG017", "ARG019", "ARG022"], ids);
	}

	[Fact]
	public void Pipeline_LinkDiagnosticsPointAtTheSourceText()
	{
		var source = Preamble + """
			[CommandApp("app")]
			public partial class App { public Leaf? Leaf { get; } }

			[ParameterCollection] [Command("leaf")] public partial class Leaf { }
			""";
		var (diagnostics, _) = TestHelper.GetGeneratedOutput(source);

		var diagnostic = Assert.Single(diagnostics, d => d.Id == "ARG022");
		var span = diagnostic.Location.SourceSpan;
		Assert.Equal("Leaf", source.Substring(span.Start, span.Length));
		Assert.Contains("'Leaf'", diagnostic.GetMessage());
	}
	#endregion
}
