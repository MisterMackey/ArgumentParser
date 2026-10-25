using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArgumentParser.Internal.Utilities;

namespace ArgumentParser.Internal;

// TODO: maybe an extra review pass on this compilation unit

/// <summary>
/// Generates the source a tree of commands adds to each of its classes: the help text of the level (<c>CommandHelpText</c>)
/// and <c>ParseCommand</c>, which parses the arguments of the level and returns whether the help argument of the tree was given
/// and which help text belongs to the outcome. For the root and the groups it reads the options and flags of the class, then hands
/// the arguments after the command word to the child it names; for a leaf it calls the parser of its ParameterCollection attribute.
/// The root also gets the public <c>Parse</c>, which applies the help and error settings of the tree, and the <c>Commands</c> list.
/// </summary>
public static class CommandTreeSourceGenerator
{
	private const string HelpTextName = "CommandHelpText";
	private const string HelpArgumentName = "CommandHelpArgument";

	/// <summary>
	/// Gets the name of the generated file for a node.
	/// </summary>
	/// <param name="node">The node.</param>
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
	/// Generates the partial class for a node of a tree.
	/// </summary>
	/// <param name="tree">The tree the node is in.</param>
	/// <param name="node">The node to generate.</param>
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
		var settings = tree.Root.Source.Node.Settings;
		var className = info.ClassName;
		var helpText = settings.GeneratesHelpText ? HelpTextName : "null";

		using var writer = new IndentedTextWriter(new StringWriter());
		// a leaf already has the part of its ParameterCollection attribute, which carries the GeneratedCode attribute
		SourceTextGenerator.WriteClassStart(writer, info.NamespaceName, info.ContainingTypes, className, markAsGenerated: info.Kind != CommandNodeKind.Leaf);

		if (settings.GeneratesHelpText)
		{
			writer.Write(HelptextProvider.ConstDeclaration(HelpTextName, CommandHelpTextProvider.Generate(tree, node)));
			writer.WriteLine();
		}
		if (settings.HasHelpArgument)
		{
			writer.WriteLine($"internal static readonly global::ArgumentParser.FlagAttribute {HelpArgumentName} = new global::ArgumentParser.FlagAttribute({SourceTextGenerator.Literal(settings.HelpShortName)}, {SourceTextGenerator.Literal(settings.HelpLongName)}, {SourceTextGenerator.Literal(ArgumentProvider.HelpDescription)});");
			writer.WriteLine();
		}

		var signature = $"internal static ({className} result, List<global::ArgumentParser.ArgumentParserException> errors, List<global::ArgumentParser.MissingRequiredArgumentException> missingRequired, bool helpRequested, string helpText) ParseCommand(string[] args)";
		if (info.Kind == CommandNodeKind.Leaf)
		{
			// the parser of the ParameterCollection attribute, with the help argument of the tree instead of its own
			writer.WriteLine(signature);
			writer.WriteLine("{");
			writer.Indent++;
			writer.WriteLine($"var (instance, errors, missingRequired, helpRequested) = ParseCore(args, {SourceTextGenerator.CommandFlagsName}, {(settings.HasHelpArgument ? HelpArgumentName : "null")});");
			writer.WriteLine($"return (instance, errors, missingRequired, helpRequested, {helpText});");
			writer.Indent--;
			writer.WriteLine("}");
		}
		else
		{
			WriteGroupParseCommand(writer, node, settings, signature, helpText);
		}

		if (info.Kind == CommandNodeKind.Root)
		{
			writer.WriteLine();
			WriteParse(writer, className, settings);
			writer.WriteLine();
			WriteCommands(writer, tree, settings);
		}

		SourceTextGenerator.WriteClassEnd(writer, info.NamespaceName, info.ContainingTypes);
		writer.Flush();
		var text = writer.InnerWriter.ToString();
		writer.Close();
		return text;
	}

	// reads the options of this level and hands the rest to the command that was named
	private static void WriteGroupParseCommand(IndentedTextWriter writer, TreeNode node, NodeSettings settings, string signature, string helpText)
	{
		var options = node.Source.Arguments.Options.AsReadOnly();
		var flags = node.Source.Arguments.Flags.AsReadOnly();
		var noPositionals = Array.Empty<PropertyAndAttributeInfo>();
		// the help argument is matched by the tokenizer like a flag, but no property is set for it
		SourceTextGenerator.WriteArgumentTables(writer, options, null, flags, settings.HasHelpArgument ? HelpArgumentName : null);
		writer.WriteLine();

		writer.WriteLine(signature);
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine("var tokenizer = new global::ArgumentParser.ArgumentTokenizer();");
		writer.WriteLine("var (tokens, errors, commandIndex) = tokenizer.TokenizeLeadingArguments(args, options, flags);");
		writer.WriteLine($"var instance = new {node.Source.Node.ClassName}();");
		SourceTextGenerator.WriteRequiredProperties(writer, options, noPositionals);
		SourceTextGenerator.WriteTokenLoop(writer, options, noPositionals, flags);
		SourceTextGenerator.WriteMissingRequired(writer);
		if (settings.HasHelpArgument)
		{
			// the help argument shows the help of the level it is given on, whatever follows it
			writer.WriteLine($"if (global::ArgumentParser.ArgumentTokenizer.ContainsFlag(tokens, {HelpArgumentName}))");
			writer.WriteLine("{");
			writer.Indent++;
			writer.WriteLine($"return (instance, errors, missingRequired, true, {helpText});");
			writer.Indent--;
			writer.WriteLine("}");
		}
		WriteDispatch(writer, node, helpText);
		writer.Indent--;
		writer.WriteLine("}");
	}

	private static void WriteDispatch(IndentedTextWriter writer, TreeNode node, string helpText)
	{
		var names = node.Children.AsReadOnly().Select(item => item.CommandName).ToList();
		var expected = SourceTextGenerator.Literal(" Expected one of: " + string.Join(", ", names) + ".");

		writer.WriteLine("if (commandIndex >= args.Length)");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine($"errors.Add(new global::ArgumentParser.MissingCommandException(\"Missing command.\" + {expected}));");
		writer.WriteLine($"return (instance, errors, missingRequired, false, {helpText});");
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
			writer.WriteLine($"var (child, childErrors, childMissing, childHelpRequested, childHelpText) = global::{child.TypeName}.ParseCommand(remaining);");
			writer.WriteLine($"instance.{child.PropertyName} = child;");
			writer.WriteLine("errors.AddRange(childErrors);");
			writer.WriteLine("missingRequired.AddRange(childMissing);");
			// the help of the deepest level that was reached goes with the outcome
			writer.WriteLine("return (instance, errors, missingRequired, childHelpRequested, childHelpText);");
			writer.Indent--;
			writer.WriteLine("}");
		}
		writer.WriteLine("default:");
		writer.Indent++;
		writer.WriteLine($"errors.Add(new global::ArgumentParser.UnknownCommandException(\"Unknown command '\" + args[commandIndex] + \"'.\" + {expected}));");
		writer.WriteLine($"return (instance, errors, missingRequired, false, {helpText});");
		writer.Indent--;
		writer.Indent--;
		writer.WriteLine("}");
	}

	private static void WriteParse(IndentedTextWriter writer, string className, NodeSettings settings)
	{
		writer.WriteLine($"public static ({className} result, List<global::ArgumentParser.ArgumentParserException> errors) Parse(string[] args)");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine("var (instance, errors, missingRequired, helpRequested, helpText) = ParseCommand(args);");
		writer.WriteLine($"global::ArgumentParser.ParseOutcomePolicy.Apply({settings.BehaviourExpression}, helpRequested, helpText, errors, missingRequired);");
		writer.WriteLine("return (instance, errors);");
		writer.Indent--;
		writer.WriteLine("}");
	}

	private static void WriteCommands(IndentedTextWriter writer, CommandTree tree, NodeSettings settings)
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
			writer.WriteLine(settings.GeneratesHelpText ? $"global::{info.TypeName}.{HelpTextName}," : "\"\",");
			writer.WriteLine("args =>");
			writer.WriteLine("{");
			writer.Indent++;
			writer.WriteLine($"var (result, errors, _, _, _) = global::{info.TypeName}.ParseCommand(args);");
			writer.WriteLine("return (result, errors);");
			writer.Indent--;
			writer.WriteLine("}),");
			writer.Indent--;
		}
		writer.Indent--;
		writer.WriteLine("};");
	}
}
