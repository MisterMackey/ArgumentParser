using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArgumentParser.Internal.Utilities;

/// <summary>
/// Finds the parts of a partial class, which may be declared in several files.
/// </summary>
public static class DeclarationParts
{
	/// <summary>
	/// Reads every part of a class with the semantic model of the file it is in,
	/// in an order that does not depend on how the compilation lists its files (by file path, then by position).
	/// </summary>
	/// <param name="symbol">The symbol of the class.</param>
	/// <param name="classDeclaration">The part that was found, used when the symbol has no declarations.</param>
	/// <param name="semanticModel">The semantic model of the file that part is in.</param>
	/// <returns>The parts of the class, with their semantic models.</returns>
	public static IReadOnlyList<(ClassDeclarationSyntax Declaration, SemanticModel Model)> Read(
		INamedTypeSymbol symbol,
		ClassDeclarationSyntax classDeclaration,
		SemanticModel semanticModel)
	{
		if (symbol is null)
		{
			throw new ArgumentNullException(nameof(symbol));
		}
		if (classDeclaration is null)
		{
			throw new ArgumentNullException(nameof(classDeclaration));
		}
		if (semanticModel is null)
		{
			throw new ArgumentNullException(nameof(semanticModel));
		}
		var parts = new List<(ClassDeclarationSyntax, SemanticModel)>();
		foreach (var reference in symbol.DeclaringSyntaxReferences.OrderBy(r => r.SyntaxTree.FilePath, StringComparer.Ordinal).ThenBy(r => r.Span.Start))
		{
			if (reference.GetSyntax() is ClassDeclarationSyntax declaration)
			{
				parts.Add((declaration, reference.SyntaxTree == classDeclaration.SyntaxTree ? semanticModel : semanticModel.Compilation.GetSemanticModel(reference.SyntaxTree)));
			}
		}
		if (parts.Count == 0)
		{
			parts.Add((classDeclaration, semanticModel));
		}
		return parts;
	}

	/// <summary>
	/// Reads the properties of every part, each with the semantic model of its file.
	/// </summary>
	/// <param name="parts">The parts of the class.</param>
	/// <returns>A factory for the attributes of the properties of each part.</returns>
	public static IEnumerable<AttributeFactory> AttributeFactories(IReadOnlyList<(ClassDeclarationSyntax Declaration, SemanticModel Model)> parts)
	{
		if (parts is null)
		{
			throw new ArgumentNullException(nameof(parts));
		}
		return parts.Select(p => new AttributeFactory(p.Model, p.Declaration.Members.OfType<PropertyDeclarationSyntax>().ToList().AsReadOnly()));
	}
}
