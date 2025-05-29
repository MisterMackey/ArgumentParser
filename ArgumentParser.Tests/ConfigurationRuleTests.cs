using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.CodeAnalysis;

namespace ArgumentParser.Tests
{
    public class ConfigurationRuleTests
    {
        [Fact]
        public void Diagnostic_DisplayHelpPropertyConflict_ReportsError()
        {
            // Arrange
            var source = @"
using ArgumentParser;

namespace TestNamespace
{
    [ParameterCollection(HelpTextGeneration.GenerateAll)]
    public partial class ConflictArgs
    {
        // This conflicts with the generator-supplied DisplayHelp
        public bool DisplayHelp { get; set; }
    }
}";

            // Act
            var (diagnostics, _) = TestHelper.GetGeneratedOutput(source);

            // Assert
            var error = diagnostics.FirstOrDefault(d => d.Id == "ARG011");
            Assert.NotNull(error);
            Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        }
    }
}