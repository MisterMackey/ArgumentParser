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

		Assert.All(OutputReasons(driver), reason => Assert.Equal(IncrementalStepRunReason.Modified, reason));

		var generated = driver.GetRunResult().GeneratedTrees.Single().ToString();
		Assert.Contains("A different description", generated);
		Assert.DoesNotContain("DESCRIPTION", generated);
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
		var compilation = CreateCompilation();
		var driver = CreateDriver().RunGenerators(compilation);
		return driver.RunGenerators(edit(compilation));
	}

	private static GeneratorDriver CreateDriver()
	{
		return CSharpGeneratorDriver.Create(
			generators: [new ParserGenerator().AsSourceGenerator()],
			driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
	}

	private static CSharpCompilation CreateCompilation()
	{
		return TestHelper.CreateCompilation(AnnotatedSource).AddSyntaxTrees(Parse(UnrelatedSource));
	}

	private static SyntaxTree Parse(string source) => CSharpSyntaxTree.ParseText(source);

	private static IEnumerable<IncrementalStepRunReason> OutputReasons(GeneratorDriver driver)
	{
		var result = driver.GetRunResult().Results.Single();
		var outputs = result.TrackedOutputSteps[OutputStepName].SelectMany(step => step.Outputs).ToList();
		Assert.NotEmpty(outputs);
		return outputs.Select(o => o.Reason);
	}

	private static bool IsReused(IncrementalStepRunReason reason)
	{
		return reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged;
	}
}
