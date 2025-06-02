using Microsoft.CodeAnalysis;
using ArgumentParser.Internal;

namespace ArgumentParser.Tests
{
	public class DiagnosticTests
	{
		[Fact]
		public void Diagnostic_DuplicateArgumentName_ReportsError()
		{
			// Arrange
			var builder = new SourceCodeBuilder();
			builder.AddImports(null)
			    .AddOptionParameter(new PropertyAndAttributeInfo
			    {
				    PropertyName = "FirstOption",
				    PropertyType = "string",
				    Attribute = new AttributeInfo("o", "Output", "First option", false, -1)
			    }, null)
			    .AddFlagParameter(new PropertyAndAttributeInfo
			    {
				    PropertyName = "DuplicateShortName",
				    PropertyType = "bool",
				    Attribute = new AttributeInfo("o", "Other", "Duplicate short name", false, -1)
			    }, null);
			var source = builder.ToString();

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
			var builder = new SourceCodeBuilder();
			builder.AddImports(null)
			    .AddFlagParameter(new PropertyAndAttributeInfo
			    {
				    PropertyName = "InvalidFlag",
				    PropertyType = "string",
				    Attribute = new AttributeInfo("s", "String", "Invalid flag on string", false, -1)
			    }, null);
			var source = builder.ToString();

			// Act
			var (diagnostics, _) = TestHelper.GetGeneratedOutput(source);

			// Assert
			var error = diagnostics.FirstOrDefault(d => d.Id == "ARG013");
			Assert.NotNull(error);
			Assert.Equal(DiagnosticSeverity.Error, error.Severity);
		}
	}
}