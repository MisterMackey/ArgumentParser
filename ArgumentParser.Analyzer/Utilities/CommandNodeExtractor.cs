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
		return AttributeReader.Find(symbol.GetAttributes(), AttributeReader.CommandApp) is not null;
	}

	/// <summary>
	/// Reads the node for a class that has the CommandApp or the Command attribute.
	/// </summary>
	/// <param name="classDeclaration">The declaration of the class.</param>
	/// <param name="symbol">The symbol of the class.</param>
	/// <param name="semanticModel">The semantic model of the tree the class is in.</param>
	/// <returns>The node and the diagnostics found while reading it.</returns>
	public static CommandNodeModel Extract(ClassDeclarationSyntax classDeclaration, INamedTypeSymbol symbol, SemanticModel semanticModel)
	{
		if (classDeclaration is null)
		{
			throw new ArgumentNullException(nameof(classDeclaration));
		}
		if (symbol is null)
		{
			throw new ArgumentNullException(nameof(symbol));
		}
		if (semanticModel is null)
		{
			throw new ArgumentNullException(nameof(semanticModel));
		}

		var attributes = symbol.GetAttributes();
		var commandApp = AttributeReader.Find(attributes, AttributeReader.CommandApp);
		var command = AttributeReader.Find(attributes, AttributeReader.Command);
		var parameterCollection = AttributeReader.Find(attributes, AttributeReader.ParameterCollection);
		var hasParameterCollection = parameterCollection is not null;

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
			ContainingType.ReadAll(symbol),
			symbol.ToDisplayString(),
			kind,
			AttributeReader.StringArgument(nodeAttribute, 0),
			AttributeReader.StringArgument(nodeAttribute, 1),
			AnnotationsArgument(nodeAttribute),
			new EquatableArray<ChildCandidate>(children.Select(c => c.Candidate)),
			kind switch
			{
				// the arguments that follow the name and the description of the application
				CommandNodeKind.Root => SettingsArguments(commandApp, 2),
				CommandNodeKind.Leaf => SettingsArguments(parameterCollection, 0),
				_ => NodeSettings.Default,
			});

		var diagnostics = new List<DiagnosticInfo>();
		// the class may be declared in several parts, in several files: the arguments of every part count
		var parts = DeclarationParts.Read(symbol, classDeclaration, semanticModel);
		var declarations = parts.Select(p => p.Declaration).ToList();
		var factories = DeclarationParts.AttributeFactories(parts).ToList();
		var options = factories.SelectMany(f => f.GetOptionAttributes()).ToList();
		var flags = factories.SelectMany(f => f.GetFlagAttributes()).ToList();
		// A class with the ParameterCollection attribute (a leaf, or a root that wrongly has it too, ARG016) is also read by that pipeline,
		// which reports on the class itself and on its arguments: checking them here too would report everything twice.
		// Its options and flags are still read, so their names can be compared with those of the commands above it.
		if (!hasParameterCollection)
		{
			diagnostics.AddRange(Validation.ValidateClassDeclaration(classDeclaration, checkParseMethod: kind == CommandNodeKind.Root, declarations));
			// positionals cannot be used here (ValidateCommandNode reports them), only a command class as their type is reported like on the others
			var positionals = factories.SelectMany(f => f.GetPositionalAttributes()).ToList();
			diagnostics.AddRange(Validation.ValidateAttributes(options, flags, Array.Empty<PropertyAndAttributeInfo>(), declarations));
			diagnostics.AddRange(Validation.ValidateCommandClassArguments(
				Array.Empty<PropertyAndAttributeInfo>(),
				Array.Empty<PropertyAndAttributeInfo>(),
				positionals,
				declarations));
		}
		// a leaf keeps its positionals for its help text in the tree; on a group or the root they cannot be used
		var leafPositionals = kind == CommandNodeKind.Leaf ? factories.SelectMany(f => f.GetPositionalAttributes()) : [];
		var arguments = new NodeArguments(
			new EquatableArray<PropertyAndAttributeInfo>(options),
			new EquatableArray<PropertyAndAttributeInfo>(flags),
			new EquatableArray<PropertyAndAttributeInfo>(leafPositionals));
		var argumentLocations = options.Concat(flags)
			.Select(a => new PropertyLocation(a.PropertyName, LocationInfo.From(ArgumentLocation(declarations, a.PropertyName))));
		diagnostics.AddRange(Validation.ValidateNodeDeclaration(
			node,
			classDeclaration.Identifier.GetLocation(),
			ContainingType.IsGenericOrInGeneric(symbol),
			symbol.IsAbstract,
			kind == CommandNodeKind.Leaf ? [] : ReadRequiredMembers(symbol)));
		diagnostics.AddRange(Validation.ValidateCommandNode(
			node,
			nodeAttribute?.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
			command is not null,
			hasParameterCollection,
			ReadPositionals(symbol)));

		return new CommandNodeModel(
			node,
			arguments,
			LocationInfo.From(classDeclaration.Identifier.GetLocation()),
			new EquatableArray<PropertyLocation>(children.Select(c => new PropertyLocation(c.Candidate.PropertyName, LocationInfo.From(c.Location)))),
			new EquatableArray<PropertyLocation>(argumentLocations),
			new EquatableArray<DiagnosticInfo>(diagnostics));
	}

	// the whole declaration, like the rules about arguments in Validation
	private static Location? ArgumentLocation(IEnumerable<ClassDeclarationSyntax> declarations, string propertyName)
	{
		return declarations
			.SelectMany(c => c.Members)
			.OfType<PropertyDeclarationSyntax>()
			.FirstOrDefault(p => p.Identifier.Text == propertyName)
			?.GetLocation();
	}

	// HelpTextGeneration, the short and long help names and BehaviourOnError, in this order from the given index;
	// Roslyn fills in the defaults of the arguments the user left out
	private static NodeSettings SettingsArguments(AttributeData? data, int first)
	{
		var defaults = NodeSettings.Default;
		if (data is null || data.ConstructorArguments.Length < first + 4)
		{
			return defaults;
		}
		return new NodeSettings(
			AttributeReader.IntArgument(data, first, defaults.HelpTextGeneration),
			AttributeReader.StringArgument(data, first + 1),
			AttributeReader.StringArgument(data, first + 2),
			AttributeReader.IntArgument(data, first + 3, defaults.BehaviourOnError));
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

	// Only what the class declares itself: a property inherited from a base class is not seen.
	// An explicit interface implementation is left out, it cannot be reached by its name from the generated code.
	private static IEnumerable<IPropertySymbol> InstanceProperties(INamedTypeSymbol symbol)
	{
		return symbol.GetMembers()
			.OfType<IPropertySymbol>()
			.Where(p => !p.IsStatic && !p.IsIndexer && !p.IsImplicitlyDeclared && p.ExplicitInterfaceImplementations.Length == 0);
	}

	private static List<(string PropertyName, Location? Location)> ReadRequiredMembers(INamedTypeSymbol symbol)
	{
		return symbol.GetMembers()
			.Where(m => m is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true })
			.Select(m => (m.Name, m.Locations.FirstOrDefault()))
			.ToList();
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
			var hasArgumentAttribute = AttributeReader.Has(property, AttributeReader.Option)
				|| AttributeReader.Has(property, AttributeReader.Flag)
				|| AttributeReader.Has(property, AttributeReader.Positional);
			var candidate = new ChildCandidate(
				ContainingType.EscapeIdentifier(property.Name),
				type.ToDisplayString(),
				property.SetMethod is { IsInitOnly: false },
				hasArgumentAttribute);
			candidates.Add((candidate, property.Locations.FirstOrDefault()));
		}
		return candidates;
	}

	// a positional of a command class type is reported by ARG026 instead
	private static List<(string PropertyName, Location? Location)> ReadPositionals(INamedTypeSymbol symbol)
	{
		return InstanceProperties(symbol)
			.Where(p => AttributeReader.Has(p, AttributeReader.Positional) && !AttributeReader.IsCommandClass(p.Type))
			.Select(p => (p.Name, p.Locations.FirstOrDefault()))
			.ToList();
	}
}
