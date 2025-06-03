using ArgumentParser.Internal;
using Xunit;

namespace ArgumentParser.Tests
{
	public class BehaviourOnErrorTests
	{
		[Fact]
		public void BehaviourOnError_DefaultIsDisplayHelpAndExit_GeneratesHandler()
		{
			// Arrange
			var builder = new SourceCodeBuilder();
			builder.AddImports(null)
			    // Do not specify behaviourOnError, should default to DisplayHelpAndExit
			    // helptext set to none so we don't accidentily mistake the handler it generates for the one we are looking for
			    .AddClassAttribute("[ParameterCollection(helpTextGeneration: HelpTextGeneration.None)]")
			    .AddClassMember("public const string HelpText = \"\";");
			var source = builder.ToString();

			// Act
			var (diagnostics, output) = TestHelper.GetGeneratedOutput(source);

			// Assert
			Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
			// Should generate handler for displaying help on error
			Assert.Contains("if (errors.Any())", output);
			Assert.Contains("System.Console.WriteLine(TestClass.HelpText);", output);
			Assert.Contains("Environment.Exit(2);", output);
		}

		[Fact]
		public void BehaviourOnError_ThrowIfAnyError_GeneratesThrowException()
		{
			// Arrange
			var builder = new SourceCodeBuilder();
			builder.AddImports(null)
			    .AddClassAttribute("[ParameterCollection(helpTextGeneration: HelpTextGeneration.None, behaviourOnError: BehaviourOnError.ThrowIfAnyError)]")
			    .AddClassMember("public const string HelpText = \"\";");
			var source = builder.ToString();

			// Act
			var (diagnostics, output) = TestHelper.GetGeneratedOutput(source);

			// Assert
			Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
			// Should generate throw exception for errors
			Assert.Contains("if (errors.Count > 0)", output);
			Assert.Contains("new System.AggregateException", output);
			Assert.Contains("One or more errors occurred ", output);
		}

		[Fact]
		public void BehaviourOnError_ThrowNever_DoesNotGenerateErrorHandling()
		{
			// Arrange
			var builder = new SourceCodeBuilder();
			builder.AddImports(null)
			    .AddClassAttribute("[ParameterCollection(helpTextGeneration: HelpTextGeneration.None, behaviourOnError: BehaviourOnError.ThrowNever)]")
			    .AddClassMember("public const string HelpText = \"\";");
			var source = builder.ToString();

			// Act
			var (diagnostics, output) = TestHelper.GetGeneratedOutput(source);

			// Assert
			Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
			// Should not generate any error handling code
			Assert.DoesNotContain("throw ", output);
		}

		[Fact]
		public void BehaviourOnError_ThrowIfMissingRequired_GeneratesThrowExceptionForMissingRequired()
		{
			// Arrange
			var builder = new SourceCodeBuilder();
			builder.AddImports(null)
			    .AddClassAttribute("[ParameterCollection(helpTextGeneration: HelpTextGeneration.None, behaviourOnError: BehaviourOnError.ThrowIfMissingRequired)]")
			    .AddPositionalParameter(new PropertyAndAttributeInfo
			    {
				    PropertyName = "RequiredPositional",
				    PropertyType = "string",
				    Attribute = new AttributeInfo("", "", "", true, 0)
			    }, null);
			var source = builder.ToString();

			// Act
			var (diagnostics, output) = TestHelper.GetGeneratedOutput(source);

			// Assert
			Assert.Empty(diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
			// Should generate throw exception for missing required parameters
			Assert.Contains("if (missingRequired.Count > 0)", output);
			Assert.Contains("new System.AggregateException", output);
			Assert.Contains("One or more required arguments missing", output);
		}
	}
}
