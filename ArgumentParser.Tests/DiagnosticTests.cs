using Microsoft.CodeAnalysis;
using ArgumentParser.Internal;

namespace ArgumentParser.Tests
{
	public class DiagnosticTests
	{
		[Fact]
		public void Diagnostic_MissingPartialKeyword_ReportsError()
		{
			// Arrange
			var builder = new SourceCodeBuilder();
			builder.AddImports(null)
				.AddClassDeclaration("TestClass", "public class");
			var source = builder.ToString();

			// Act
			var (diagnostics, _) = TestHelper.GetGeneratedOutput(source);

			// Assert
			var error = diagnostics.FirstOrDefault(d => d.Id == "ARG001");
			Assert.NotNull(error);
			Assert.Equal(DiagnosticSeverity.Error, error.Severity);
			Assert.Contains("must be declared as a partial class", error.GetMessage());
		}

		[Fact]
		public void Diagnostic_ParseMethodAlreadyExists_ReportsError()
		{
			// Arrange
			var builder = new SourceCodeBuilder();
			builder.AddImports(null)
				.AddOptionParameter(new PropertyAndAttributeInfo
				{
					PropertyName = "SimpleOption",
					PropertyType = "string",
					Attribute = new AttributeInfo("s", "Simple", "A simple option", false, -1)
				}, null)
				// Add a Parse method that would conflict with the generated one
				.AddClassMember(@"public static (TestClass result, List<ArgumentParser.ArgumentParserException> errors) Parse(string[] args)
				{
					return (new TestClass(), new List<ArgumentParser.ArgumentParserException>());
				}");
			var source = builder.ToString();

			// Act
			var (diagnostics, _) = TestHelper.GetGeneratedOutput(source);

			// Assert
			var error = diagnostics.FirstOrDefault(d => d.Id == "ARG002");
			Assert.NotNull(error);
			Assert.Equal(DiagnosticSeverity.Error, error.Severity);
			Assert.Contains("already contains a Parse(string[] args) method", error.GetMessage());
		}

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
		public void Diagnostic_DuplicatePositionalPosition_ReportsError()
		{
			// Arrange
			var builder = new SourceCodeBuilder();
			builder.AddImports(null)
			    .AddPositionalParameter(new PropertyAndAttributeInfo
			    {
				    PropertyName = "FirstPositional",
				    PropertyType = "string",
				    Attribute = new AttributeInfo("", "", "First positional", false, 0)
			    }, null)
			    .AddPositionalParameter(new PropertyAndAttributeInfo
			    {
				    PropertyName = "DuplicatePosition",
				    PropertyType = "string",
				    Attribute = new AttributeInfo("", "", "Duplicate position", false, 0)
			    }, null);
			var source = builder.ToString();

			// Act
			var (diagnostics, _) = TestHelper.GetGeneratedOutput(source);

			// Assert
			var error = diagnostics.FirstOrDefault(d => d.Id == "ARG004");
			Assert.NotNull(error);
			Assert.Equal(DiagnosticSeverity.Error, error.Severity);
			Assert.Contains("Duplicate positional argument position '0'", error.GetMessage());
		}

		[Fact]
		public void Diagnostic_InvalidPositionalPosition_ReportsError()
		{
			// Arrange
			var builder = new SourceCodeBuilder();
			builder.AddImports(null)
			    .AddPositionalParameter(new PropertyAndAttributeInfo
			    {
				    PropertyName = "InvalidPosition",
				    PropertyType = "string",
				    Attribute = new AttributeInfo("", "", "Invalid position", false, 2) // Position 2 when only 1 positional arg
			    }, null);
			var source = builder.ToString();

			// Act
			var (diagnostics, _) = TestHelper.GetGeneratedOutput(source);

			// Assert
			var error = diagnostics.FirstOrDefault(d => d.Id == "ARG005");
			Assert.NotNull(error);
			Assert.Equal(DiagnosticSeverity.Error, error.Severity);
			Assert.Contains("Invalid positional argument position '2'", error.GetMessage());
		}
		
		[Fact]
		public void Diagnostic_NoParameterlessConstructor_ReportsError()
		{
			// Arrange
			var builder = new SourceCodeBuilder();
			builder.AddImports(null)
			    .AddOptionParameter(new PropertyAndAttributeInfo
			    {
				    PropertyName = "Simple",
				    PropertyType = "string",
				    Attribute = new AttributeInfo("s", "Simple", "Simple option", false, -1)
			    }, null)
			    // Add constructor with parameters but no parameterless constructor
			    .AddClassMember(@"public TestClass(string value) 
				{
					Simple = value;
				}");
			var source = builder.ToString();

			// Act
			var (diagnostics, _) = TestHelper.GetGeneratedOutput(source);

			// Assert
			var error = diagnostics.FirstOrDefault(d => d.Id == "ARG006");
			Assert.NotNull(error);
			Assert.Equal(DiagnosticSeverity.Error, error.Severity);
			Assert.Contains("must have a constructor with no parameters", error.GetMessage());
		}

		[Fact]
		public void Diagnostic_UnsupportedPropertyType_ReportsError()
		{
			// Arrange
			var builder = new SourceCodeBuilder();
			builder.AddImports(new[] { "System.IO", "ArgumentParser;" })
			    .AddOptionParameter(new PropertyAndAttributeInfo
			    {
				    PropertyName = "UnsupportedType",
				    PropertyType = "FileInfo", // This type is not supported
				    Attribute = new AttributeInfo("u", "Unsupported", "Unsupported type option", false, -1)
			    }, null);
			var source = builder.ToString();

			// Act
			var (diagnostics, _) = TestHelper.GetGeneratedOutput(source);

			// Assert
			var error = diagnostics.FirstOrDefault(d => d.Id == "ARG007");
			Assert.NotNull(error);
			Assert.Equal(DiagnosticSeverity.Error, error.Severity);
			Assert.Contains("Specified property type 'FileInfo'", error.GetMessage());
		}
		
		[Fact]
		public void Diagnostic_HelpArgumentNamesSpecifiedButGenerationDisabled_ReportsWarning()
		{
			// Arrange
			var builder = new SourceCodeBuilder();
			builder.AddImports(null)
			    .AddClassAttribute("[ParameterCollection(helpTextGeneration: HelpTextGeneration.None, helpArgumentShortName: \"?\", helpArgumentLongName: \"Helpme\")]");
			var source = builder.ToString();

			// Act
			var (diagnostics, _) = TestHelper.GetGeneratedOutput(source);

			// Assert
			var warning = diagnostics.FirstOrDefault(d => d.Id == "ARG008");
			Assert.NotNull(warning);
			Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
		}
		
		[Fact]
		public void Diagnostic_MissingHelpTextWithHandler_ReportsError()
		{
			// Arrange
			var builder = new SourceCodeBuilder();
			builder.AddImports(null)
			    .AddClassAttribute("[ParameterCollection(helpTextGeneration: HelpTextGeneration.GenerateArgumentAndHandler)]")
			    .AddOptionParameter(new PropertyAndAttributeInfo
			    {
				    PropertyName = "Simple",
				    PropertyType = "string",
				    Attribute = new AttributeInfo("s", "Simple", "Simple option", false, -1)
			    }, null);
			var source = builder.ToString();

			// Act
			var (diagnostics, _) = TestHelper.GetGeneratedOutput(source);

			// Assert
			var error = diagnostics.FirstOrDefault(d => d.Id == "ARG009");
			Assert.NotNull(error);
			Assert.Equal(DiagnosticSeverity.Error, error.Severity);
		}
		
		[Fact]
		public void Diagnostic_MissingHelpTextWithErrorHandler_ReportsError()
		{
			// Arrange
			var builder = new SourceCodeBuilder();
			builder.AddImports(null)
			    .AddClassAttribute("[ParameterCollection(helpTextGeneration: HelpTextGeneration.None, behaviourOnError: BehaviourOnError.DisplayHelpAndExit)]")
			    .AddOptionParameter(new PropertyAndAttributeInfo
			    {
				    PropertyName = "Simple",
				    PropertyType = "string",
				    Attribute = new AttributeInfo("s", "Simple", "Simple option", false, -1)
			    }, null);
			var source = builder.ToString();

			// Act
			var (diagnostics, _) = TestHelper.GetGeneratedOutput(source);

			// Assert
			var error = diagnostics.FirstOrDefault(d => d.Id == "ARG010");
			Assert.NotNull(error);
			Assert.Equal(DiagnosticSeverity.Error, error.Severity);
		}
		
		[Fact]
		public void Diagnostic_DuplicateDisplayHelpProperty_ReportsError()
		{
			// Arrange
			var builder = new SourceCodeBuilder();
			builder.AddImports(null)
			    .AddClassAttribute("[ParameterCollection(helpTextGeneration: HelpTextGeneration.GenerateArgumentAndHandler)]")
			    // Add HelpText constant to avoid ARG009
			    .AddClassMember("public const string HelpText = \"Help text\";")
			    // Add DisplayHelp property which conflicts with generator
			    .AddClassMember("public bool DisplayHelp { get; set; }")
			    .AddOptionParameter(new PropertyAndAttributeInfo
			    {
				    PropertyName = "Simple",
				    PropertyType = "string",
				    Attribute = new AttributeInfo("s", "Simple", "Simple option", false, -1)
			    }, null);
			var source = builder.ToString();

			// Act
			var (diagnostics, _) = TestHelper.GetGeneratedOutput(source);

			// Assert
			var error = diagnostics.FirstOrDefault(d => d.Id == "ARG011");
			Assert.NotNull(error);
			Assert.Equal(DiagnosticSeverity.Error, error.Severity);
		}
		
		[Fact]
		public void Diagnostic_DuplicateHelpTextConstant_ReportsError()
		{
			// Arrange
			var builder = new SourceCodeBuilder();
			builder.AddImports(null)
			    .AddClassAttribute("[ParameterCollection(helpTextGeneration: HelpTextGeneration.GenerateAll)]")
			    // Add HelpText constant which conflicts with generator
			    .AddClassMember("public const string HelpText = \"Help text\";")
			    .AddOptionParameter(new PropertyAndAttributeInfo
			    {
				    PropertyName = "Simple",
				    PropertyType = "string",
				    Attribute = new AttributeInfo("s", "Simple", "Simple option", false, -1)
			    }, null);
			var source = builder.ToString();

			// Act
			var (diagnostics, _) = TestHelper.GetGeneratedOutput(source);

			// Assert
			var error = diagnostics.FirstOrDefault(d => d.Id == "ARG012");
			Assert.NotNull(error);
			Assert.Equal(DiagnosticSeverity.Error, error.Severity);
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
			Assert.Contains("flags can only be applied to boolean properties", error.GetMessage());
		}
	}
}