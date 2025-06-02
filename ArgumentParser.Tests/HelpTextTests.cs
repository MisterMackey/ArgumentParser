using Microsoft.CodeAnalysis;
using ArgumentParser.Internal;

namespace ArgumentParser.Tests
{
	/// <summary>
	/// Tests focused on the help text, both generation and display cq adding to the generated code.
	/// </summary>
	public class HelpTextTests
	{
		[Fact]
		public void HelpText_GenerateAll_GeneratesHelpTextAndArgument()
		{
			// Arrange
			var builder = new SourceCodeBuilder();
			builder.AddImports(null)
				// we set thrownever to ensure we only test the help text generation, the default err behaviour is to display helptext
				.AddClassAttribute("[ParameterCollection(helpTextGeneration: HelpTextGeneration.GenerateAll, behaviourOnError: BehaviourOnError.ThrowNever)]")
				.AddPositionalParameter(new PropertyAndAttributeInfo
				{
					PropertyName = "RequiredPositional",
					PropertyType = "string",
					Attribute = new AttributeInfo("", "", "", true, 0)
				}, null)
				.AddPositionalParameter(new PropertyAndAttributeInfo
				{
					PropertyName = "OptionalPositional",
					PropertyType = "string",
					Attribute = new AttributeInfo("", "", "", false, 1)
				}, null);
			var source = builder.ToString();

			// Act
			var (diagnostics, output) = TestHelper.GetGeneratedOutput(source);

			// Assert
			Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
			// const string is generated and added
			Assert.Contains("public const string HelpText =", output);
			Assert.Contains("Required arguments", output);
			Assert.Contains("Optional arguments", output);
			// property for DisplayHelp is generated
			Assert.Contains("public bool DisplayHelp { get; set; }", output);
			// helptext is written at some point
			Assert.Contains("if (instance.DisplayHelp)", output);
			Assert.Contains("System.Console.WriteLine(TestClass.HelpText);", output);
		}

		[Fact]
		public void HelpText_GenerateNone_DoesNotGenerateAnything()
		{
			// Arrange
			var builder = new SourceCodeBuilder();
			builder.AddImports(null)
				.AddClassAttribute("[ParameterCollection(helpTextGeneration: HelpTextGeneration.None, behaviourOnError: BehaviourOnError.ThrowNever)]")
				.AddPositionalParameter(new PropertyAndAttributeInfo
				{
					PropertyName = "RequiredPositional",
					PropertyType = "string",
					Attribute = new AttributeInfo("", "", "", true, 0)
				}, null)
				.AddPositionalParameter(new PropertyAndAttributeInfo
				{
					PropertyName = "OptionalPositional",
					PropertyType = "string",
					Attribute = new AttributeInfo("", "", "", false, 1)
				}, null);
			var source = builder.ToString();

			// Act
			var (diagnostics, output) = TestHelper.GetGeneratedOutput(source);

			// Assert
			Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
			Assert.DoesNotContain("public const string HelpText =", output);
			Assert.DoesNotContain("public bool DisplayHelp { get; set; }", output);
			Assert.DoesNotContain("if (instance.DisplayHelp)", output);
			Assert.DoesNotContain("System.Console.WriteLine(TestClass.HelpText);", output);
		}

		[Fact]
		public void HelpText_GenerateTextOnly_OnlyGeneratesText()
		{
			// Arrange
			var builder = new SourceCodeBuilder();
			builder.AddImports(null)
				.AddClassAttribute("[ParameterCollection(helpTextGeneration: HelpTextGeneration.GenerateTextOnly, behaviourOnError: BehaviourOnError.ThrowNever)]")
				.AddPositionalParameter(new PropertyAndAttributeInfo
				{
					PropertyName = "RequiredPositional",
					PropertyType = "string",
					Attribute = new AttributeInfo("", "", "", true, 0)
				}, null)
				.AddPositionalParameter(new PropertyAndAttributeInfo
				{
					PropertyName = "OptionalPositional",
					PropertyType = "string",
					Attribute = new AttributeInfo("", "", "", false, 1)
				}, null);
			var source = builder.ToString();

			// Act
			var (diagnostics, output) = TestHelper.GetGeneratedOutput(source);

			// Assert
			Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
			Assert.Contains("public const string HelpText =", output);
			Assert.DoesNotContain("public bool DisplayHelp { get; set; }", output);
			Assert.DoesNotContain("if (instance.DisplayHelp)", output);
			Assert.DoesNotContain("System.Console.WriteLine(TestClass.HelpText);", output);
		}

		[Fact]
		public void HelpText_GenerateArgumentOnly_OnlyGeneratesArgument()
		{
			// Arrange
			var builder = new SourceCodeBuilder();
			builder.AddImports(null)
				.AddClassAttribute("[ParameterCollection(helpTextGeneration: HelpTextGeneration.GenerateArgumentOnly, behaviourOnError: BehaviourOnError.ThrowNever)]")
				.AddPositionalParameter(new PropertyAndAttributeInfo
				{
					PropertyName = "RequiredPositional",
					PropertyType = "string",
					Attribute = new AttributeInfo("", "", "", true, 0)
				}, null)
				.AddPositionalParameter(new PropertyAndAttributeInfo
				{
					PropertyName = "OptionalPositional",
					PropertyType = "string",
					Attribute = new AttributeInfo("", "", "", false, 1)
				}, null);
			var source = builder.ToString();

			// Act
			var (diagnostics, output) = TestHelper.GetGeneratedOutput(source);

			// Assert
			Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
			Assert.DoesNotContain("public const string HelpText =", output);
			Assert.Contains("public bool DisplayHelp { get; set; }", output);
			Assert.DoesNotContain("if (instance.DisplayHelp)", output);
			Assert.DoesNotContain("System.Console.WriteLine(TestClass.HelpText);", output);
		}

		[Fact]
		public void HelpText_GenerateArgumentAndHandler_GeneratesArgumentAndHandlerNoText()
		{
			// Arrange
			var builder = new SourceCodeBuilder();
			builder.AddImports(null)
				.AddClassAttribute("[ParameterCollection(helpTextGeneration: HelpTextGeneration.GenerateArgumentAndHandler, behaviourOnError: BehaviourOnError.ThrowNever)]")
				.AddPositionalParameter(new PropertyAndAttributeInfo
				{
					PropertyName = "RequiredPositional",
					PropertyType = "string",
					Attribute = new AttributeInfo("", "", "", true, 0)
				}, null)
				.AddPositionalParameter(new PropertyAndAttributeInfo
				{
					PropertyName = "OptionalPositional",
					PropertyType = "string",
					Attribute = new AttributeInfo("", "", "", false, 1)
				}, null)
				// we need to add the const string here otherwise we error out
				.AddClassMember("public const string HelpText = \"\";");
			var source = builder.ToString();

			// Act
			var (diagnostics, output) = TestHelper.GetGeneratedOutput(source);

			// Assert
			Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
			// this property is there in the source text, but not in the generated output
			Assert.DoesNotContain("public const string HelpText =", output);
			Assert.Contains("public bool DisplayHelp { get; set; }", output);
			Assert.Contains("if (instance.DisplayHelp)", output);
			Assert.Contains("System.Console.WriteLine(TestClass.HelpText);", output);
		}
	}
}