using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.CodeAnalysis;

namespace ArgumentParser.Tests
{
    public class DiagnosticTests
    {
        [Fact]
        public void Diagnostic_DuplicateArgumentName_ReportsError()
        {
            // Arrange
            var source = @"
using ArgumentParser;

namespace TestNamespace
{
    [ParameterCollection]
    public partial class DuplicateNameArgs
    {
        [Option(""o"", ""Output"", ""First option"")]
        public string FirstOption { get; set; }
        
        [Flag(""o"", ""Other"", ""Duplicate short name"")]
        public bool DuplicateShortName { get; set; }
    }
}";

            // Act
            var (diagnostics, _) = TestHelper.GetGeneratedOutput(source);

            // Assert
            var error = diagnostics.FirstOrDefault(d => d.Id == "ARG003");
            Assert.NotNull(error);
            Assert.Equal(DiagnosticSeverity.Error, error.Severity);
            Assert.Contains("Duplicate short name 'o'", error.GetMessage());
        }

        [Fact]
        public void Diagnostic_FlagOnNonBooleanProperty_ReportsError()
        {
            // Arrange
            var source = @"
using ArgumentParser;

namespace TestNamespace
{
    [ParameterCollection]
    public partial class InvalidFlagArgs
    {
        [Flag(""s"", ""String"", ""Invalid flag on string"")]
        public string InvalidFlag { get; set; } // Flag on non-boolean
    }
}";

            // Act
            var (diagnostics, _) = TestHelper.GetGeneratedOutput(source);

            // Assert
            var error = diagnostics.FirstOrDefault(d => d.Id == "ARG013");
            Assert.NotNull(error);
            Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        }
    }
}