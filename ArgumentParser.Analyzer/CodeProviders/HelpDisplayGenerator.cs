using System;
using System.CodeDom.Compiler;

namespace ArgumentParser.Analyzer;

/// <summary>
/// Provides methods to generate code for displaying help text and errors in the generated parser.
/// </summary>
public static class HelpDisplayGenerator
{
	/// <summary>
	/// Generates code to display help text and exit the program if the DisplayHelp property is set.
	/// </summary>
	/// <param name="writer">The <see cref="IndentedTextWriter"/> to write the generated code to.</param>
	/// <param name="className">The name of the class containing the help text.</param>
	public static void GenerateDisplayHelpText(IndentedTextWriter writer, string className)
	{
		if (writer == null) throw new ArgumentNullException(nameof(writer));
		if (string.IsNullOrEmpty(className)) throw new ArgumentNullException(nameof(className));

		writer.WriteLine("if (instance.DisplayHelp)");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine($"System.Console.WriteLine({className}.HelpText);");
		writer.WriteLine("Environment.Exit(0);");
		writer.Indent--;
		writer.WriteLine("}");
	}

	/// <summary>
	/// Generates code to display errors, help text, and exit the program if there are any errors.
	/// </summary>
	/// <param name="writer">The <see cref="IndentedTextWriter"/> to write the generated code to.</param>
	/// <param name="className">The name of the class containing the help text.</param>
	public static void GenerateDisplayHelpTextWithError(IndentedTextWriter writer, string className)
	{
		if (writer == null) throw new ArgumentNullException(nameof(writer));
		if (string.IsNullOrEmpty(className)) throw new ArgumentNullException(nameof(className));

		writer.WriteLine("if (errors.Any())");
		writer.WriteLine("{");
		writer.Indent++;
		// Display the errors
		writer.WriteLine("foreach (var error in errors)");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine("System.Console.WriteLine(error);");
		writer.Indent--;
		writer.WriteLine("}");
		writer.WriteLine("System.Console.WriteLine();");
		writer.WriteLine($"System.Console.WriteLine({className}.HelpText);");
		writer.WriteLine("Environment.Exit(2);");
		writer.Indent--;
		writer.WriteLine("}");
	}
}
