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
