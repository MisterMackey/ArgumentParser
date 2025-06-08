using ArgumentParser.Internal;
using Microsoft.CodeAnalysis;

namespace ArgumentParser.Tests
{
	/// <summary>
	/// Couple simple checks including property types, required params, parameter handlers
	/// </summary>
	public class BasicSourceTextGeneratorTests
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

		[Fact]
		public void Generator_HandlesEnumFlagWithLevels()
		{
			// Arrange
			using var builder = new SourceCodeBuilder();
			builder.AddImports(null)
				.AddImports(new[] { "using ArgumentParser.Tests;" })
				.AddFlagParameter(new PropertyAndAttributeInfo
				{
					PropertyName = "LogLevel",
					PropertyType = "ExampleEnum",
					Attribute = new AttributeInfo("l", "LogLevel", "Set the log level", false, -1)
				}, null);
			var source = builder.ToString();
			// Act
			var (diagnostics, output) = TestHelper.GetGeneratedOutput(source);
			// Assert
			Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
			Assert.Contains("LogLevel = (ArgumentParser.Tests.ExampleEnum)flagToken.Level", output);
		}

		[Fact]
		public void Generator_HandlesEnumOption()
		{
			// Arrange
			using var builder = new SourceCodeBuilder();
			builder.AddImports(null)
				.AddImports(new[] { "using ArgumentParser.Tests;" })
				.AddOptionParameter(new PropertyAndAttributeInfo
				{
					PropertyName = "LogLevelOption",
					PropertyType = "ExampleEnum",
					Attribute = new AttributeInfo("l", "LogLevel", "Set the log level option", false, -1)
				}, null);
			var source = builder.ToString();
			// Act
			var (diagnostics, output) = TestHelper.GetGeneratedOutput(source);
			// Assert
			Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
			Assert.Contains("LogLevelOption = parsedValue", output);
			Assert.Contains("if (!Enum.TryParse<ArgumentParser.Tests.ExampleEnum>)", output);
		}

		[Fact]
		public void Generator_CreatesFlagHandler()
		{
			// Arrange
			using var builder = new SourceCodeBuilder();
			builder.AddImports(null)
				.AddFlagParameter(new PropertyAndAttributeInfo
				{
					PropertyName = "Verbose",
					PropertyType = "bool",
					Attribute = new AttributeInfo("v", "Verbose", "Enable verbose output", false, -1)
				}, null);
			var source = builder.ToString();

			// Act
			var (diagnostics, output) = TestHelper.GetGeneratedOutput(source);

			// Assert
			Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
			Assert.Contains("Verbose = ", output);
			Assert.Contains("if (flagToken.Name == \"v\" || flagToken.Name == \"Verbose\")", output);
		}

		[Fact]
		public void Generator_CreatesOptionHandler()
		{
			// Arrange
			using var builder = new SourceCodeBuilder();
			builder.AddImports(null)
				.AddOptionParameter(new PropertyAndAttributeInfo
				{
					PropertyName = "ConfigFile",
					PropertyType = "string",
					Attribute = new AttributeInfo("c", "Config", "Path to config file", false, -1)
				}, null);
			var source = builder.ToString();

			// Act
			var (diagnostics, output) = TestHelper.GetGeneratedOutput(source);

			// Assert
			Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
			Assert.Contains("ConfigFile = ", output);
			Assert.Contains("if (optionToken.Name == \"c\" || optionToken.Name == \"Config\")", output);
		}

		[Fact]
		public void Generator_CreatesPositionalHandler()
		{
			// Arrange
			using var builder = new SourceCodeBuilder();
			builder.AddImports(null)
				.AddPositionalParameter(new PropertyAndAttributeInfo
				{
					PropertyName = "InputFile",
					PropertyType = "string",
					Attribute = new AttributeInfo("", "", "Input file path", false, 0)
				}, null);
			var source = builder.ToString();

			// Act
			var (diagnostics, output) = TestHelper.GetGeneratedOutput(source);

			// Assert
			Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
			Assert.Contains("InputFile = ", output);
			Assert.Contains("if (positionalToken.Position == 0)", output);
		}
	}
}