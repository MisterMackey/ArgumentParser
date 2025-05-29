using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.CodeAnalysis;

namespace ArgumentParser.Tests
{
    public class HelpTextTests
    {
        [Fact]
        public void HelpText_GenerateAll_GeneratesHelpTextAndArgument()
        {
            // Arrange
            var source = @"
using ArgumentParser;

namespace TestNamespace
{
    [ParameterCollection(HelpTextGeneration.GenerateAll)]
    public partial class HelpTextArgs
    {
        [Option(""o"", ""Output"", ""Output file path"")]
        public string OutputPath { get; set; }
    }
}";

            // Act
            var (diagnostics, output) = TestHelper.GetGeneratedOutput(source);

            // Assert
            Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Contains("public const string HelpText =", output);
            Assert.Contains("public bool DisplayHelp { get; set; }", output);
            Assert.Contains("System.Console.WriteLine(HelpTextArgs.HelpText);", output);
        }
    }
}