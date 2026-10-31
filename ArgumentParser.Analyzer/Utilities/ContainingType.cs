using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ArgumentParser.Internal.Utilities;

/// <summary>
/// A type that a generated class is nested in. The generated code has to repeat its declaration (as a partial type),
/// or the generated class would be a different class than the one the user wrote.
/// </summary>
public sealed class ContainingType : IEquatable<ContainingType>
{
	/// <summary>
	/// Gets the keywords that declare the type, such as <c>class</c> or <c>record struct</c>.
	/// </summary>
	public string Keyword { get; }

	/// <summary>
	/// Gets the name of the type as it is written in code: a keyword used as a name has its <c>@</c>.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="ContainingType"/> class.
	/// </summary>
	/// <param name="keyword">The keywords that declare the type.</param>
	/// <param name="name">The name of the type.</param>
	public ContainingType(string keyword, string name)
	{
		Keyword = keyword ?? throw new ArgumentNullException(nameof(keyword));
		Name = name ?? throw new ArgumentNullException(nameof(name));
	}

	/// <summary>
	/// Reads the types a type is nested in, the outermost first.
	/// </summary>
	/// <param name="symbol">The nested type.</param>
	/// <returns>The containing types, or an empty array for a type that is not nested.</returns>
	public static EquatableArray<ContainingType> ReadAll(INamedTypeSymbol symbol)
	{
		if (symbol is null)
		{
			throw new ArgumentNullException(nameof(symbol));
		}
		var containing = new List<ContainingType>();
		for (var type = symbol.ContainingType; type is not null; type = type.ContainingType)
		{
			containing.Insert(0, new ContainingType(KeywordOf(type), EscapeIdentifier(type.Name)));
		}
		return new EquatableArray<ContainingType>(containing);
	}

	/// <summary>
	/// Determines whether a type or one of the types it is nested in has type parameters.
	/// </summary>
	/// <param name="symbol">The type.</param>
	/// <returns>True if the type is generic or nested in a generic type; otherwise, false.</returns>
	public static bool IsGenericOrInGeneric(INamedTypeSymbol symbol)
	{
		if (symbol is null)
		{
			throw new ArgumentNullException(nameof(symbol));
		}
		for (var type = symbol; type is not null; type = type.ContainingType)
		{
			if (type.TypeParameters.Length > 0)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Builds the name of a type from its namespace, the types it is nested in and its own name, without type parameters.
	/// It is unique in a compilation, so it can be used in the name of a generated file, which is why it has no <c>@</c> of an escaped keyword.
	/// </summary>
	/// <param name="namespaceName">The namespace, if any.</param>
	/// <param name="containingTypes">The types the type is nested in, the outermost first.</param>
	/// <param name="className">The name of the type.</param>
	/// <returns>The qualified name.</returns>
	public static string QualifiedName(string? namespaceName, EquatableArray<ContainingType> containingTypes, string className)
	{
		var parts = new List<string>();
		if (namespaceName is not null)
		{
			parts.Add(namespaceName);
		}
		parts.AddRange(containingTypes.AsReadOnly().Select(item => item.Name));
		parts.Add(className);
		return string.Join(".", parts).Replace("@", "");
	}

	/// <summary>
	/// Writes a name the way it has to be written in code: a keyword, such as <c>event</c>, gets an <c>@</c>.
	/// </summary>
	/// <param name="name">The name of a symbol.</param>
	/// <returns>The name, escaped when it is a keyword.</returns>
	public static string EscapeIdentifier(string name)
	{
		return SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;
	}

	private static string KeywordOf(INamedTypeSymbol type)
	{
		return type.TypeKind switch
		{
			TypeKind.Interface => "interface",
			TypeKind.Struct => type.IsRecord ? "record struct" : "struct",
			_ => type.IsRecord ? "record" : "class"
		};
	}

	/// <inheritdoc/>
	public bool Equals(ContainingType? other)
	{
		return other is not null && Keyword == other.Keyword && Name == other.Name;
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj)
	{
		return Equals(obj as ContainingType);
	}

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		return (Keyword.GetHashCode() * 23) + Name.GetHashCode();
	}
}
