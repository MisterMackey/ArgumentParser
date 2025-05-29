using Microsoft.CodeAnalysis;

namespace ArgumentParser.Tests
{
    public class ParserGeneratorTests
    {
        [Fact]
        public void Generator_SimpleClass_GeneratesParser()
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

        [Fact]
        public void Generator_NonPartialClass_ReportsDiagnostic()
        {
            // Arrange
            var source = @"
using ArgumentParser;

namespace TestNamespace
{
    [ParameterCollection]
    public class NonPartialArgs // Missing partial keyword
    {
        [Option(""o"", ""Output"", ""Output file path"")]
        public string OutputPath { get; set; }
    }
}";

            // Act
            var (diagnostics, _) = TestHelper.GetGeneratedOutput(source);

            // Assert
            var error = diagnostics.FirstOrDefault(d => d.Id == "ARG001");
            Assert.NotNull(error);
            Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        }
    }
}