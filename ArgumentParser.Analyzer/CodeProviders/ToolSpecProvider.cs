using System;
using System.Collections.Generic;
using System.Linq;
using ArgumentParser.Internal.Utilities;

namespace ArgumentParser.Internal;

/// <summary>
/// Describes classes and the commands of a tree as tools (<see cref="ToolSpec"/>): the JSON view of their arguments,
/// which the JSON Schema and the JSON binder are written from. It also holds the rules both of them follow:
/// which arguments are fields (<see cref="FieldArguments"/>), the key of a field (<see cref="Key"/>) and the name of a tool (<see cref="ToolName"/>).
/// </summary>
public static class ToolSpecProvider
{
	/// <summary>
	/// The text between the command names of a path in the name of a tool. A single underscore is common in command names, two are not.
	/// </summary>
	public const string ToolNameSeparator = "__";

	/// <summary>
	/// Gets the key of a property in a JSON object: its name in camelCase, the way System.Text.Json writes it
	/// (<c>FilePath</c> becomes <c>filePath</c>, <c>URL</c> becomes <c>url</c>, <c>IPAddress</c> becomes <c>ipAddress</c>).
	/// The <c>@</c> of a keyword used as a name is left out.
	/// </summary>
	/// <param name="propertyName">The name of the property.</param>
	/// <returns>The key.</returns>
	public static string Key(string propertyName)
	{
		if (propertyName is null)
		{
			throw new ArgumentNullException(nameof(propertyName));
		}
		var name = propertyName.StartsWith("@", StringComparison.Ordinal) ? propertyName.Substring(1) : propertyName;
		if (name.Length == 0 || !char.IsUpper(name[0]))
		{
			return name;
		}
		// the leading capitals are lowered, but the last one of a run stays when a lower case letter follows it (URLValue becomes urlValue)
		var chars = name.ToCharArray();
		for (int i = 0; i < chars.Length; i++)
		{
			if (i == 1 && !char.IsUpper(chars[i]))
			{
				break;
			}
			if (i > 0 && i + 1 < chars.Length && !char.IsUpper(chars[i + 1]))
			{
				break;
			}
			chars[i] = char.ToLowerInvariant(chars[i]);
		}
		return new string(chars);
	}

	/// <summary>
	/// Gets the name of the tool of a command: its path joined with <see cref="ToolNameSeparator"/>, such as <c>ingredient__add</c>.
	/// </summary>
	/// <param name="path">The names of the commands that lead to the command.</param>
	/// <returns>The name of the tool.</returns>
	public static string ToolName(IEnumerable<string> path)
	{
		if (path is null)
		{
			throw new ArgumentNullException(nameof(path));
		}
		return string.Join(ToolNameSeparator, path);
	}

	/// <summary>
	/// Gets the arguments of a class that are fields of its tool, in the order of the fields: the positional arguments by position,
	/// then the options, then the flags. An argument with the ExcludeFromManifest attribute is not a field.
	/// </summary>
	/// <param name="options">The properties with the Option attribute.</param>
	/// <param name="flags">The properties with the Flag attribute.</param>
	/// <param name="positionals">The properties with the Positional attribute.</param>
	/// <returns>The arguments with their kind.</returns>
	public static IEnumerable<(PropertyAndAttributeInfo Argument, ArgumentKind Kind)> FieldArguments(
		IEnumerable<PropertyAndAttributeInfo> options,
		IEnumerable<PropertyAndAttributeInfo> flags,
		IEnumerable<PropertyAndAttributeInfo> positionals)
	{
		if (options is null)
		{
			throw new ArgumentNullException(nameof(options));
		}
		if (flags is null)
		{
			throw new ArgumentNullException(nameof(flags));
		}
		if (positionals is null)
		{
			throw new ArgumentNullException(nameof(positionals));
		}
		return positionals.OrderBy(p => p.Attribute.Position).Select(p => (p, ArgumentKind.Positional))
			.Concat(options.Select(o => (o, ArgumentKind.Option)))
			.Concat(flags.Select(f => (f, ArgumentKind.Flag)))
			.Where(item => !item.Item1.ExcludeFromManifest);
	}

	/// <summary>
	/// Describes a class with the ParameterCollection attribute on its own as a tool, named after the class.
	/// </summary>
	/// <param name="model">The model of the class.</param>
	/// <returns>The tool.</returns>
	public static ToolSpec ForClass(ClassModel model)
	{
		if (model is null)
		{
			throw new ArgumentNullException(nameof(model));
		}
		var fields = FieldArguments(model.Options.AsReadOnly(), model.Flags.AsReadOnly(), model.Positionals.AsReadOnly())
			.Select(item => Field(item.Argument, item.Kind, []));
		return new ToolSpec(model.ClassName, "", CommandNodeInfo.DefaultAnnotations, fields);
	}

	/// <summary>
	/// Describes every leaf of a tree as a tool, in the order of the Commands list of the root.
	/// </summary>
	/// <param name="tree">The tree.</param>
	/// <returns>The tools.</returns>
	public static IReadOnlyList<ToolSpec> ForTree(CommandTree tree)
	{
		if (tree is null)
		{
			throw new ArgumentNullException(nameof(tree));
		}
		return CommandTreeSourceGenerator.Leaves(tree).Select(leaf => ForCommand(tree, leaf)).ToList();
	}

	/// <summary>
	/// Describes a leaf of a tree as a tool. Its fields are the options and flags of the root and of every group on the way to the leaf
	/// (they are given on the command line too), then the arguments of the leaf.
	/// </summary>
	/// <param name="tree">The tree.</param>
	/// <param name="leaf">The leaf.</param>
	/// <returns>The tool.</returns>
	public static ToolSpec ForCommand(CommandTree tree, TreeNode leaf)
	{
		if (tree is null)
		{
			throw new ArgumentNullException(nameof(tree));
		}
		if (leaf is null)
		{
			throw new ArgumentNullException(nameof(leaf));
		}
		var fields = new List<ToolField>();
		foreach (var level in CommandHelpTextProvider.Ancestors(tree, leaf).Concat(new[] { leaf }))
		{
			var access = CommandTreeSourceGenerator.PropertyAccess(tree, level);
			var arguments = level.Source.Arguments;
			fields.AddRange(FieldArguments(arguments.Options.AsReadOnly(), arguments.Flags.AsReadOnly(), arguments.Positionals.AsReadOnly())
				.Select(item => Field(item.Argument, item.Kind, access)));
		}
		var info = leaf.Source.Node;
		return new ToolSpec(ToolName(leaf.Path.AsReadOnly()), info.Description, info.Annotations, fields);
	}

	private static ToolField Field(PropertyAndAttributeInfo argument, ArgumentKind kind, IEnumerable<string> access)
	{
		// a flag reads no text, so a parse method on it does nothing (as on the command line)
		var parseMethod = kind != ArgumentKind.Flag && argument.HasParseMethod;
		return new ToolField(
			Key(argument.PropertyName),
			access.Concat(new[] { argument.PropertyName }),
			kind,
			parseMethod ? "string" : JsonType(argument),
			argument.UnderlyingType,
			argument.EnumMembers.AsReadOnly(),
			argument.IsList,
			argument.Attribute.Required,
			argument.IsNullable,
			argument.Attribute.Description,
			parseMethod ? argument.ParseMethodOwner : "",
			parseMethod ? argument.ParseMethodName : "");
	}

	// an enum is given by the name of a member; the other text based types (char, Guid, Uri, TimeSpan, DateTime) are strings too
	private static string JsonType(PropertyAndAttributeInfo argument)
	{
		if (argument.IsEnum)
		{
			return "string";
		}
		return argument.UnderlyingType switch
		{
			"bool" => "boolean",
			"int" or "long" or "short" or "byte" or "sbyte" or "uint" or "ulong" or "ushort" => "integer",
			"double" or "float" or "decimal" => "number",
			_ => "string",
		};
	}
}
