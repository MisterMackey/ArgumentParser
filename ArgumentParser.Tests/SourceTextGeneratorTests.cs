using ArgumentParser.Internal;
using Microsoft.CodeAnalysis;

namespace ArgumentParser.Tests
{
	public class SourceTextGeneratorTests
	{
		[Fact]
		public void Generator_DeclaresParseMethod()
		{
			// Arrange
			using var builder = new SourceCodeBuilder();
			builder.AddImports(null)
				.AddOptionParameter(new PropertyAndAttributeInfo
				{
					PropertyName = "OutputPath",
					PropertyType = "string",
					Attribute = new AttributeInfo("o", "Output", "Output file path", false, -1)
				}, null);
			var source = builder.ToString();

			// Act
			var (diagnostics, output) = TestHelper.GetGeneratedOutput(source);

			// Assert
			Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
			Assert.Contains("public static (TestClass result, List<ArgumentParser.ArgumentParserException> errors) Parse(string[] args)", output);
			Assert.Contains("Output", output);
		}
	}
}