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

		[Fact]
		public void Generator_HandlesRequiredParameters()
		{
			// Arrange
			using var builder = new SourceCodeBuilder();
			builder.AddImports(null)
				.AddOptionParameter(new PropertyAndAttributeInfo
				{
					PropertyName = "RequiredOption",
					PropertyType = "string",
					Attribute = new AttributeInfo("r", "Required", "Required option", true, -1)
				}, null);
			var source = builder.ToString();

			// Act
			var (diagnostics, output) = TestHelper.GetGeneratedOutput(source);

			// Assert
			Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
			Assert.Contains("requiredProperties[\"r | Required\"] = true;", output);
			Assert.Contains("var missingRequired = requiredProperties", output);
		}

		[Theory]
		[InlineData("string")]
		[InlineData("int")]
		[InlineData("double")]
		[InlineData("float")]
		[InlineData("long")]
		[InlineData("short")]
		[InlineData("decimal")]
		[InlineData("byte")]
		[InlineData("sbyte")]
		[InlineData("char")]
		[InlineData("uint")]
		[InlineData("ulong")]
		[InlineData("ushort")]
		[InlineData("Guid")]
		[InlineData("Uri")]
		[InlineData("TimeSpan")]
		[InlineData("bool")]
		[InlineData("DateTime")]
		[InlineData("string?")]
		public void Generator_HandlesSimpleBCLTypes(string propertyType)
		{
			// Arrange
			string propertyName = "TestProperty";
			using var builder = new SourceCodeBuilder();
			builder.AddImports(null)
				.AddOptionParameter(new PropertyAndAttributeInfo
				{
					PropertyName = propertyName,
					PropertyType = propertyType,
					Attribute = new AttributeInfo("t", "Test", "Test property", false, -1)
				}, null);
			var source = builder.ToString();
			// Act
			var (diagnostics, output) = TestHelper.GetGeneratedOutput(source);
			// Assert
			Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
			Assert.Contains($"TestProperty = ", output);
		}
	}
}