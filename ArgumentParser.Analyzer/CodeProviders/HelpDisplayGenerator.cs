using System;
using System.CodeDom.Compiler;

namespace ArgumentParser.Analyzer;

public static class HelpDisplayGenerator
{
	public static void GenerateDisplayHelpText(IndentedTextWriter writer)
	{
		if (writer == null) throw new ArgumentNullException(nameof(writer));

		writer.WriteLine("if (this.DisplayHelp)");
		writer.Indent++;
		writer.WriteLine("{");
		writer.WriteLine("System.Console.WriteLine(this.HelpText);");
		writer.WriteLine("Environment.Exit(0);");
		writer.Indent--;
		writer.WriteLine("}");
	}

	public static void GenerateDisplayHelpTextWithError(IndentedTextWriter writer)
	{
		if (writer == null) throw new ArgumentNullException(nameof(writer));

		writer.WriteLine("if (errors.Any())");
		writer.Indent++;
		writer.WriteLine("{");
		// Display the errors
		writer.WriteLine("foreach (var error in errors)");
		writer.Indent++;
		writer.WriteLine("{");
		writer.WriteLine("System.Console.WriteLine(error);");
		writer.Indent--;
		writer.WriteLine("}");
		writer.WriteLine("System.Console.WriteLine();");
		writer.WriteLine("System.Console.WriteLine(this.HelpText);");
		writer.WriteLine("Environment.Exit(2);");
		writer.Indent--;
		writer.WriteLine("}");
	}
}
