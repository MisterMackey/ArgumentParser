using System;

namespace ArgumentParser.Internal.Utilities;

/// <summary>
/// The role a class plays in a tree of commands.
/// </summary>
public enum CommandNodeKind
{
	/// <summary>
	/// The class has the CommandApp attribute: the root of the tree, which names the application.
	/// </summary>
	Root,

	/// <summary>
	/// The class has the Command attribute but not the ParameterCollection attribute: a group of commands.
	/// </summary>
	Group,

	/// <summary>
	/// The class has both the Command and the ParameterCollection attribute: a command that does the work.
	/// </summary>
	Leaf
}

/// <summary>
/// A property of a node that may be a child command, because its type is a class declared in source.
/// Whether it is one depends on the type being a node itself, which only the whole tree can tell.
/// It holds no source location: a location moves when anything above it is edited, and the tree should not be linked again for that.
/// </summary>
public sealed class ChildCandidate : IEquatable<ChildCandidate>
{
	/// <summary>
	/// Gets the name of the property.
	/// </summary>
	public string PropertyName { get; }

	/// <summary>
	/// Gets the type of the property as it is displayed, without a nullable annotation. Nodes are matched on this.
	/// </summary>
	public string TypeName { get; }

	/// <summary>
	/// Gets a value indicating whether generated code in the declaring class can assign the property.
	/// An init-only setter does not count, since it can only be called while an object is being created.
	/// </summary>
	public bool HasSetter { get; }

	/// <summary>
	/// Gets a value indicating whether the property also has an Option, Flag or Positional attribute.
	/// </summary>
	public bool HasArgumentAttribute { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="ChildCandidate"/> class.
	/// </summary>
	/// <param name="propertyName">The name of the property.</param>
	/// <param name="typeName">The type of the property as it is displayed, without a nullable annotation.</param>
	/// <param name="hasSetter">Whether generated code in the declaring class can assign the property.</param>
	/// <param name="hasArgumentAttribute">Whether the property also has an Option, Flag or Positional attribute.</param>
	public ChildCandidate(string propertyName, string typeName, bool hasSetter, bool hasArgumentAttribute)
	{
		PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
		TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
		HasSetter = hasSetter;
		HasArgumentAttribute = hasArgumentAttribute;
	}

	/// <inheritdoc/>
	public bool Equals(ChildCandidate? other)
	{
		return other is not null
			&& PropertyName == other.PropertyName
			&& TypeName == other.TypeName
			&& HasSetter == other.HasSetter
			&& HasArgumentAttribute == other.HasArgumentAttribute;
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj)
	{
		return Equals(obj as ChildCandidate);
	}

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		int hash = 17;
		hash = hash * 23 + PropertyName.GetHashCode();
		hash = hash * 23 + TypeName.GetHashCode();
		hash = hash * 23 + HasSetter.GetHashCode();
		hash = hash * 23 + HasArgumentAttribute.GetHashCode();
		return hash;
	}
}

/// <summary>
/// What the tree of commands needs to know about one node, as plain data without symbols or syntax nodes.
/// It leaves out the options and arguments of the node and every source location, so editing those does not change it.
/// </summary>
public sealed class CommandNodeInfo : IEquatable<CommandNodeInfo>
{
	/// <summary>
	/// Gets the name of the class.
	/// </summary>
	public string ClassName { get; }

	/// <summary>
	/// Gets the namespace of the class as it is displayed, or null when there is none.
	/// </summary>
	public string? NamespaceName { get; }

	/// <summary>
	/// Gets the type of the class as it is displayed. A property whose type is this one is a child command of the class that declares it.
	/// </summary>
	public string TypeName { get; }

	/// <summary>
	/// Gets the role of the class in the tree.
	/// </summary>
	public CommandNodeKind Kind { get; }

	/// <summary>
	/// Gets the name of the command, or of the application for the root.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Gets the description used in help text.
	/// </summary>
	public string Description { get; }

	/// <summary>
	/// Gets the value of the Annotations argument of the Command attribute, as the numeric value of CommandAnnotations.
	/// </summary>
	public int Annotations { get; }

	/// <summary>
	/// Gets the properties that may be child commands.
	/// </summary>
	public EquatableArray<ChildCandidate> Children { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="CommandNodeInfo"/> class.
	/// </summary>
	/// <param name="className">The name of the class.</param>
	/// <param name="namespaceName">The namespace of the class, if any.</param>
	/// <param name="typeName">The type of the class as it is displayed.</param>
	/// <param name="kind">The role of the class in the tree.</param>
	/// <param name="name">The name of the command, or of the application for the root.</param>
	/// <param name="description">The description used in help text.</param>
	/// <param name="annotations">The numeric value of the CommandAnnotations of the command.</param>
	/// <param name="children">The properties that may be child commands.</param>
	public CommandNodeInfo(
		string className,
		string? namespaceName,
		string typeName,
		CommandNodeKind kind,
		string name,
		string description,
		int annotations,
		EquatableArray<ChildCandidate> children)
	{
		ClassName = className ?? throw new ArgumentNullException(nameof(className));
		NamespaceName = namespaceName;
		TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
		Kind = kind;
		Name = name ?? throw new ArgumentNullException(nameof(name));
		Description = description ?? throw new ArgumentNullException(nameof(description));
		Annotations = annotations;
		Children = children;
	}

	/// <inheritdoc/>
	public bool Equals(CommandNodeInfo? other)
	{
		return other is not null
			&& ClassName == other.ClassName
			&& NamespaceName == other.NamespaceName
			&& TypeName == other.TypeName
			&& Kind == other.Kind
			&& Name == other.Name
			&& Description == other.Description
			&& Annotations == other.Annotations
			&& Children.Equals(other.Children);
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj)
	{
		return Equals(obj as CommandNodeInfo);
	}

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		int hash = 17;
		hash = hash * 23 + ClassName.GetHashCode();
		hash = hash * 23 + (NamespaceName?.GetHashCode() ?? 0);
		hash = hash * 23 + TypeName.GetHashCode();
		hash = hash * 23 + Kind.GetHashCode();
		hash = hash * 23 + Name.GetHashCode();
		hash = hash * 23 + Description.GetHashCode();
		hash = hash * 23 + Annotations;
		hash = hash * 23 + Children.GetHashCode();
		return hash;
	}
}
