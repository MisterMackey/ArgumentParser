using Microsoft.CodeAnalysis;

namespace ArgumentParser.Tests
{
	public class SourceTextGeneratorTests
	{
		[Fact]
		public void Generator_DeclaresParseMethod()
		{
			// Arrange
			var source = @"
using ArgumentParser;

namespace TestNamespace
{
    [ParameterCollection]
    public partial class SimpleArgs
    {
        [Option(""o"", ""Output"", ""Output file path"")]
        public string OutputPath { get; set; }
    }
}";

			// Act
			var (diagnostics, output) = TestHelper.GetGeneratedOutput(source);

			// Assert
			Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
			Assert.Contains("public static (SimpleArgs result, List<ArgumentParser.ArgumentParserException> errors) Parse(string[] args)", output);
			Assert.Contains("Output", output);
		}
	}
}