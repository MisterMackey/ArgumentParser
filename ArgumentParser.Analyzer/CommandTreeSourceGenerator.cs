using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArgumentParser.Internal.Utilities;

namespace ArgumentParser.Internal;

// TODO: maybe an extra review pass on this compilation unit

/// <summary>
/// Generates the source for the classes of a tree of commands that have child commands: the root and the groups.
/// Their <c>ParseCore</c> reads the options and flags of the class, then hands the arguments after the command word to the child it names.
/// The leaves are not generated here, they have the parser of their ParameterCollection attribute.
/// </summary>
public static class CommandTreeSourceGenerator
{
	/// <summary>
	/// Gets the name of the generated file for a node.
	/// </summary>
	/// <param name="node">The root or group.</param>
	/// <returns>The hint name to add the source with.</returns>
	public static string HintName(TreeNode node)
	{
		if (node is null)
		{
			throw new ArgumentNullException(nameof(node));
		}
		var info = node.Source.Node;
		return $"{ContainingType.QualifiedName(info.NamespaceName, info.ContainingTypes, info.ClassName)}_Command.g.cs";
	}

	/// <summary>
	/// Generates the partial class for a root or a group.
	/// </summary>
	/// <param name="tree">The tree the node is in.</param>
	/// <param name="node">The root or group to generate.</param>
	/// <returns>The source text.</returns>
	public static string Generate(CommandTree tree, TreeNode node)
	{
		if (tree is null)
		{
			throw new ArgumentNullException(nameof(tree));
		}
		if (node is null)
		{
			throw new ArgumentNullException(nameof(node));
		}
		var info = node.Source.Node;
		if (info.Kind == CommandNodeKind.Leaf)
		{
			throw new ArgumentException("A leaf has the parser of its ParameterCollection attribute.", nameof(node));
		}
		var className = info.ClassName;
		var options = node.Source.Arguments.Options.AsReadOnly();
		var flags = node.Source.Arguments.Flags.AsReadOnly();
		var noPositionals = Array.Empty<PropertyAndAttributeInfo>();

		using var writer = new IndentedTextWriter(new StringWriter());
		SourceTextGenerator.WriteClassStart(writer, info.NamespaceName, info.ContainingTypes, className);

		SourceTextGenerator.WriteArgumentTables(writer, options, null, flags);
		writer.WriteLine();

		// ParseCore reads the options of this level and hands the rest to the command that was named
		writer.WriteLine($"internal static ({className} result, List<global::ArgumentParser.ArgumentParserException> errors, List<global::ArgumentParser.MissingRequiredArgumentException> missingRequired) ParseCore(string[] args)");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine("var tokenizer = new global::ArgumentParser.ArgumentTokenizer();");
		writer.WriteLine("var (tokens, errors, commandIndex) = tokenizer.TokenizeLeadingArguments(args, options, flags);");
		writer.WriteLine($"var instance = new {className}();");
		SourceTextGenerator.WriteRequiredProperties(writer, options, noPositionals);
		SourceTextGenerator.WriteTokenLoop(writer, options, noPositionals, flags);
		SourceTextGenerator.WriteMissingRequired(writer);
		WriteDispatch(writer, node);
		writer.WriteLine("return (instance, errors, missingRequired);");
		writer.Indent--;
		writer.WriteLine("}");

		if (info.Kind == CommandNodeKind.Root)
		{
			writer.WriteLine();
			WriteParse(writer, className);
			writer.WriteLine();
			WriteCommands(writer, tree);
		}

		SourceTextGenerator.WriteClassEnd(writer, info.NamespaceName, info.ContainingTypes);
		writer.Flush();
		var text = writer.InnerWriter.ToString();
		writer.Close();
		return text;
	}

	private static void WriteDispatch(IndentedTextWriter writer, TreeNode node)
	{
		var names = node.Children.AsReadOnly().Select(item => item.CommandName).ToList();
		var expected = SourceTextGenerator.Literal(" Expected one of: " + string.Join(", ", names) + ".");

		writer.WriteLine("if (commandIndex >= args.Length)");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine($"errors.Add(new global::ArgumentParser.MissingCommandException(\"Missing command.\" + {expected}));");
		writer.WriteLine("return (instance, errors, missingRequired);");
		writer.Indent--;
		writer.WriteLine("}");

		writer.WriteLine("var remaining = args.Skip(commandIndex + 1).ToArray();");
		writer.WriteLine("switch (args[commandIndex])");
		writer.WriteLine("{");
		writer.Indent++;
		for (int i = 0; i < node.Children.Count; i++)
		{
			var child = node.Children[i];
			writer.WriteLine($"case {SourceTextGenerator.Literal(child.CommandName)}:");
			writer.WriteLine("{");
			writer.Indent++;
			writer.WriteLine($"var (child, childErrors, childMissing) = global::{child.TypeName}.ParseCore(remaining);");
			writer.WriteLine($"instance.{child.PropertyName} = child;");
			writer.WriteLine("errors.AddRange(childErrors);");
			writer.WriteLine("missingRequired.AddRange(childMissing);");
			writer.WriteLine("break;");
			writer.Indent--;
			writer.WriteLine("}");
		}
		writer.WriteLine("default:");
		writer.Indent++;
		writer.WriteLine($"errors.Add(new global::ArgumentParser.UnknownCommandException(\"Unknown command '\" + args[commandIndex] + \"'.\" + {expected}));");
		writer.WriteLine("break;");
		writer.Indent--;
		writer.Indent--;
		writer.WriteLine("}");
	}

	private static void WriteParse(IndentedTextWriter writer, string className)
	{
		writer.WriteLine($"public static ({className} result, List<global::ArgumentParser.ArgumentParserException> errors) Parse(string[] args)");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine("var (instance, errors, _) = ParseCore(args);");
		writer.WriteLine("return (instance, errors);");
		writer.Indent--;
		writer.WriteLine("}");
	}

	private static void WriteCommands(IndentedTextWriter writer, CommandTree tree)
	{
		writer.WriteLine("public static IReadOnlyList<global::ArgumentParser.CommandInfo> Commands { get; } = new global::ArgumentParser.CommandInfo[] {");
		writer.Indent++;
		foreach (var leaf in tree.Nodes.AsReadOnly().Where(n => n.Source.Node.Kind == CommandNodeKind.Leaf))
		{
			var info = leaf.Source.Node;
			var path = string.Join(", ", leaf.Path.AsReadOnly().Select(item => SourceTextGenerator.Literal(item)));
			var annotations = info.Annotations == 0
				? "global::ArgumentParser.CommandAnnotations.None"
				: $"(global::ArgumentParser.CommandAnnotations)({info.Annotations})";
			writer.WriteLine("new global::ArgumentParser.CommandInfo(");
			writer.Indent++;
			writer.WriteLine($"new string[] {{ {path} }},");
			writer.WriteLine($"{SourceTextGenerator.Literal(info.Description)},");
			writer.WriteLine($"{annotations},");
			writer.WriteLine("args =>");
			writer.WriteLine("{");
			writer.Indent++;
			writer.WriteLine($"var (result, errors, _) = global::{info.TypeName}.ParseCore(args);");
			writer.WriteLine("return (result, errors);");
			writer.Indent--;
			writer.WriteLine("}),");
			writer.Indent--;
		}
		writer.Indent--;
		writer.WriteLine("};");
	}
}
