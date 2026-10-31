using System;
using Microsoft.CodeAnalysis;

namespace ArgumentParser.Internal.Utilities;

/// <summary>
/// The options and flags a node declares, as plain data.
/// The tree generates the parsing of a group or the root from them. A leaf has them too, so the names can be compared along the tree,
/// but its parser, its positional arguments and the rules about its arguments belong to the pipeline of the ParameterCollection attribute.
/// </summary>
public sealed class NodeArguments : IEquatable<NodeArguments>
{
	/// <summary>
	/// Gets the arguments of a node that has none.
	/// </summary>
	public static NodeArguments Empty { get; } = new NodeArguments(default, default, default);

	/// <summary>
	/// Gets the properties with the Option attribute.
	/// </summary>
	public EquatableArray<PropertyAndAttributeInfo> Options { get; }

	/// <summary>
	/// Gets the properties with the Flag attribute.
	/// </summary>
	public EquatableArray<PropertyAndAttributeInfo> Flags { get; }

	/// <summary>
	/// Gets the properties with the Positional attribute of a leaf, for its help text. A group or the root has none (ARG015).
	/// </summary>
	public EquatableArray<PropertyAndAttributeInfo> Positionals { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="NodeArguments"/> class.
	/// </summary>
	/// <param name="options">The properties with the Option attribute.</param>
	/// <param name="flags">The properties with the Flag attribute.</param>
	/// <param name="positionals">The properties with the Positional attribute of a leaf.</param>
	public NodeArguments(EquatableArray<PropertyAndAttributeInfo> options, EquatableArray<PropertyAndAttributeInfo> flags, EquatableArray<PropertyAndAttributeInfo> positionals)
	{
		Options = options;
		Flags = flags;
		Positionals = positionals;
	}

	/// <inheritdoc/>
	public bool Equals(NodeArguments? other)
	{
		return other is not null && Options.Equals(other.Options) && Flags.Equals(other.Flags) && Positionals.Equals(other.Positionals);
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj)
	{
		return Equals(obj as NodeArguments);
	}

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		return (((Options.GetHashCode() * 23) + Flags.GetHashCode()) * 23) + Positionals.GetHashCode();
	}
}

/// <summary>
/// What linking needs to know about one node: where it sits and what it declares, without any source location.
/// An edit that only moves code leaves it equal, so the tree is not linked and generated again for it.
/// </summary>
public sealed class CommandNodeSource : IEquatable<CommandNodeSource>
{
	/// <summary>
	/// Gets the node.
	/// </summary>
	public CommandNodeInfo Node { get; }

	/// <summary>
	/// Gets the options and flags of the node.
	/// </summary>
	public NodeArguments Arguments { get; }

	/// <summary>
	/// Gets a value indicating whether reading the node reported an error. A tree with such a node is not generated.
	/// </summary>
	public bool HasErrors { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="CommandNodeSource"/> class.
	/// </summary>
	/// <param name="node">The node.</param>
	/// <param name="arguments">The options and flags of the node.</param>
	/// <param name="hasErrors">Whether reading the node reported an error.</param>
	public CommandNodeSource(CommandNodeInfo node, NodeArguments arguments, bool hasErrors)
	{
		Node = node ?? throw new ArgumentNullException(nameof(node));
		Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
		HasErrors = hasErrors;
	}

	/// <inheritdoc/>
	public bool Equals(CommandNodeSource? other)
	{
		return other is not null && Node.Equals(other.Node) && Arguments.Equals(other.Arguments) && HasErrors == other.HasErrors;
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj)
	{
		return Equals(obj as CommandNodeSource);
	}

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		int hash = 17;
		hash = hash * 23 + Node.GetHashCode();
		hash = hash * 23 + Arguments.GetHashCode();
		hash = hash * 23 + HasErrors.GetHashCode();
		return hash;
	}
}

/// <summary>
/// A child command of a node: the property that holds it and the node its type refers to.
/// </summary>
public sealed class TreeChild : IEquatable<TreeChild>
{
	/// <summary>
	/// Gets the name of the property of the parent that holds the child.
	/// </summary>
	public string PropertyName { get; }

	/// <summary>
	/// Gets the type of the child node as it is displayed.
	/// </summary>
	public string TypeName { get; }

	/// <summary>
	/// Gets the name of the child command, the word a user types to select it.
	/// </summary>
	public string CommandName { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="TreeChild"/> class.
	/// </summary>
	/// <param name="propertyName">The name of the property of the parent that holds the child.</param>
	/// <param name="typeName">The type of the child node as it is displayed.</param>
	/// <param name="commandName">The name of the child command.</param>
	public TreeChild(string propertyName, string typeName, string commandName)
	{
		PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
		TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
		CommandName = commandName ?? throw new ArgumentNullException(nameof(commandName));
	}

	/// <inheritdoc/>
	public bool Equals(TreeChild? other)
	{
		return other is not null
			&& PropertyName == other.PropertyName
			&& TypeName == other.TypeName
			&& CommandName == other.CommandName;
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj)
	{
		return Equals(obj as TreeChild);
	}

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		int hash = 17;
		hash = hash * 23 + PropertyName.GetHashCode();
		hash = hash * 23 + TypeName.GetHashCode();
		hash = hash * 23 + CommandName.GetHashCode();
		return hash;
	}
}

/// <summary>
/// A node as it sits in a linked tree.
/// </summary>
public sealed class TreeNode : IEquatable<TreeNode>
{
	/// <summary>
	/// Gets the node.
	/// </summary>
	public CommandNodeSource Source { get; }

	/// <summary>
	/// Gets the names of the commands that lead to the node, without the name of the application. Empty for the root.
	/// </summary>
	public EquatableArray<string> Path { get; }

	/// <summary>
	/// Gets the child commands in the order they are declared.
	/// </summary>
	public EquatableArray<TreeChild> Children { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="TreeNode"/> class.
	/// </summary>
	/// <param name="source">The node.</param>
	/// <param name="path">The names of the commands that lead to the node.</param>
	/// <param name="children">The child commands in the order they are declared.</param>
	public TreeNode(CommandNodeSource source, EquatableArray<string> path, EquatableArray<TreeChild> children)
	{
		Source = source ?? throw new ArgumentNullException(nameof(source));
		Path = path;
		Children = children;
	}

	/// <inheritdoc/>
	public bool Equals(TreeNode? other)
	{
		return other is not null && Source.Equals(other.Source) && Path.Equals(other.Path) && Children.Equals(other.Children);
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj)
	{
		return Equals(obj as TreeNode);
	}

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		int hash = 17;
		hash = hash * 23 + Source.GetHashCode();
		hash = hash * 23 + Path.GetHashCode();
		hash = hash * 23 + Children.GetHashCode();
		return hash;
	}
}

/// <summary>
/// All the nodes that can be reached from one class with the CommandApp attribute.
/// </summary>
public sealed class CommandTree : IEquatable<CommandTree>
{
	/// <summary>
	/// Gets the nodes, the root first, then every node after its parent in the order the children are declared.
	/// </summary>
	public EquatableArray<TreeNode> Nodes { get; }

	/// <summary>
	/// Gets a value indicating whether the tree has an error, so that no code is generated for it.
	/// </summary>
	public bool HasErrors { get; }

	/// <summary>
	/// Gets the root.
	/// </summary>
	public TreeNode Root => Nodes[0];

	/// <summary>
	/// Initializes a new instance of the <see cref="CommandTree"/> class.
	/// </summary>
	/// <param name="nodes">The nodes, the root first.</param>
	/// <param name="hasErrors">Whether the tree has an error.</param>
	public CommandTree(EquatableArray<TreeNode> nodes, bool hasErrors)
	{
		if (nodes.Count == 0)
		{
			throw new ArgumentException("A tree has at least a root.", nameof(nodes));
		}
		Nodes = nodes;
		HasErrors = hasErrors;
	}

	/// <inheritdoc/>
	public bool Equals(CommandTree? other)
	{
		return other is not null && Nodes.Equals(other.Nodes) && HasErrors == other.HasErrors;
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj)
	{
		return Equals(obj as CommandTree);
	}

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		return (Nodes.GetHashCode() * 23) + HasErrors.GetHashCode();
	}
}

/// <summary>
/// A rule that the tree as a whole breaks, as plain data. It names the class (and the property) it is about,
/// the source locations are looked up when it is turned into a diagnostic.
/// </summary>
public sealed class LinkProblem : IEquatable<LinkProblem>
{
	/// <summary>
	/// Gets the descriptor of the diagnostic that reports the problem.
	/// </summary>
	public DiagnosticDescriptor Descriptor { get; }

	/// <summary>
	/// Gets the id of the diagnostic that reports the problem, e.g. ARG017.
	/// </summary>
	public string RuleId => Descriptor.Id;

	/// <summary>
	/// Gets the type of the class the problem is about, as it is displayed.
	/// </summary>
	public string TypeName { get; }

	/// <summary>
	/// Gets the name of the property the problem is about, or null when it is about the class.
	/// </summary>
	public string? PropertyName { get; }

	/// <summary>
	/// Gets the arguments of the message of the diagnostic.
	/// </summary>
	public EquatableArray<string> Arguments { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="LinkProblem"/> class.
	/// </summary>
	/// <param name="descriptor">The descriptor of the diagnostic that reports the problem.</param>
	/// <param name="typeName">The type of the class the problem is about.</param>
	/// <param name="propertyName">The name of the property the problem is about, if any.</param>
	/// <param name="arguments">The arguments of the message of the diagnostic.</param>
	public LinkProblem(DiagnosticDescriptor descriptor, string typeName, string? propertyName, EquatableArray<string> arguments)
	{
		Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
		TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
		PropertyName = propertyName;
		Arguments = arguments;
	}

	/// <inheritdoc/>
	public bool Equals(LinkProblem? other)
	{
		return other is not null
			&& RuleId == other.RuleId
			&& TypeName == other.TypeName
			&& PropertyName == other.PropertyName
			&& Arguments.Equals(other.Arguments);
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj)
	{
		return Equals(obj as LinkProblem);
	}

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		int hash = 17;
		hash = hash * 23 + RuleId.GetHashCode();
		hash = hash * 23 + TypeName.GetHashCode();
		hash = hash * 23 + (PropertyName?.GetHashCode() ?? 0);
		hash = hash * 23 + Arguments.GetHashCode();
		return hash;
	}
}

/// <summary>
/// The outcome of linking the nodes of a compilation: the trees and the rules they break.
/// </summary>
public sealed class LinkResult : IEquatable<LinkResult>
{
	/// <summary>
	/// Gets the trees, one for every class with the CommandApp attribute.
	/// </summary>
	public EquatableArray<CommandTree> Trees { get; }

	/// <summary>
	/// Gets the rules the nodes break as a whole.
	/// </summary>
	public EquatableArray<LinkProblem> Problems { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="LinkResult"/> class.
	/// </summary>
	/// <param name="trees">The trees.</param>
	/// <param name="problems">The rules the nodes break as a whole.</param>
	public LinkResult(EquatableArray<CommandTree> trees, EquatableArray<LinkProblem> problems)
	{
		Trees = trees;
		Problems = problems;
	}

	/// <inheritdoc/>
	public bool Equals(LinkResult? other)
	{
		return other is not null && Trees.Equals(other.Trees) && Problems.Equals(other.Problems);
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj)
	{
		return Equals(obj as LinkResult);
	}

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		return (Trees.GetHashCode() * 23) + Problems.GetHashCode();
	}
}
