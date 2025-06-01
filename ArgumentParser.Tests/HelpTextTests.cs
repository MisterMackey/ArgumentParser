using System.Linq;
using System.Threading.Tasks;
using Xunit;
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
				.AddClassAttribute("[ParameterCollection(helpTextGeneration: HelpTextGeneration.GenerateAll)]")
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
	}
}