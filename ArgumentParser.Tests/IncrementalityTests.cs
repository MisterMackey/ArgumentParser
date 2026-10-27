using ArgumentParser.Internal;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ArgumentParser.Tests;

/// <summary>
/// Tests that the generator is properly incremental: an edit that does not touch an annotated class must not
/// regenerate its parser, while an edit to the annotated class must. Uses the tracked pipeline steps of the driver.
/// </summary>
public class IncrementalityTests
{
	private const string OutputStepName = "SourceOutput";

	private const string AnnotatedSource = """
		using ArgumentParser;

		[ParameterCollection(HelpTextGeneration.GenerateTextOnly, "h", "Help", BehaviourOnError.ThrowNever)]
		public partial class Args
		{
			[Flag("v", "Verbose", "DESCRIPTION")]
			public bool Verbose { get; set; }
		}
		""";

	private const string NotPartialSource = """
		using ArgumentParser;

		[ParameterCollection]
		public class Args
		{
			[Flag("v", "Verbose")]
			public bool Verbose { get; set; }
		}
		""";

	private const string WarningSource = """
		using ArgumentParser;

		[ParameterCollection(HelpTextGeneration.None, "x", "Other", BehaviourOnError.ThrowNever)]
		public partial class Args
		{
		}
		""";

	private const string UnrelatedSource = "public class Unrelated { public int A; }";

	private const string GroupSource = """
		using ArgumentParser;

		public class Child { }

		[Command("group", "A group")]
		public partial class Group
		{
			[Option("d", "db", "DESCRIPTION")]
			public string Db { get; set; } = "";

			public Child? Add { get; private set; }
		}
		""";

	private const string TreeSource = """
		using ArgumentParser;

		[CommandApp("app", "An app")]
		public partial class App
		{
			public ItemGroup? Items { get; private set; }
		}

		[Command("items", "Manage items")]
		public partial class ItemGroup
		{
			[Option("d", "db", "DESCRIPTION")]
			public string Db { get; set; } = "a";

			public ItemAdd? Add { get; private set; }
		}

		[ParameterCollection]
		[Command("add", "Add an item")]
		public partial class ItemAdd
		{
		}
		""";

	private const string InvalidNameSource = """
		using ArgumentParser;

		[Command("bad name")]
		public partial class Group
		{
		}
		""";

	private const string PositionalOnGroupSource = """
		using ArgumentParser;

		[CommandApp("app")]
		public partial class App
		{
			[Positional(0, "first")]
			public string First { get; set; } = "";
		}
		""";

	private const string ConflictingAttributesSource = """
		using ArgumentParser;

		[CommandApp("app")]
		[Command("x")]
		public partial class App
		{
		}
		""";

	[Fact]
	public void SameCompilationTwice_ReusesTheCachedOutput()
	{
		var driver = RunTwice(compilation => compilation);

		Assert.All(OutputReasons(driver), reason => Assert.True(IsReused(reason), reason.ToString()));
	}

	[Fact]
	public void EditToAnUnrelatedFile_ReusesTheCachedOutput()
	{
		var driver = RunTwice(
			compilation => compilation.ReplaceSyntaxTree(
				compilation.SyntaxTrees.Single(t => t.ToString() == UnrelatedSource),
				Parse(UnrelatedSource + "\n// a comment\npublic class AnotherUnrelated { }")));

		Assert.All(OutputReasons(driver), reason => Assert.True(IsReused(reason), reason.ToString()));
	}

	[Theory]
	[InlineData(NotPartialSource, "ARG001")]
	[InlineData(WarningSource, "ARG008")]
	[InlineData(InvalidNameSource, "ARG014")]
	[InlineData(PositionalOnGroupSource, "ARG015")]
	[InlineData(ConflictingAttributesSource, "ARG016")]
	public void EditToAnUnrelatedFile_ReusesTheCachedDiagnostics(string source, string expectedRule)
	{
		var compilation = TestHelper.CreateCompilation(source).AddSyntaxTrees(Parse(UnrelatedSource));
		var driver = CreateDriver().RunGenerators(compilation);
		Assert.Contains(driver.GetRunResult().Diagnostics, d => d.Id == expectedRule);

		driver = driver.RunGenerators(compilation.ReplaceSyntaxTree(
			compilation.SyntaxTrees.Single(t => t.ToString() == UnrelatedSource),
			Parse(UnrelatedSource + "\n// a comment")));

		Assert.All(OutputReasons(driver), reason => Assert.True(IsReused(reason), reason.ToString()));
		Assert.Contains(driver.GetRunResult().Diagnostics, d => d.Id == expectedRule);
	}

	[Fact]
	public void EditToTheAnnotatedClass_RegeneratesTheParser()
	{
		var driver = RunTwice(
			compilation => compilation.ReplaceSyntaxTree(
				compilation.SyntaxTrees.Single(t => t.ToString() == AnnotatedSource),
				Parse(AnnotatedSource.Replace("DESCRIPTION", "A different description"))));

		// the steps of the command tree are idle for a source without commands, so only some of the outputs change
		Assert.Contains(IncrementalStepRunReason.Modified, OutputReasons(driver));

		var generated = driver.GetRunResult().GeneratedTrees.Single().ToString();
		Assert.Contains("A different description", generated);
		Assert.DoesNotContain("DESCRIPTION", generated);
	}

	[Fact]
	public void EditToAnotherPartOfTheAnnotatedClass_RegeneratesTheParser()
	{
		const string otherPart = """
			using ArgumentParser;

			public partial class Args
			{
				[Option("o", "Output", "OTHER PART")]
				public string Output { get; set; } = "";
			}
			""";
		var compilation = CreateCompilation().AddSyntaxTrees(Parse(otherPart));
		var driver = CreateDriver().RunGenerators(compilation);
		Assert.Contains("OTHER PART", driver.GetRunResult().GeneratedTrees.Single().ToString(), StringComparison.Ordinal);

		driver = driver.RunGenerators(compilation.ReplaceSyntaxTree(
			compilation.SyntaxTrees.Single(t => t.ToString() == otherPart),
			Parse(otherPart.Replace("OTHER PART", "Edited elsewhere", StringComparison.Ordinal))));

		Assert.Contains(IncrementalStepRunReason.Modified, OutputReasons(driver));
		var generated = driver.GetRunResult().GeneratedTrees.Single().ToString();
		Assert.Contains("Edited elsewhere", generated, StringComparison.Ordinal);
		Assert.DoesNotContain("OTHER PART", generated, StringComparison.Ordinal);
	}

	[Fact]
	public void Group_EditToAnUnrelatedFile_ReusesTheCachedModel()
	{
		var driver = RunTwice(
			GroupSource,
			compilation => compilation.ReplaceSyntaxTree(
				compilation.SyntaxTrees.Single(t => t.ToString() == UnrelatedSource),
				Parse(UnrelatedSource + "\n// a comment\npublic class AnotherUnrelated { }")));

		Assert.All(ModelReasons(driver, ParserGenerator.CommandModelsStepName), reason => Assert.True(IsReused(reason), reason.ToString()));
	}

	[Fact]
	public void Group_EditToAnInitializerThatMovesNothing_ReusesTheModel()
	{
		// the initializer is not part of the model and the new text is as long as the old one: nothing after it moves
		var driver = RunTwice(
			TreeSource,
			compilation => compilation.ReplaceSyntaxTree(
				compilation.SyntaxTrees.Single(t => t.ToString() == TreeSource),
				Parse(TreeSource.Replace("= \"a\";", "= \"b\";"))));

		Assert.All(ModelReasons(driver, ParserGenerator.CommandModelsStepName), reason => Assert.True(IsReused(reason), reason.ToString()));
		Assert.All(ModelReasons(driver, ParserGenerator.NodeSourcesStepName), reason => Assert.True(IsReused(reason), reason.ToString()));
		Assert.All(ModelReasons(driver, ParserGenerator.LinkedTreesStepName), reason => Assert.True(IsReused(reason), reason.ToString()));
	}

	[Fact]
	public void Group_MovingCode_ChangesTheModelButNotTheSourcesOrTheLinkedTrees()
	{
		var driver = RunTwice(
			TreeSource,
			compilation => compilation.ReplaceSyntaxTree(
				compilation.SyntaxTrees.Single(t => t.ToString() == TreeSource),
				Parse("// a comment that moves everything below it\n" + TreeSource)));

		Assert.All(ModelReasons(driver, ParserGenerator.CommandModelsStepName), reason => Assert.Equal(IncrementalStepRunReason.Modified, reason));
		Assert.All(ModelReasons(driver, ParserGenerator.NodeSourcesStepName), reason => Assert.True(IsReused(reason), reason.ToString()));
		Assert.All(ModelReasons(driver, ParserGenerator.LinkedTreesStepName), reason => Assert.True(IsReused(reason), reason.ToString()));
	}

	[Fact]
	public void Group_EditingAnOption_LinksTheTreeAgain()
	{
		var driver = RunTwice(
			TreeSource,
			compilation => compilation.ReplaceSyntaxTree(
				compilation.SyntaxTrees.Single(t => t.ToString() == TreeSource),
				Parse(TreeSource.Replace("DESCRIPTION", "ANOTHER ONE"))));

		Assert.All(ModelReasons(driver, ParserGenerator.NodeSourcesStepName), reason => Assert.Equal(IncrementalStepRunReason.Modified, reason));
		Assert.All(ModelReasons(driver, ParserGenerator.LinkedTreesStepName), reason => Assert.Equal(IncrementalStepRunReason.Modified, reason));
		var group = driver.GetRunResult().GeneratedTrees.Single(t => t.FilePath.EndsWith("ItemGroup_Command.g.cs", StringComparison.Ordinal)).ToString();
		Assert.Contains("ANOTHER ONE", group, StringComparison.Ordinal);
		Assert.DoesNotContain("DESCRIPTION", group, StringComparison.Ordinal);
	}

	[Fact]
	public void Leaf_RenamingAnOption_LinksTheTreeAgain()
	{
		// the names of the options of a leaf are compared with those of the commands above it (ARG027)
		var withOption = TreeSource.Replace(
			"public partial class ItemAdd\n{\n}",
			"public partial class ItemAdd\n{\n\t[Option(\"x\", \"extra\", \"leaf\")]\n\tpublic string Extra { get; set; } = \"\";\n}",
			StringComparison.Ordinal);
		Assert.NotEqual(TreeSource, withOption);
		var driver = RunTwice(
			withOption,
			compilation => compilation.ReplaceSyntaxTree(
				compilation.SyntaxTrees.Single(t => t.ToString() == withOption),
				Parse(withOption.Replace("\"extra\"", "\"db\"", StringComparison.Ordinal))));

		Assert.All(ModelReasons(driver, ParserGenerator.NodeSourcesStepName), reason => Assert.Equal(IncrementalStepRunReason.Modified, reason));
		Assert.All(ModelReasons(driver, ParserGenerator.LinkedTreesStepName), reason => Assert.Equal(IncrementalStepRunReason.Modified, reason));
		var result = driver.GetRunResult();
		Assert.Equal(["ARG027"], result.Diagnostics.Select(d => d.Id));
		Assert.DoesNotContain(result.GeneratedTrees, t => t.FilePath.EndsWith("_Command.g.cs", StringComparison.Ordinal));
	}

	[Fact]
	public void Group_RenamingTheCommand_ChangesTheModel()
	{
		var driver = RunTwice(
			GroupSource,
			compilation => compilation.ReplaceSyntaxTree(
				compilation.SyntaxTrees.Single(t => t.ToString() == GroupSource),
				Parse(GroupSource.Replace("\"group\"", "\"groups\""))));

		Assert.All(ModelReasons(driver, ParserGenerator.CommandModelsStepName), reason => Assert.Equal(IncrementalStepRunReason.Modified, reason));
	}

	[Fact]
	public void Root_RenamingTheApplication_LinksTheTreeAgain()
	{
		// the name of the application is part of the tree: the help text of every level will show it
		var driver = RunTwice(
			TreeSource,
			compilation => compilation.ReplaceSyntaxTree(
				compilation.SyntaxTrees.Single(t => t.ToString() == TreeSource),
				Parse(TreeSource.Replace("\"app\"", "\"apps\""))));

		Assert.All(ModelReasons(driver, ParserGenerator.RootModelsStepName), reason => Assert.Equal(IncrementalStepRunReason.Modified, reason));
		Assert.All(ModelReasons(driver, ParserGenerator.LinkedTreesStepName), reason => Assert.Equal(IncrementalStepRunReason.Modified, reason));
	}

	[Fact]
	public void Root_ChangingTheHelpSettings_LinksAndGeneratesTheTreeAgain()
	{
		var driver = RunTwice(
			TreeSource,
			compilation => compilation.ReplaceSyntaxTree(
				compilation.SyntaxTrees.Single(t => t.ToString() == TreeSource),
				Parse(TreeSource.Replace("[CommandApp(\"app\", \"An app\")]", "[CommandApp(\"app\", \"An app\", helpArgumentLongName: \"info\")]", StringComparison.Ordinal))));

		Assert.All(ModelReasons(driver, ParserGenerator.LinkedTreesStepName), reason => Assert.Equal(IncrementalStepRunReason.Modified, reason));
		var leaf = driver.GetRunResult().GeneratedTrees.Single(t => t.FilePath.EndsWith("ItemAdd_Command.g.cs", StringComparison.Ordinal)).ToString();
		Assert.Contains("\"info\"", leaf, StringComparison.Ordinal);
	}

	[Fact]
	public void Root_ChangingUseCurrentCulture_GeneratesTheTreeAgain()
	{
		var driver = RunTwice(
			TreeSource,
			compilation => compilation.ReplaceSyntaxTree(
				compilation.SyntaxTrees.Single(t => t.ToString() == TreeSource),
				Parse(TreeSource.Replace("[CommandApp(\"app\", \"An app\")]", "[CommandApp(\"app\", \"An app\", UseCurrentCulture = true)]", StringComparison.Ordinal))));

		Assert.All(ModelReasons(driver, ParserGenerator.LinkedTreesStepName), reason => Assert.Equal(IncrementalStepRunReason.Modified, reason));
		var leaf = driver.GetRunResult().GeneratedTrees.Single(t => t.FilePath.EndsWith("ItemAdd_Command.g.cs", StringComparison.Ordinal)).ToString();
		Assert.Contains("CommandHelpArgument, true);", leaf, StringComparison.Ordinal);
	}

	[Fact]
	public void AddingAParseMethodToTheDeclaringClass_RegeneratesTheParserWithThatMethod()
	{
		const string source = """
			using ArgumentParser;

			public class Point { public static bool Read(string input, out Point value) { value = new Point(); return true; } }

			[ParameterCollection]
			public partial class Args
			{
				[Option("p", "point", "A point")]
				[ParsedWithMethod("Read")]
				public Point Point { get; set; } = new Point();
			}
			""";
		var driver = RunTwice(
			source,
			compilation => compilation.ReplaceSyntaxTree(
				compilation.SyntaxTrees.Single(t => t.ToString() == source),
				Parse(source.Replace("public Point Point { get; set; } = new Point();", "public Point Point { get; set; } = new Point(); private static bool Read(string input, out Point value) { value = new Point(); return true; }", StringComparison.Ordinal))));

		Assert.Contains(IncrementalStepRunReason.Modified, OutputReasons(driver));
		var generated = driver.GetRunResult().GeneratedTrees.Single().ToString();
		Assert.Contains("global::Args.Read(optionToken.Value", generated, StringComparison.Ordinal);
		Assert.DoesNotContain("global::Point.Read", generated, StringComparison.Ordinal);
	}

	[Fact]
	public void ChangingUseCurrentCulture_RegeneratesTheParser()
	{
		const string source = """
			using ArgumentParser;

			[ParameterCollection]
			public partial class Args
			{
				[Option("a", "amount", "An amount")]
				public double Amount { get; set; }
			}
			""";
		var driver = RunTwice(
			source,
			compilation => compilation.ReplaceSyntaxTree(
				compilation.SyntaxTrees.Single(t => t.ToString() == source),
				Parse(source.Replace("[ParameterCollection]", "[ParameterCollection(UseCurrentCulture = true)]", StringComparison.Ordinal))));

		Assert.Contains(IncrementalStepRunReason.Modified, OutputReasons(driver));
		var generated = driver.GetRunResult().GeneratedTrees.Single().ToString();
		Assert.Contains("ValueParser.TryParse(optionToken.Value, true, out double parsedValue)", generated, StringComparison.Ordinal);
	}

	[Fact]
	public void EditToTheParserClass_LeavesAGroupInTheSameFileCached()
	{
		// the group comes first in the file, so an edit to the parser class after it moves nothing in the group
		var both = "using ArgumentParser;\n"
			+ GroupSource.Replace("using ArgumentParser;\n", "").Replace("DESCRIPTION", "OPTION TEXT")
			+ "\n"
			+ AnnotatedSource.Replace("using ArgumentParser;\n", "");
		var driver = RunTwice(
			both,
			compilation => compilation.ReplaceSyntaxTree(
				compilation.SyntaxTrees.Single(t => t.ToString() == both),
				Parse(both.Replace("DESCRIPTION", "A different description"))));

		Assert.All(ModelReasons(driver, ParserGenerator.CommandModelsStepName), reason => Assert.True(IsReused(reason), reason.ToString()));
		Assert.Contains("A different description", driver.GetRunResult().GeneratedTrees.Single().ToString());
	}

	[Fact]
	public void FirstRun_ProducesTheOutputFromScratch()
	{
		var driver = CreateDriver().RunGenerators(CreateCompilation());

		Assert.All(OutputReasons(driver), reason => Assert.Equal(IncrementalStepRunReason.New, reason));
		Assert.Single(driver.GetRunResult().GeneratedTrees);
	}

	private static GeneratorDriver RunTwice(Func<Compilation, Compilation> edit)
	{
		return RunTwice(AnnotatedSource, edit);
	}

	private static GeneratorDriver RunTwice(string source, Func<Compilation, Compilation> edit)
	{
		var compilation = CreateCompilation(source);
		var driver = CreateDriver().RunGenerators(compilation);
		return driver.RunGenerators(edit(compilation));
	}

	private static GeneratorDriver CreateDriver()
	{
		return CSharpGeneratorDriver.Create(
			generators: [new ParserGenerator().AsSourceGenerator()],
			driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
	}

	private static CSharpCompilation CreateCompilation(string source = AnnotatedSource)
	{
		return TestHelper.CreateCompilation(source).AddSyntaxTrees(Parse(UnrelatedSource));
	}

	private static SyntaxTree Parse(string source) => CSharpSyntaxTree.ParseText(source);

	private static IEnumerable<IncrementalStepRunReason> OutputReasons(GeneratorDriver driver)
	{
		var result = driver.GetRunResult().Results.Single();
		var outputs = result.TrackedOutputSteps[OutputStepName].SelectMany(step => step.Outputs).ToList();
		Assert.NotEmpty(outputs);
		return outputs.Select(o => o.Reason);
	}

	private static IEnumerable<IncrementalStepRunReason> ModelReasons(GeneratorDriver driver, string stepName)
	{
		var result = driver.GetRunResult().Results.Single();
		var outputs = result.TrackedSteps[stepName].SelectMany(step => step.Outputs).ToList();
		Assert.NotEmpty(outputs);
		return outputs.Select(o => o.Reason);
	}

	private static bool IsReused(IncrementalStepRunReason reason)
	{
		return reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged;
	}
}
