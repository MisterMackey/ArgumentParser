using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ArgumentParser.Internal.Utilities;

namespace ArgumentParser.Internal;

/// <summary>
/// Writes the help text of one level of a tree of commands: the root, a group or a leaf.
/// It shows the path that leads to the level, its own arguments, the commands below a root or group,
/// and the options of the levels above, which have to come before the command word of the level below them.
/// </summary>
public static class CommandHelpTextProvider
{
	/// <summary>
	/// Gets the levels above a node, the root first: the nodes whose path starts the path of the node.
	/// The names of the children of one node are unique (ARG017), so a path names exactly one node.
	/// </summary>
	/// <param name="tree">The tree the node is in.</param>
	/// <param name="node">The node.</param>
	/// <returns>The root, then every group on the way to the node; empty for the root itself.</returns>
	public static IReadOnlyList<TreeNode> Ancestors(CommandTree tree, TreeNode node)
	{
		if (tree is null)
		{
			throw new ArgumentNullException(nameof(tree));
		}
		if (node is null)
		{
			throw new ArgumentNullException(nameof(node));
		}
		var path = node.Path.AsReadOnly();
		return tree.Nodes.AsReadOnly()
			.Where(n => n.Path.Count < path.Count && n.Path.AsReadOnly().SequenceEqual(path.Take(n.Path.Count), StringComparer.Ordinal))
			.OrderBy(n => n.Path.Count)
			.ToList();
	}

	/// <summary>
	/// Writes the help text of a node.
	/// </summary>
	/// <param name="tree">The tree the node is in.</param>
	/// <param name="node">The node.</param>
	/// <returns>The help text, ending with a line break.</returns>
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
		var root = tree.Root.Source.Node;
		var settings = root.Settings;
		var ancestors = Ancestors(tree, node);
		var isLeaf = info.Kind == CommandNodeKind.Leaf;
		var arguments = node.Source.Arguments;
		var help = settings.HasHelpArgument ? $"Flag: {Names(settings.HelpShortName, settings.HelpLongName)}: {ArgumentProvider.HelpDescription}" : null;

		var text = new StringBuilder();
		var title = string.Join(" ", new[] { root.Name }.Concat(node.Path.AsReadOnly()));
		text.AppendLine(info.Description.Length > 0 ? $"{title} - {info.Description}" : title);
		text.AppendLine();

		// every level above the node is written with its options, which have to come before its command word
		text.AppendLine("Usage:");
		var usage = new StringBuilder("\t");
		foreach (var ancestor in ancestors)
		{
			usage.Append(Word(root, ancestor));
			if (HasOptions(ancestor))
			{
				usage.Append($" [{Word(root, ancestor)} options]");
			}
			usage.Append(' ');
		}
		usage.Append(Word(root, node));
		usage.Append(isLeaf ? " [optional args] (required args)" : " [options] <command> [command args]");
		text.AppendLine(usage.ToString());
		text.AppendLine();

		if (isLeaf)
		{
			var positionals = arguments.Positionals.AsReadOnly().OrderBy(p => p.Attribute.Position).ToList();
			var options = arguments.Options.AsReadOnly();
			var flags = arguments.Flags.AsReadOnly();
			Section(
				text,
				"Required arguments:",
				positionals.Where(p => p.Attribute.Required).Select(PositionalLine)
					.Concat(options.Where(o => o.Attribute.Required).Select(o => ArgumentLine("Option", o))));
			Section(
				text,
				"Optional arguments:",
				positionals.Where(p => !p.Attribute.Required).Select(PositionalLine)
					.Concat(flags.Select(f => ArgumentLine("Flag", f)))
					.Concat(options.Where(o => !o.Attribute.Required).Select(o => ArgumentLine("Option", o)))
					.Concat(help is null ? [] : [help]));
		}
		else
		{
			Section(
				text,
				"Commands:",
				node.Children.AsReadOnly().Select(c => $"{c.CommandName}{DescriptionOf(tree, c)}"));
			Section(text, "Options:", OptionLines(node).Concat(help is null ? [] : [help]));
		}

		foreach (var ancestor in ancestors.Where(HasOptions))
		{
			var next = node.Path[ancestor.Path.Count];
			Section(text, $"Options of {Word(root, ancestor)}, before '{next}':", OptionLines(ancestor));
		}
		return text.ToString();
	}

	// the root is written with the name of the application, every other level with its command word
	private static string Word(CommandNodeInfo root, TreeNode node)
	{
		return node.Path.Count == 0 ? root.Name : node.Path[node.Path.Count - 1];
	}

	private static bool HasOptions(TreeNode node)
	{
		return node.Source.Arguments.Options.Count > 0 || node.Source.Arguments.Flags.Count > 0;
	}

	private static IEnumerable<string> OptionLines(TreeNode node)
	{
		var arguments = node.Source.Arguments;
		return arguments.Flags.AsReadOnly().Select(f => ArgumentLine("Flag", f))
			.Concat(arguments.Options.AsReadOnly().Select(o => ArgumentLine("Option", o) + (o.Attribute.Required ? " (required)" : "")));
	}

	private static string DescriptionOf(CommandTree tree, TreeChild child)
	{
		var description = tree.Nodes.AsReadOnly().FirstOrDefault(n => n.Source.Node.TypeName == child.TypeName)?.Source.Node.Description ?? "";
		return description.Length > 0 ? ": " + description : "";
	}

	private static void Section(StringBuilder text, string heading, IEnumerable<string> lines)
	{
		text.AppendLine(heading);
		var any = false;
		foreach (var line in lines)
		{
			any = true;
			text.AppendLine("\t" + line);
		}
		if (!any)
		{
			text.AppendLine("\tNone");
		}
		text.AppendLine();
	}

	private static string PositionalLine(PropertyAndAttributeInfo positional)
	{
		return $"Position: {positional.Attribute.Position}: {positional.Attribute.Description}";
	}

	private static string ArgumentLine(string kind, PropertyAndAttributeInfo argument)
	{
		return $"{kind}: {Names(argument.Attribute.ShortName, argument.Attribute.LongName)}: {argument.Attribute.Description}";
	}

	private static string Names(string shortName, string longName)
	{
		var names = new List<string>();
		if (!string.IsNullOrEmpty(shortName))
		{
			names.Add("-" + shortName);
		}
		if (!string.IsNullOrEmpty(longName))
		{
			names.Add("--" + longName);
		}
		return string.Join(" | ", names) + " ";
	}
}
