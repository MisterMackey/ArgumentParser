using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArgumentParser.Internal.Utilities;

/// <summary>
/// Reads the place of a class in a tree of commands from its symbol and attributes into a <see cref="CommandNodeModel"/>.
/// This is the only place that looks at symbols for the tree; the model it returns is plain data.
/// </summary>
public static class CommandNodeExtractor
{
	private const string CommandAppAttributeName = "ArgumentParser.CommandAppAttribute";
	private const string CommandAttributeName = "ArgumentParser.CommandAttribute";
	private const string ParameterCollectionAttributeName = "ArgumentParser.ParameterCollectionAttribute";
	private const string OptionAttributeName = "ArgumentParser.OptionAttribute";
	private const string FlagAttributeName = "ArgumentParser.FlagAttribute";
	private const string PositionalAttributeName = "ArgumentParser.PositionalAttribute";

	/// <summary>
	/// Determines whether a class has the CommandApp attribute.
	/// </summary>
	/// <param name="symbol">The symbol of the class.</param>
	/// <returns>True if the class has the CommandApp attribute; otherwise, false.</returns>
	public static bool HasCommandAppAttribute(INamedTypeSymbol symbol)
	{
		if (symbol is null)
		{
			throw new ArgumentNullException(nameof(symbol));
		}
		return FindAttribute(symbol.GetAttributes(), CommandAppAttributeName) is not null;
	}

	/// <summary>
	/// Reads the node for a class that has the CommandApp or the Command attribute.
	/// </summary>
	/// <param name="classDeclaration">The declaration of the class.</param>
	/// <param name="symbol">The symbol of the class.</param>
	/// <returns>The node and the diagnostics found while reading it.</returns>
	public static CommandNodeModel Extract(ClassDeclarationSyntax classDeclaration, INamedTypeSymbol symbol)
	{
		if (classDeclaration is null)
		{
			throw new ArgumentNullException(nameof(classDeclaration));
		}
		if (symbol is null)
		{
			throw new ArgumentNullException(nameof(symbol));
		}

		var attributes = symbol.GetAttributes();
		var commandApp = FindAttribute(attributes, CommandAppAttributeName);
		var command = FindAttribute(attributes, CommandAttributeName);
		var hasParameterCollection = FindAttribute(attributes, ParameterCollectionAttributeName) is not null;

		// the root wins when attributes are combined, the conflict is reported on it
		var kind = commandApp is not null ? CommandNodeKind.Root
			: hasParameterCollection ? CommandNodeKind.Leaf
			: CommandNodeKind.Group;
		var nodeAttribute = commandApp ?? command;

		var children = ReadChildCandidates(symbol);
		var containingNamespace = symbol.ContainingNamespace;
		var node = new CommandNodeInfo(
			classDeclaration.Identifier.Text,
			containingNamespace is { IsGlobalNamespace: false } ? containingNamespace.ToDisplayString() : null,
			symbol.ToDisplayString(),
			kind,
			StringArgument(nodeAttribute, 0),
			StringArgument(nodeAttribute, 1),
			AnnotationsArgument(nodeAttribute),
			new EquatableArray<ChildCandidate>(children.Select(c => c.Candidate)));

		var diagnostics = new List<DiagnosticInfo>();
		// a leaf is also a ParameterCollection, which reports on the class itself
		if (kind != CommandNodeKind.Leaf)
		{
			diagnostics.AddRange(Validation.ValidateClassDeclaration(classDeclaration, checkParseMethod: kind == CommandNodeKind.Root));
		}
		diagnostics.AddRange(Validation.ValidateCommandNode(
			node,
			nodeAttribute?.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
			command is not null,
			hasParameterCollection,
			ReadPositionals(symbol)));

		return new CommandNodeModel(
			node,
			LocationInfo.From(classDeclaration.Identifier.GetLocation()),
			new EquatableArray<PropertyLocation>(children.Select(c => new PropertyLocation(c.Candidate.PropertyName, LocationInfo.From(c.Location)))),
			new EquatableArray<DiagnosticInfo>(diagnostics));
	}

	private static AttributeData? FindAttribute(IEnumerable<AttributeData> attributes, string attributeName)
	{
		return attributes.FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == attributeName);
	}

	private static bool HasAttribute(IPropertySymbol property, string attributeName)
	{
		return FindAttribute(property.GetAttributes(), attributeName) is not null;
	}

	private static string StringArgument(AttributeData? data, int index)
	{
		if (data is null || data.ConstructorArguments.Length <= index)
		{
			return string.Empty;
		}
		return data.ConstructorArguments[index].Value?.ToString() ?? string.Empty;
	}

	// Annotations is a named argument, so it is only there when the user wrote it. An enum value arrives as its underlying int.
	private static int AnnotationsArgument(AttributeData? data)
	{
		if (data is null)
		{
			return 0;
		}
		foreach (var argument in data.NamedArguments)
		{
			if (argument.Key == "Annotations" && argument.Value.Value is int value)
			{
				return value;
			}
		}
		return 0;
	}

	private static IEnumerable<IPropertySymbol> InstanceProperties(INamedTypeSymbol symbol)
	{
		return symbol.GetMembers()
			.OfType<IPropertySymbol>()
			.Where(p => !p.IsStatic && !p.IsIndexer && !p.IsImplicitlyDeclared);
	}

	// A property may be a child command when its type is a class declared in this compilation.
	// The types from referenced libraries (string, Uri, ...) can never have the Command attribute from this tree.
	private static List<(ChildCandidate Candidate, Location? Location)> ReadChildCandidates(INamedTypeSymbol symbol)
	{
		var candidates = new List<(ChildCandidate, Location?)>();
		foreach (var property in InstanceProperties(symbol))
		{
			var type = property.Type.WithNullableAnnotation(NullableAnnotation.None);
			if (type.TypeKind != TypeKind.Class || type.DeclaringSyntaxReferences.Length == 0)
			{
				continue;
			}
			var hasArgumentAttribute = HasAttribute(property, OptionAttributeName)
				|| HasAttribute(property, FlagAttributeName)
				|| HasAttribute(property, PositionalAttributeName);
			var candidate = new ChildCandidate(
				property.Name,
				type.ToDisplayString(),
				property.SetMethod is { IsInitOnly: false },
				hasArgumentAttribute);
			candidates.Add((candidate, property.Locations.FirstOrDefault()));
		}
		return candidates;
	}

	private static List<(string PropertyName, Location? Location)> ReadPositionals(INamedTypeSymbol symbol)
	{
		return InstanceProperties(symbol)
			.Where(p => HasAttribute(p, PositionalAttributeName))
			.Select(p => (p.Name, p.Locations.FirstOrDefault()))
			.ToList();
	}
}
