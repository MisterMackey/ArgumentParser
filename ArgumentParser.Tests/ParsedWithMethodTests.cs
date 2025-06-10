using Xunit;
using ArgumentParser.Internal;
using Microsoft.CodeAnalysis;

namespace ArgumentParser.Tests
{
	/// <summary>
	/// Example class to test the parsing of a custom class with a TryParse method.
	/// </summary>
	public class CustomClassWithTryParse()
	{
		public static bool TryParse(string input, out CustomClassWithTryParse result)
		{
			result = new CustomClassWithTryParse();
			// Simulate parsing logic
			return true;
		}
	}
	public class ParsedWithMethodTests
	{
		private readonly SourceCodeBuilder builder = new();
		public ParsedWithMethodTests()
		{ }

		[Fact]
		public void Generator_HandlesCustomClassWithTryParse()
		{
			// Arrange
			builder.AddImports(null)
				.AddOptionParameter(new PropertyAndAttributeInfo
				{
					PropertyName = "CustomClass",
					PropertyType = "CustomClassWithTryParse",
					Attribute = new AttributeInfo("c", "Custom", "Custom class with TryParse", false, -1)
				}, null, "TryParse");
			var source = builder.ToString();

			// Act
			var (diagnostics, output) = TestHelper.GetGeneratedOutput(source);

			// Assert
			Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
			Assert.Contains("if (!ArgumentParser.Tests.CustomClassWithTryParse.TryParse", output);
			Assert.Contains("out var parsedValue", output);
			Assert.Contains("CustomClass = parsedValue", output);
		}
	}
}
