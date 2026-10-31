using Microsoft.CodeAnalysis;

namespace ArgumentParser.Internal.Utilities;

/// <summary>
/// Finds the method that a ParsedWithMethod attribute names. The method is looked for on the class that declares
/// the property first (which is also where the generated code is, so it can be private, and it is the only place
/// that can hold a method for a type that cannot be extended, such as <c>int?</c>), and then on the type of the property (the type of the elements for a list).
/// </summary>
internal static class ParseMethodResolver
{
	/// <summary>
	/// Finds the type that holds the parse method of a property.
	/// </summary>
	/// <param name="property">The property that has the ParsedWithMethod attribute.</param>
	/// <param name="methodName">The method name given to the attribute.</param>
	/// <param name="compilation">The compilation, to decide which methods the declaring class can call.</param>
	/// <returns>The type to call the method on, written so that it cannot be hidden by a namespace of the user, or an empty string when there is no such method.</returns>
	internal static string Resolve(IPropertySymbol? property, string methodName, Compilation compilation)
	{
		if (property is null || string.IsNullOrEmpty(methodName))
		{
			return string.Empty;
		}
		var declaringClass = property.ContainingType;
		if (HasParseMethod(declaringClass, methodName, declaringClass, compilation))
		{
			return Qualified(declaringClass);
		}
		// a list is parsed element by element, so its elements are the type that can hold the method
		if (AttributeReader.UnwrapNullable(AttributeReader.ListElement(property.Type) ?? property.Type) is INamedTypeSymbol propertyType
			&& HasParseMethod(propertyType, methodName, declaringClass, compilation))
		{
			return Qualified(propertyType);
		}
		return string.Empty;
	}

	private static string Qualified(INamedTypeSymbol type)
	{
		return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
	}

	// A static bool Name(string, out T): the shape of TryParse. The value can be a nullable type, so a method can answer null.
	// The base types count as well, as a call on the derived type reaches their static methods.
	private static bool HasParseMethod(INamedTypeSymbol? type, string methodName, INamedTypeSymbol caller, Compilation compilation)
	{
		for (var current = type; current is not null; current = current.BaseType)
		{
			foreach (var member in current.GetMembers(methodName))
			{
				if (member is IMethodSymbol { IsStatic: true, IsGenericMethod: false, ReturnType.SpecialType: SpecialType.System_Boolean, Parameters.Length: 2 } method
					&& method.Parameters[1].RefKind == RefKind.Out
					&& compilation.IsSymbolAccessibleWithin(method, caller))
				{
					return true;
				}
			}
		}
		return false;
	}
}
