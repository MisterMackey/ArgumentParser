using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ArgumentParser.Internal.Utilities;

/// <summary>
/// Where a property is declared, for diagnostics about it.
/// </summary>
public sealed class PropertyLocation : IEquatable<PropertyLocation>
{
	/// <summary>
	/// Gets the name of the property.
	/// </summary>
	public string PropertyName { get; }

	/// <summary>
	/// Gets where the property is declared, or null when it has no source location.
	/// </summary>
	public LocationInfo? Location { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="PropertyLocation"/> class.
	/// </summary>
	/// <param name="propertyName">The name of the property.</param>
	/// <param name="location">Where the property is declared.</param>
	public PropertyLocation(string propertyName, LocationInfo? location)
	{
		PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
		Location = location;
	}

	/// <inheritdoc/>
	public bool Equals(PropertyLocation? other)
	{
		return other is not null && PropertyName == other.PropertyName && Equals(Location, other.Location);
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj)
	{
		return Equals(obj as PropertyLocation);
	}

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		return (PropertyName.GetHashCode() * 23) + (Location?.GetHashCode() ?? 0);
	}
}

/// <summary>
/// Everything the generator knows about one class in a tree of commands, as plain data.
/// The incremental pipeline compares models with <see cref="Equals(CommandNodeModel)"/> to decide whether to report or link again.
/// Unlike <see cref="ClassModel"/> it says nothing about the arguments of the class; it is about its place in the tree.
/// The locations are kept next to the node and not inside it: they are only needed for diagnostics,
/// and linking the tree from the nodes alone is then unaffected by an edit that moves code around.
/// </summary>
public sealed class CommandNodeModel : IEquatable<CommandNodeModel>
{
	/// <summary>
	/// Gets the node. Present even when the class has errors, so the tree can still be linked and report on the rest.
	/// </summary>
	public CommandNodeInfo Node { get; }

	/// <summary>
	/// Gets where the class is declared, or null when it has no source location.
	/// </summary>
	public LocationInfo? ClassLocation { get; }

	/// <summary>
	/// Gets where the properties that may be child commands are declared, in the order of <see cref="CommandNodeInfo.Children"/>.
	/// </summary>
	public EquatableArray<PropertyLocation> ChildLocations { get; }

	/// <summary>
	/// Gets the diagnostics found while reading the class.
	/// </summary>
	public EquatableArray<DiagnosticInfo> Diagnostics { get; }

	/// <summary>
	/// Gets a value indicating whether any of the diagnostics is an error.
	/// </summary>
	public bool HasErrors => Enumerable.Range(0, Diagnostics.Count).Any(i => Diagnostics[i].Severity == DiagnosticSeverity.Error);

	/// <summary>
	/// Initializes a new instance of the <see cref="CommandNodeModel"/> class.
	/// </summary>
	/// <param name="node">The node.</param>
	/// <param name="classLocation">Where the class is declared.</param>
	/// <param name="childLocations">Where the properties that may be child commands are declared.</param>
	/// <param name="diagnostics">The diagnostics found while reading the class.</param>
	public CommandNodeModel(
		CommandNodeInfo node,
		LocationInfo? classLocation,
		EquatableArray<PropertyLocation> childLocations,
		EquatableArray<DiagnosticInfo> diagnostics)
	{
		Node = node ?? throw new ArgumentNullException(nameof(node));
		ClassLocation = classLocation;
		ChildLocations = childLocations;
		Diagnostics = diagnostics;
	}

	/// <inheritdoc/>
	public bool Equals(CommandNodeModel? other)
	{
		return other is not null
			&& Node.Equals(other.Node)
			&& Equals(ClassLocation, other.ClassLocation)
			&& ChildLocations.Equals(other.ChildLocations)
			&& Diagnostics.Equals(other.Diagnostics);
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj)
	{
		return Equals(obj as CommandNodeModel);
	}

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		int hash = 17;
		hash = hash * 23 + Node.GetHashCode();
		hash = hash * 23 + (ClassLocation?.GetHashCode() ?? 0);
		hash = hash * 23 + ChildLocations.GetHashCode();
		hash = hash * 23 + Diagnostics.GetHashCode();
		return hash;
	}
}
