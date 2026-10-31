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
		return CommandNodeExtractor.Extract(declaration, (INamedTypeSymbol)semanticModel.GetDeclaredSymbol(declaration)!, semanticModel);
	}

	private static IReadOnlyList<string> Ids(CommandNodeModel model)
	{
		return model.Diagnostics.AsReadOnly().Select(item => item.Id).ToList();
	}

	private static IReadOnlyList<ChildCandidate> Children(CommandNodeModel model)
	{
		return model.Node.Children.AsReadOnly().ToList();
	}

	#region Arguments of groups and roots

	[Fact]
	public void Group_ReadsItsOptionsAndFlags()
	{
		var model = Extract("""
			public enum Level { None, Some }

			[Command("group")]
			public partial class Group
			{
				[Option("d", "db", "the database")] public string Db { get; set; } = "";
				[Option("", "level", "how much", true)] public Level Level { get; set; }
				[Flag("v", "verbose", "talk")] public bool Verbose { get; set; }
				public string Ignored { get; set; } = "";
			}
			""", "Group");

		Assert.Equal(["Db", "Level"], model.Arguments.Options.AsReadOnly().Select(item => item.PropertyName));
		Assert.Equal(["Verbose"], model.Arguments.Flags.AsReadOnly().Select(item => item.PropertyName));
		Assert.True(model.Arguments.Options[1].Attribute.Required);
		Assert.True(model.Arguments.Options[1].IsEnum);
		Assert.Equal("the database", model.Arguments.Options[0].Attribute.Description);
	}

	[Fact]
	public void Root_ReadsItsOptionsAndFlags()
	{
		var model = Extract("""
			[CommandApp("app")]
			public partial class App
			{
				[Option("d", "db", "the database")] public string Db { get; set; } = "";
				[Flag("v", "verbose", "talk")] public bool Verbose { get; set; }
			}
			""", "App");

		Assert.Equal(1, model.Arguments.Options.Count);
		Assert.Equal(1, model.Arguments.Flags.Count);
	}

	[Fact]
	public void Leaf_ReadsItsOptionsAndFlagsButLeavesTheirRulesToTheParameterCollectionPipeline()
	{
		var model = Extract("""
			[ParameterCollection]
			[Command("leaf")]
			public partial class Leaf
			{
				[Option("d", "db", "the database")] public string Db { get; set; } = "";
				[Option("d", "other", "the same short name")] public string Other { get; set; } = "";
				[Flag("v", "verbose", "more output")] public bool Verbose { get; set; }
				[Positional(0, "a file")] public string File { get; set; } = "";
			}
			""", "Leaf");

		// the names are compared along the tree (ARG027); the duplicate (ARG003) is reported by the other pipeline
		Assert.Equal(["Db", "Other"], model.Arguments.Options.AsReadOnly().Select(o => o.PropertyName));
		Assert.Equal(["Verbose"], model.Arguments.Flags.AsReadOnly().Select(f => f.PropertyName));
		// the positionals of a leaf have locations too, for a key of the JSON Schema that a command above uses (ARG034)
		Assert.Equal(["Db", "Other", "Verbose", "File"], model.ArgumentLocations.AsReadOnly().Select(l => l.PropertyName));
		Assert.Empty(Ids(model));
	}

	[Fact]
	public void Settings_AreThoseOfTheCommandAppForTheRootAndOfTheParameterCollectionForALeaf()
	{
		const string types = """
			[CommandApp("app", "An app", HelpTextGeneration.GenerateTextOnly, "?", "info", BehaviourOnError.ThrowNever)]
			public partial class App { public Group? Group { get; private set; } }

			[Command("group")]
			public partial class Group { public Leaf? Leaf { get; private set; } }

			[ParameterCollection(HelpTextGeneration.None, "x", "", BehaviourOnError.ThrowIfAnyError)]
			[Command("leaf")]
			public partial class Leaf { }

			[ParameterCollection]
			[Command("plain")]
			public partial class Plain2 { }
			""";

		Assert.Equal(new NodeSettings(2, "?", "info", 2), Extract(types, "App").Node.Settings);
		Assert.Equal(NodeSettings.Default, Extract(types, "Group").Node.Settings);
		Assert.Equal(new NodeSettings(1, "x", "", 1), Extract(types, "Leaf").Node.Settings);
		Assert.Equal(NodeSettings.Default, Extract(types, "Plain2").Node.Settings);
		Assert.Equal(NodeSettings.Default, Extract("[CommandApp(\"app\")] public partial class App { }", "App").Node.Settings);
	}

	[Fact]
	public void Settings_UseCurrentCulture_IsReadForTheRootAndALeaf()
	{
		const string types = """
			[CommandApp("app", UseCurrentCulture = true)]
			public partial class App { public Group? Group { get; private set; } }

			[Command("group")]
			public partial class Group { public Leaf? Leaf { get; private set; } }

			[ParameterCollection(UseCurrentCulture = true)]
			[Command("leaf")]
			public partial class Leaf { }
			""";

		Assert.Equal(new NodeSettings(0, "h", "Help", 0, useCurrentCulture: true), Extract(types, "App").Node.Settings);
		Assert.Equal(NodeSettings.Default, Extract(types, "Group").Node.Settings);
		Assert.Equal(new NodeSettings(0, "h", "Help", 0, useCurrentCulture: true), Extract(types, "Leaf").Node.Settings);
	}

	[Fact]
	public void Settings_ToolSettings_AreReadForTheRootAndALeaf()
	{
		const string types = """
			[CommandApp("app", EmitMcpManifest = true)]
			public partial class App { public Group? Group { get; private set; } }

			[Command("group")]
			public partial class Group { public Leaf? Leaf { get; private set; } }

			[ParameterCollection(EmitJsonBinder = true)]
			[Command("leaf")]
			public partial class Leaf { }
			""";

		Assert.Equal(new NodeSettings(0, "h", "Help", 0, emitMcpManifest: true), Extract(types, "App").Node.Settings);
		Assert.Equal(NodeSettings.Default, Extract(types, "Group").Node.Settings);
		Assert.Equal(new NodeSettings(0, "h", "Help", 0, emitJsonBinder: true), Extract(types, "Leaf").Node.Settings);
	}

	[Fact]
	public void Leaf_PositionalsHaveLocations_SoARuleAboutOneCanPointAtIt()
	{
		var model = Extract("""
			[ParameterCollection]
			[Command("leaf")]
			public partial class Leaf
			{
				[Positional(0, "first")] public string First { get; set; } = "";
				[Option("o", "out", "out")] public string Out { get; set; } = "";
			}
			""", "Leaf");

		Assert.Equal(["Out", "First"], model.ArgumentLocations.AsReadOnly().Select(l => l.PropertyName));
	}

	[Theory]
	[InlineData("[CommandApp(\"app\")]", "App")]
	[InlineData("[Command(\"group\")]", "Group")]
	public void ARG033_RequiredOptionOfTheRootOrAGroupThatIsExcluded_IsReported(string attribute, string className)
	{
		var model = Extract($$"""
			{{attribute}}
			public partial class {{className}}
			{
				[Option("", "db", "Path", true), ExcludeFromManifest] public string Db { get; set; } = "";
				[Option("", "log", "Log", true)] public string Log { get; set; } = "";
				[Option("", "tmp", "Temp"), ExcludeFromManifest] public string Temp { get; set; } = "";
				public Leaf? Leaf { get; private set; }
			}

			[ParameterCollection] [Command("leaf")] public partial class Leaf { }
			""", className);

		Assert.Equal(["ARG033"], Ids(model));
	}

	[Fact]
	public void Leaf_KeepsItsPositionalsForItsHelpText()
	{
		var model = Extract("""
			[ParameterCollection]
			[Command("leaf")]
			public partial class Leaf
			{
				[Positional(1, "second")] public string Second { get; set; } = "";
				[Positional(0, "first")] public string First { get; set; } = "";
			}
			""", "Leaf");

		Assert.Equal(["Second", "First"], model.Arguments.Positionals.AsReadOnly().Select(p => p.PropertyName));
	}

	[Fact]
	public void Group_PositionalsAreNotReadAsArguments()
	{
		var model = Extract("""
			[Command("group")]
			public partial class Group
			{
				[Positional(0, "first")] public string First { get; set; } = "";
			}
			""", "Group");

		Assert.Equal(NodeArguments.Empty, model.Arguments);
		Assert.Equal(["ARG015"], Ids(model));
	}

	[Fact]
	public void Group_PositionalOfACommandClassType_IsReportedAsThatAndNotAsAPositional()
	{
		var model = Extract("""
			[Command("group")]
			public partial class Group
			{
				[Positional(0, "first")] public Leaf? First { get; set; }
			}

			[ParameterCollection] [Command("leaf")] public partial class Leaf { }
			""", "Group");

		Assert.Equal(["ARG026"], Ids(model));
	}

	[Theory]
	[InlineData("[Option(\"o\", \"one\", \"a\")] public string A { get; set; } = \"\"; [Option(\"o\", \"two\", \"b\")] public string B { get; set; } = \"\";", "ARG003")]
	[InlineData("[Option(\"a\", \"same\", \"a\")] public string A { get; set; } = \"\"; [Flag(\"b\", \"same\", \"b\")] public bool B { get; set; }", "ARG003")]
	[InlineData("[Option(\"a\", \"one\", \"a\")] public Plain A { get; set; } = new();", "ARG007")]
	[InlineData("[Flag(\"a\", \"one\", \"a\")] public string A { get; set; } = \"\";", "ARG013")]
	public void Group_AttributesAreValidatedLikeThoseOfAParameterCollection(string members, string expectedRule)
	{
		foreach (var attribute in new[] { "[Command(\"group\")]", "[CommandApp(\"app\")]" })
		{
			var model = Extract($"{attribute} public partial class Group {{ {members} }}", "Group");

			Assert.Contains(expectedRule, Ids(model));
			Assert.True(model.HasErrors);
		}
	}

	[Fact]
	public void Group_ArgumentsHaveNoEffectOnTheNode()
	{
		var plain = Extract("[Command(\"group\")] public partial class Group { }", "Group");
		var withOption = Extract("[Command(\"group\")] public partial class Group { [Option(\"o\", \"one\", \"a\")] public string A { get; set; } = \"\"; }", "Group");

		Assert.Equal(plain.Node, withOption.Node);
		Assert.NotEqual(plain.Source, withOption.Source);
	}
	#endregion

	#region Declaration rules (ARG023 to ARG025) and declaration shapes

	[Theory]
	[InlineData("[Command(\"g\")] public partial class Node<T> { }", "Node")]
	[InlineData("[CommandApp(\"a\")] public partial class Node<T, U> { }", "Node")]
	[InlineData("public partial class Outer<T> { [Command(\"g\")] public partial class Node { } }", "Node")]
	[InlineData("[ParameterCollection] [Command(\"g\")] public partial class Node<T> { }", "Node")]
	public void ARG023_GenericClassOrGenericContainer_IsReported(string types, string className)
	{
		var model = Extract(types, className);

		Assert.Contains("ARG023", Ids(model));
		Assert.True(model.HasErrors);
	}

	[Theory]
	[InlineData("[Command(\"g\")] public partial class Node { }")]
	[InlineData("public partial class Outer { [CommandApp(\"a\")] public partial class Node { } }")]
	public void ARG023_ConcreteClass_IsNotReported(string types)
	{
		Assert.DoesNotContain("ARG023", Ids(Extract(types, "Node")));
	}

	[Theory]
	[InlineData("[Command(\"g\")] public abstract partial class Node { }")]
	[InlineData("[CommandApp(\"a\")] public abstract partial class Node { }")]
	[InlineData("[ParameterCollection] [Command(\"g\")] public abstract partial class Node { }")]
	public void ARG024_AbstractClass_IsReportedOnTheClassName(string types)
	{
		var model = Extract(types, "Node");

		Assert.Contains("ARG024", Ids(model));
		var diagnostic = model.Diagnostics.AsReadOnly().First(d => d.Id == "ARG024");
		Assert.Equal(model.ClassLocation, diagnostic.Location);
	}

	[Fact]
	public void ARG024_ClassThatIsNotAbstract_IsNotReported()
	{
		Assert.DoesNotContain("ARG024", Ids(Extract("[Command(\"g\")] public sealed partial class Node { }", "Node")));
	}

	[Theory]
	[InlineData("[Command(\"g\")]")]
	[InlineData("[CommandApp(\"a\")]")]
	public void ARG025_RequiredMembersOfAGroupOrRoot_AreReportedOnEachMember(string attribute)
	{
		var model = Extract($$"""
			{{attribute}}
			public partial class Node
			{
				public required string Name { get; set; }
				public required int Count;
				public string Fine { get; set; } = "";
			}
			""", "Node");

		var diagnostics = model.Diagnostics.AsReadOnly().Where(d => d.Id == "ARG025").ToList();
		Assert.Equal(2, diagnostics.Count);
		Assert.Contains("'Name'", diagnostics[0].ToDiagnostic().GetMessage() + diagnostics[1].ToDiagnostic().GetMessage());
		Assert.Contains("'Count'", diagnostics[0].ToDiagnostic().GetMessage() + diagnostics[1].ToDiagnostic().GetMessage());
	}

	[Fact]
	public void ARG025_RequiredMembersOfALeaf_AreNotReportedByTheTree()
	{
		var model = Extract("""
			[ParameterCollection]
			[Command("leaf")]
			public partial class Node
			{
				public required string Name { get; set; }
			}
			""", "Node");

		Assert.DoesNotContain("ARG025", Ids(model));
	}

	[Fact]
	public void ExplicitInterfaceImplementation_IsNotAChildCandidate()
	{
		var model = Extract("""
			public interface IHas { Child? Other { get; set; } }
			public class Child { }

			[Command("g")]
			public partial class Node : IHas
			{
				public Child? Real { get; private set; }
				Child? IHas.Other { get; set; }
			}
			""", "Node");

		Assert.Equal(["Real"], Children(model).Select(c => c.PropertyName));
	}

	[Fact]
	public void InheritedProperties_AreNotChildCandidates_ThisIsDocumentedAsUnsupported()
	{
		var model = Extract("""
			public class Child { }
			public class Base { public Child? Inherited { get; protected set; } }

			[Command("g")]
			public partial class Node : Base
			{
				public Child? Own { get; private set; }
			}
			""", "Node");

		Assert.Equal(["Own"], Children(model).Select(c => c.PropertyName));
	}

	[Fact]
	public void ContainingTypes_AreReadOutermostFirstWithTheirKeyword()
	{
		var model = Extract("""
			public partial class Outer
			{
				public partial struct Middle
				{
					public partial record Inner
					{
						public partial record struct Deepest
						{
							[Command("g")]
							public partial class Node { }
						}
					}
				}
			}
			""", "Node");

		var containing = model.Node.ContainingTypes.AsReadOnly().Select(item => (item.Keyword, item.Name)).ToList();
		Assert.Equal([("class", "Outer"), ("struct", "Middle"), ("record", "Inner"), ("record struct", "Deepest")], containing);
		Assert.Equal("Tree.Outer.Middle.Inner.Deepest.Node", ContainingType.QualifiedName(model.Node.NamespaceName, model.Node.ContainingTypes, model.Node.ClassName));
	}

	[Fact]
	public void ContainingTypes_AreNotNeededForATopLevelClassAndPartOfTheNodeEquality()
	{
		var topLevel = Extract("[Command(\"g\")] public partial class Node { }", "Node");
		var nested = Extract("public partial class Outer { [Command(\"g\")] public partial class Node { } }", "Node");

		Assert.Equal(0, topLevel.Node.ContainingTypes.Count);
		Assert.NotEqual(topLevel.Node, nested.Node);
		Assert.Equal("Tree.Node", ContainingType.QualifiedName(topLevel.Node.NamespaceName, topLevel.Node.ContainingTypes, topLevel.Node.ClassName));
	}

	[Fact]
	public void Group_OptionsInAnotherFile_AreReadAndValidatedWithTheirLocation()
	{
		const string other = """
			using ArgumentParser;
			namespace Tree;

			public partial class Node
			{
				[Option("o", "other", "Another file")] public string Other { get; set; } = "";
				[Option("o", "again", "Same short name")] public string Again { get; set; } = "";
			}
			""";
		const string first = """
			using ArgumentParser;
			namespace Tree;

			[Command("g")]
			public partial class Node { }
			""";
		var compilation = TestHelper.CreateCompilation(first).AddSyntaxTrees(Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(other, path: "Other.cs"));
		var tree = compilation.SyntaxTrees.First();
		var semanticModel = compilation.GetSemanticModel(tree);
		var declaration = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single();

		var model = CommandNodeExtractor.Extract(declaration, (INamedTypeSymbol)semanticModel.GetDeclaredSymbol(declaration)!, semanticModel);

		Assert.Equal(["Other", "Again"], model.Arguments.Options.AsReadOnly().Select(item => item.PropertyName));
		var duplicates = model.Diagnostics.AsReadOnly().Where(d => d.Id == "ARG003").ToList();
		Assert.Equal(2, duplicates.Count);
		Assert.All(duplicates, d => Assert.Equal("Other.cs", d.Location!.FilePath));
	}
	#endregion

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
		Assert.Equal(CommandNodeInfo.DefaultAnnotations, node.Annotations);
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
	public void Annotations_AreReadFromTheParameterCollectionOfALeaf()
	{
		var combined = Extract("[ParameterCollection(Annotations = CommandAnnotations.ReadOnly | CommandAnnotations.Idempotent)] [Command(\"x\")] public partial class Leaf { }", "Leaf");
		var none = Extract("[ParameterCollection(Annotations = CommandAnnotations.None)] [Command(\"x\")] public partial class Leaf { }", "Leaf");
		var omitted = Extract("[ParameterCollection] [Command(\"x\")] public partial class Leaf { }", "Leaf");

		Assert.Equal((int)(CommandAnnotations.ReadOnly | CommandAnnotations.Idempotent), combined.Node.Annotations);
		Assert.Equal(0, none.Node.Annotations);
		Assert.Equal(CommandNodeInfo.DefaultAnnotations, omitted.Node.Annotations);
	}

	[Fact]
	public void DefaultAnnotations_AreTheDefaultOfTheAttribute()
	{
		Assert.Equal(CommandNodeInfo.DefaultAnnotations, (int)new ParameterCollectionAttribute().Annotations);
	}

	[Fact]
	public void Annotations_OfALeaf_ChangeTheNode()
	{
		var original = Extract("[ParameterCollection] [Command(\"x\")] public partial class Leaf { }", "Leaf");
		var edited = Extract("[ParameterCollection(Annotations = CommandAnnotations.ReadOnly)] [Command(\"x\")] public partial class Leaf { }", "Leaf");

		Assert.NotEqual(original.Node, edited.Node);
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
		Assert.Equal(["Ingredient", "Meal"], model.ChildLocations.AsReadOnly().Select(item => item.PropertyName));
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
	public void EditingAnOption_DoesNotChangeTheNode_ButChangesTheArguments()
	{
		var original = Extract(Group, "IngredientGroup");

		// the new text is as long as the old one, so nothing after it moves
		var edited = Extract(Group.Replace("the database", "new database"), "IngredientGroup");

		Assert.Equal(original.Node, edited.Node);
		Assert.NotEqual(original.Arguments, edited.Arguments);
		Assert.NotEqual(original.Source, edited.Source);
		Assert.NotEqual(original, edited);
	}

	[Fact]
	public void EditingSomethingThatIsNotInTheModel_KeepsTheModelWhenNothingMoves()
	{
		const string withInitializer = "public string Db { get; set; } = \"a\";";
		var original = Extract(Group.Replace("public string Db { get; set; }", withInitializer), "IngredientGroup");

		var edited = Extract(Group.Replace("public string Db { get; set; }", withInitializer.Replace("\"a\"", "\"b\"")), "IngredientGroup");

		Assert.Equal(original, edited);
		Assert.Equal(original.GetHashCode(), edited.GetHashCode());
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
	// the characters, separator and length of a tool name of the Model Context Protocol
	[InlineData("é")]
	[InlineData("add/item")]
	[InlineData("add:item")]
	[InlineData("a__b")]
	[InlineData("add__")]
	public void ARG014_InvalidCommandName_IsReported(string name)
	{
		var model = Extract($"[Command(\"{name}\")] public partial class Group {{ }}", "Group");

		var diagnostic = Assert.Single(model.Diagnostics.AsReadOnly());
		Assert.Equal("ARG014", diagnostic.Id);
		Assert.Contains("'Group'", diagnostic.ToDiagnostic().GetMessage(), StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("add")]
	[InlineData("a-b")]
	[InlineData("x_y")]
	[InlineData("add2")]
	[InlineData("v1.2")]
	[InlineData("_add_")]
	[InlineData("ADD")]
	public void ARG014_ValidCommandName_IsNotReported(string name)
	{
		var model = Extract($"[Command(\"{name}\")] public partial class Group {{ }}", "Group");

		Assert.Empty(Ids(model));
	}

	[Theory]
	[InlineData(127, false)]
	[InlineData(128, true)]
	public void ARG014_ANameHasAtMost127Characters(int length, bool reported)
	{
		var model = Extract($"[Command(\"{new string('a', length)}\")] public partial class Group {{ }}", "Group");

		Assert.Equal(reported ? ["ARG014"] : Array.Empty<string>(), Ids(model));
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
		var messages = model.Diagnostics.AsReadOnly().Select(item => item.ToDiagnostic().GetMessage()).ToList();
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
		var diagnostic = Assert.Single(model.Diagnostics.AsReadOnly());
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
