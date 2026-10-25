using ArgumentParser.Internal.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArgumentParser.Tests;

/// <summary>
/// Tests for reading the place of a class in a tree of commands (CommandNodeExtractor), the value semantics of the
/// node types, and the rules that can be checked on a single node.
/// </summary>
public class CommandNodeExtractionTests
{
	private const string Preamble = """
		using System;
		using ArgumentParser;

		namespace Tree;

		public enum Colour { Red, Green }
		public class Plain { }
		public static class Names { public const string Add = "add"; }

		""";

	private static CommandNodeModel Extract(string types, string className, string preamble = Preamble)
	{
		var compilation = TestHelper.CreateCompilation(preamble + types);
		var tree = compilation.SyntaxTrees.Single();
		var semanticModel = compilation.GetSemanticModel(tree);
		var declaration = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First(c => c.Identifier.Text == className);
		return CommandNodeExtractor.Extract(declaration, (INamedTypeSymbol)semanticModel.GetDeclaredSymbol(declaration)!);
	}

	private static IReadOnlyList<string> Ids(CommandNodeModel model)
	{
		return Enumerable.Range(0, model.Diagnostics.Count).Select(i => model.Diagnostics[i].Id).ToList();
	}

	private static IReadOnlyList<ChildCandidate> Children(CommandNodeModel model)
	{
		return Enumerable.Range(0, model.Node.Children.Count).Select(i => model.Node.Children[i]).ToList();
	}

	#region Reading the node

	[Fact]
	public void Root_ReadsNameDescriptionAndIdentity()
	{
		var model = Extract("""
			[CommandApp("calories", "Track what you eat")]
			public partial class CaloriesApp { }
			""", "CaloriesApp");

		var node = model.Node;
		Assert.Equal(CommandNodeKind.Root, node.Kind);
		Assert.Equal("calories", node.Name);
		Assert.Equal("Track what you eat", node.Description);
		Assert.Equal("CaloriesApp", node.ClassName);
		Assert.Equal("Tree", node.NamespaceName);
		Assert.Equal("Tree.CaloriesApp", node.TypeName);
		Assert.Equal(0, node.Annotations);
		Assert.Empty(Ids(model));
	}

	[Fact]
	public void Group_IsACommandWithoutParameterCollection()
	{
		var model = Extract("""
			[Command("ingredient", "Manage ingredients")]
			public partial class IngredientGroup { }
			""", "IngredientGroup");

		Assert.Equal(CommandNodeKind.Group, model.Node.Kind);
		Assert.Equal("ingredient", model.Node.Name);
		Assert.Equal("Manage ingredients", model.Node.Description);
		Assert.Empty(Ids(model));
	}

	[Fact]
	public void Leaf_IsACommandWithParameterCollection()
	{
		var model = Extract("""
			[ParameterCollection]
			[Command("add", "Add an ingredient")]
			public partial class IngredientAdd { }
			""", "IngredientAdd");

		Assert.Equal(CommandNodeKind.Leaf, model.Node.Kind);
		Assert.Equal("add", model.Node.Name);
		Assert.Empty(Ids(model));
	}

	[Fact]
	public void Leaf_ReadsTheSameWhicheverOrderTheAttributesAreIn()
	{
		var first = Extract("[ParameterCollection] [Command(\"add\")] public partial class Leaf { }", "Leaf");
		var second = Extract("[Command(\"add\")] [ParameterCollection] public partial class Leaf { }", "Leaf");

		Assert.Equal(first.Node, second.Node);
	}

	[Fact]
	public void DescriptionIsEmptyWhenOmitted()
	{
		var model = Extract("[Command(\"add\")] public partial class Group { }", "Group");

		Assert.Equal("", model.Node.Description);
	}

	[Fact]
	public void Annotations_AreReadAsTheirNumericValue()
	{
		var combined = Extract("[Command(\"x\", \"d\", Annotations = CommandAnnotations.ReadOnly | CommandAnnotations.Idempotent)] public partial class Group { }", "Group");
		var single = Extract("[Command(\"x\", Annotations = CommandAnnotations.Destructive)] public partial class Group { }", "Group");
		var none = Extract("[Command(\"x\")] public partial class Group { }", "Group");

		Assert.Equal((int)(CommandAnnotations.ReadOnly | CommandAnnotations.Idempotent), combined.Node.Annotations);
		Assert.Equal((int)CommandAnnotations.Destructive, single.Node.Annotations);
		Assert.Equal(0, none.Node.Annotations);
	}

	[Fact]
	public void NamesAndDescriptions_AreReadFromConstants()
	{
		var model = Extract("[Command(Names.Add, Names.Add + \" it\")] public partial class Group { }", "Group");

		Assert.Equal("add", model.Node.Name);
		Assert.Equal("add it", model.Node.Description);
	}

	[Fact]
	public void ClassInTheGlobalNamespace_HasNoNamespace()
	{
		var model = Extract("using ArgumentParser;\n[Command(\"x\")] public partial class Group { }", "Group", preamble: "");

		Assert.Null(model.Node.NamespaceName);
		Assert.Equal("Group", model.Node.TypeName);
		Assert.Empty(Ids(model));
	}

	[Fact]
	public void ClassLocation_PointsAtTheClassName()
	{
		var source = Preamble + "[Command(\"x\")]\npublic partial class Group { }";
		var model = Extract("[Command(\"x\")]\npublic partial class Group { }", "Group");

		var line = source.Split('\n').ToList().FindIndex(l => l.Contains("partial class Group", StringComparison.Ordinal));
		Assert.Equal(line, model.ClassLocation!.LineSpan.Start.Line);
	}

	#endregion

	#region Child candidates

	[Fact]
	public void Children_AreThePropertiesWithAClassTypeDeclaredInSource()
	{
		var model = Extract("""
			public class Child { }

			[CommandApp("calories")]
			public partial class CaloriesApp
			{
				public Child Ingredient { get; private set; }
				public Child Meal { get; set; }
				public Child ReadOnly { get; }
				public Child InitOnly { get; init; }
				public Plain Other { get; set; }
				[Option("o", "opt", "an option")]
				public Child WithArgument { get; set; }

				public string Scratch { get; set; }
				public Uri Site { get; set; }
				public Colour Level { get; set; }
				public int Count { get; set; }
				public int[] Numbers { get; set; }
				public static Child Shared { get; set; }
				public Child this[int index] => null;
			}
			""", "CaloriesApp");

		var children = Children(model);

		Assert.Equal(
			["Ingredient", "Meal", "ReadOnly", "InitOnly", "Other", "WithArgument"],
			children.Select(c => c.PropertyName));
		Assert.Equal(
			["Tree.Child", "Tree.Child", "Tree.Child", "Tree.Child", "Tree.Plain", "Tree.Child"],
			children.Select(c => c.TypeName));
		Assert.Equal([true, true, false, false, true, true], children.Select(c => c.HasSetter));
		Assert.Equal([false, false, false, false, false, true], children.Select(c => c.HasArgumentAttribute));
	}

	[Fact]
	public void Children_HaveTheirTypeWithoutANullableAnnotation()
	{
		var model = Extract("""
			#nullable enable
			public class Child { }

			[CommandApp("calories")]
			public partial class CaloriesApp
			{
				public Child? Annotated { get; private set; }
				public Child Plain { get; private set; } = new();
			}
			""", "CaloriesApp");

		Assert.Equal(["Tree.Child", "Tree.Child"], Children(model).Select(c => c.TypeName));
	}

	[Fact]
	public void Children_IncludeThePropertiesOfOtherPartsOfThePartialClass()
	{
		var model = Extract("""
			public class Child { }

			[CommandApp("calories")]
			public partial class CaloriesApp
			{
				public Child First { get; private set; }
			}

			public partial class CaloriesApp
			{
				public Child Second { get; private set; }
			}
			""", "CaloriesApp");

		Assert.Equal(["First", "Second"], Children(model).Select(c => c.PropertyName));
	}

	[Fact]
	public void ChildLocations_FollowTheChildrenAndPointAtTheirProperties()
	{
		const string types = """
			public class Child { }

			[CommandApp("calories")]
			public partial class CaloriesApp
			{
				public Child Ingredient { get; private set; }
				public Child Meal { get; private set; }
			}
			""";
		var model = Extract(types, "CaloriesApp");

		var lines = (Preamble + types).Split('\n').ToList();
		Assert.Equal(["Ingredient", "Meal"], Enumerable.Range(0, model.ChildLocations.Count).Select(i => model.ChildLocations[i].PropertyName));
		Assert.Equal(
			lines.FindIndex(l => l.Contains("Ingredient {", StringComparison.Ordinal)),
			model.ChildLocations[0].Location!.LineSpan.Start.Line);
		Assert.Equal(
			lines.FindIndex(l => l.Contains("Meal {", StringComparison.Ordinal)),
			model.ChildLocations[1].Location!.LineSpan.Start.Line);
	}

	[Fact]
	public void LeafAndGroup_AlsoListTheirChildCandidates()
	{
		var leaf = Extract("""
			public class Child { }

			[ParameterCollection]
			[Command("add")]
			public partial class Leaf
			{
				public Child Oops { get; private set; }
			}
			""", "Leaf");

		Assert.Equal(["Oops"], Children(leaf).Select(c => c.PropertyName));
	}

	#endregion

	#region Value semantics

	private const string Group = """
		public class Child { }

		[Command("ingredient", "Manage ingredients")]
		public partial class IngredientGroup
		{
			[Option("d", "db", "the database")]
			public string Db { get; set; }

			public Child Add { get; private set; }
		}
		""";

	[Fact]
	public void TheSameSource_GivesEqualModelsWithEqualHashes()
	{
		var first = Extract(Group, "IngredientGroup");
		var second = Extract(Group, "IngredientGroup");

		Assert.Equal(first, second);
		Assert.Equal(first.GetHashCode(), second.GetHashCode());
		Assert.Equal(first.Node, second.Node);
		Assert.Equal(first.Node.GetHashCode(), second.Node.GetHashCode());
	}

	[Fact]
	public void EditingAnOption_DoesNotChangeTheNode_NorTheModelWhenNothingMoves()
	{
		var original = Extract(Group, "IngredientGroup");

		// the new text is as long as the old one, so nothing after it moves
		var edited = Extract(Group.Replace("the database", "new database"), "IngredientGroup");

		Assert.Equal(original, edited);
	}

	[Fact]
	public void AddingAnOptionAboveAChild_MovesItsLocationButDoesNotChangeTheNode()
	{
		var original = Extract(Group, "IngredientGroup");

		var edited = Extract(Group.Replace(
			"[Option(\"d\", \"db\", \"the database\")]",
			"[Flag(\"v\", \"verbose\", \"talk\")]\n\tpublic bool Verbose { get; set; }\n\t[Option(\"d\", \"db\", \"the database\")]"), "IngredientGroup");

		// the tree is linked from the nodes, so it has no reason to link again; only the diagnostics' locations moved
		Assert.Equal(original.Node, edited.Node);
		Assert.Equal(original.Node.GetHashCode(), edited.Node.GetHashCode());
		Assert.NotEqual(original, edited);
	}

	[Theory]
	[InlineData("[Command(\"ingredient\", \"Manage ingredients\")]", "[Command(\"ingredients\", \"Manage ingredients\")]")]
	[InlineData("[Command(\"ingredient\", \"Manage ingredients\")]", "[Command(\"ingredient\", \"Manage them\")]")]
	[InlineData("[Command(\"ingredient\", \"Manage ingredients\")]", "[Command(\"ingredient\", \"Manage ingredients\", Annotations = CommandAnnotations.ReadOnly)]")]
	[InlineData("[Command(\"ingredient\", \"Manage ingredients\")]", "[ParameterCollection] [Command(\"ingredient\", \"Manage ingredients\")]")]
	[InlineData("public Child Add { get; private set; }", "public Child Remove { get; private set; }")]
	[InlineData("public Child Add { get; private set; }", "public Plain Add { get; private set; }")]
	[InlineData("public Child Add { get; private set; }", "public Child Add { get; }")]
	public void ChangesToTheTreeFacts_ChangeTheNode(string from, string to)
	{
		var original = Extract(Group, "IngredientGroup");
		var edited = Extract(Group.Replace(from, to), "IngredientGroup");

		Assert.NotEqual(original.Node, edited.Node);
		Assert.NotEqual(original, edited);
	}

	#endregion

	#region Rules that can be checked on one node

	[Theory]
	[InlineData("")]
	[InlineData("add item")]
	[InlineData(" add")]
	[InlineData("add\\t")]
	[InlineData("-add")]
	[InlineData("--add")]
	public void ARG014_InvalidCommandName_IsReported(string name)
	{
		var model = Extract($"[Command(\"{name}\")] public partial class Group {{ }}", "Group");

		var diagnostic = Assert.Single(Enumerable.Range(0, model.Diagnostics.Count).Select(i => model.Diagnostics[i]));
		Assert.Equal("ARG014", diagnostic.Id);
		Assert.Contains("'Group'", diagnostic.ToDiagnostic().GetMessage(), StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("add")]
	[InlineData("a-b")]
	[InlineData("x_y")]
	[InlineData("add2")]
	[InlineData("é")]
	public void ARG014_ValidCommandName_IsNotReported(string name)
	{
		var model = Extract($"[Command(\"{name}\")] public partial class Group {{ }}", "Group");

		Assert.Empty(Ids(model));
	}

	[Fact]
	public void ARG014_AlsoAppliesToLeaves()
	{
		var model = Extract("[ParameterCollection] [Command(\"bad name\")] public partial class Leaf { }", "Leaf");

		Assert.Equal(["ARG014"], Ids(model));
	}

	[Theory]
	[InlineData("My Application")]
	[InlineData("")]
	public void ARG014_TheNameOfTheApplicationIsNotHeldToTheRules(string name)
	{
		var model = Extract($"[CommandApp(\"{name}\")] public partial class App {{ }}", "App");

		Assert.Empty(Ids(model));
	}

	[Fact]
	public void ARG014_PointsAtTheAttribute()
	{
		var types = "[Command(\"bad name\")]\npublic partial class Group { }";
		var model = Extract(types, "Group");

		var location = model.Diagnostics[0].Location!;
		var line = (Preamble + types).Split('\n').ToList().FindIndex(l => l.Contains("[Command(\"bad name\")]", StringComparison.Ordinal));
		Assert.Equal(line, location.LineSpan.Start.Line);
	}

	[Theory]
	[InlineData("[Command(\"group\")]")]
	[InlineData("[CommandApp(\"app\")]")]
	public void ARG015_PositionalOnAGroupOrRoot_IsReportedPerProperty(string attribute)
	{
		var model = Extract($$"""
			{{attribute}}
			public partial class Node
			{
				[Positional(0, "first")] public string First { get; set; }
				[Positional(1, "second")] public int Second { get; set; }
				[Option("o", "opt", "fine")] public string Option { get; set; }
			}
			""", "Node");

		Assert.Equal(["ARG015", "ARG015"], Ids(model));
		var messages = Enumerable.Range(0, model.Diagnostics.Count).Select(i => model.Diagnostics[i].ToDiagnostic().GetMessage()).ToList();
		Assert.Contains("'First'", messages[0], StringComparison.Ordinal);
		Assert.Contains("'Second'", messages[1], StringComparison.Ordinal);
		Assert.Contains("'Node'", messages[0], StringComparison.Ordinal);
	}

	[Fact]
	public void ARG015_PositionalOnALeaf_IsFine()
	{
		var model = Extract("""
			[ParameterCollection]
			[Command("update")]
			public partial class Leaf
			{
				[Positional(0, "Id")] public int Id { get; set; }
			}
			""", "Leaf");

		Assert.Empty(Ids(model));
	}

	[Theory]
	[InlineData("[CommandApp(\"app\")] [Command(\"x\")]", "Command")]
	[InlineData("[CommandApp(\"app\")] [ParameterCollection]", "ParameterCollection")]
	public void ARG016_RootCombinedWithAnotherNodeAttribute_IsReportedOnTheRoot(string attributes, string other)
	{
		var model = Extract($"{attributes} public partial class App {{ }}", "App");

		Assert.Equal(CommandNodeKind.Root, model.Node.Kind);
		var diagnostic = Assert.Single(Enumerable.Range(0, model.Diagnostics.Count).Select(i => model.Diagnostics[i]));
		Assert.Equal("ARG016", diagnostic.Id);
		Assert.Contains($"{other} attribute", diagnostic.ToDiagnostic().GetMessage(), StringComparison.Ordinal);
	}

	[Fact]
	public void ARG016_RootCombinedWithBoth_ReportsEach()
	{
		var model = Extract("[CommandApp(\"app\")] [Command(\"x\")] [ParameterCollection] public partial class App { }", "App");

		Assert.Equal(["ARG016", "ARG016"], Ids(model));
	}

	[Fact]
	public void GroupAndRoot_MustBePartialAndHaveAParameterlessConstructor()
	{
		var notPartial = Extract("[Command(\"x\")] public class Group { }", "Group");
		var notPartialRoot = Extract("[CommandApp(\"x\")] public class App { }", "App");
		var constructor = Extract("[Command(\"x\")] public partial class Group { public Group(int a) { } }", "Group");

		Assert.Equal(["ARG001"], Ids(notPartial));
		Assert.Equal(["ARG001"], Ids(notPartialRoot));
		Assert.Equal(["ARG006"], Ids(constructor));
		Assert.True(notPartial.HasErrors);
	}

	[Fact]
	public void Leaf_LeavesTheClassRulesToTheParameterCollection()
	{
		// the ParameterCollection pipeline reports these for a leaf, reporting them here as well would show them twice
		var model = Extract("[ParameterCollection] [Command(\"x\")] public class Leaf { public Leaf(int a) { } }", "Leaf");

		Assert.Empty(Ids(model));
	}

	[Fact]
	public void OnlyTheRoot_GetsGeneratedAParseMethod_SoOnlyTheRootMayDeclareOne()
	{
		const string parse = "public static int Parse(string[] args) => 0;";
		var root = Extract($"[CommandApp(\"x\")] public partial class App {{ {parse} }}", "App");
		var group = Extract($"[Command(\"x\")] public partial class Group {{ {parse} }}", "Group");

		Assert.Equal(["ARG002"], Ids(root));
		Assert.Empty(Ids(group));
	}

	#endregion
}
