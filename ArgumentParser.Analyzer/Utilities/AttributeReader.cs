using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ArgumentParser.Internal.Utilities;

/// <summary>
/// Finds the attributes of the runtime library on a symbol and reads their arguments.
/// </summary>
internal static class AttributeReader
{
	internal const string ParameterCollection = "ArgumentParser.ParameterCollectionAttribute";
	internal const string CommandApp = "ArgumentParser.CommandAppAttribute";
	internal const string Command = "ArgumentParser.CommandAttribute";
	internal const string Option = "ArgumentParser.OptionAttribute";
	internal const string Flag = "ArgumentParser.FlagAttribute";
	internal const string Positional = "ArgumentParser.PositionalAttribute";
	internal const string ParsedWithMethod = "ArgumentParser.ParsedWithMethodAttribute";
	internal const string ExcludeFromManifest = "ArgumentParser.ExcludeFromManifestAttribute";

	internal static AttributeData? Find(ISymbol? symbol, string attributeName)
	{
		return symbol is null ? null : Find(symbol.GetAttributes(), attributeName);
	}

	internal static AttributeData? Find(IEnumerable<AttributeData> attributes, string attributeName)
	{
		return attributes.FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == attributeName);
	}

	internal static bool Has(ISymbol symbol, string attributeName)
	{
		return Find(symbol, attributeName) is not null;
	}

	// A class with the Command or the CommandApp attribute is a node of a tree of commands.
	internal static bool IsCommandClass(ITypeSymbol? type)
	{
		return type is { TypeKind: TypeKind.Class } && (Has(type, Command) || Has(type, CommandApp));
	}

	// int? is Nullable<int>: the type that is read and parsed is int
	internal static ITypeSymbol UnwrapNullable(ITypeSymbol type)
	{
		return type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
			? nullable.TypeArguments[0]
			: type;
	}

	// The names of the members of an enum (or of the enum a nullable enum is of), in the order they are declared; empty for any other type.
	internal static IEnumerable<string> EnumMemberNames(ITypeSymbol? type)
	{
		return type is not null && UnwrapNullable(type) is { TypeKind: TypeKind.Enum } enumType
			? enumType.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).Select(f => f.Name)
			: [];
	}

	// a property set by name (UseCurrentCulture = true), which is only there when the user wrote it
	internal static bool NamedBoolArgument(AttributeData? data, string name)
	{
		return data is not null && data.NamedArguments.Any(a => a.Key == name && a.Value.Value is true);
	}

	// List<T> (and List<T>?, which is the same type): the argument can be given several times and the type that is read and parsed is T.
	// Only List<T> counts; other collections are not lists.
	internal static ITypeSymbol? ListElement(ITypeSymbol? type)
	{
		return type is INamedTypeSymbol { TypeArguments.Length: 1 } named
			&& named.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>"
			? named.TypeArguments[0]
			: null;
	}

	// The element type as it is written in the model: a nullable value type keeps its question mark (int?),
	// the annotation of a reference type (string?) says nothing about the parsing and is dropped.
	internal static string DisplayElement(ITypeSymbol element)
	{
		return (element.IsReferenceType ? element.WithNullableAnnotation(NullableAnnotation.NotAnnotated) : element).ToDisplayString();
	}

	// The attribute properties are get-only, so every value reaches the generator as a constructor argument,
	// whether the user wrote it positionally or by name. Omitted optional arguments are filled with their defaults by Roslyn.
	internal static string StringArgument(AttributeData? data, int index)
	{
		if (data is null || data.ConstructorArguments.Length <= index)
		{
			return string.Empty;
		}
		return data.ConstructorArguments[index].Value?.ToString() ?? string.Empty;
	}

	// an enum value arrives as its underlying int
	internal static int IntArgument(AttributeData? data, int index, int fallback)
	{
		if (data is null || data.ConstructorArguments.Length <= index)
		{
			return fallback;
		}
		return data.ConstructorArguments[index].Value is int value ? value : fallback;
	}

	internal static bool BoolArgument(AttributeData? data, int index)
	{
		if (data is null || data.ConstructorArguments.Length <= index)
		{
			return false;
		}
		return data.ConstructorArguments[index].Value is true;
	}
}
