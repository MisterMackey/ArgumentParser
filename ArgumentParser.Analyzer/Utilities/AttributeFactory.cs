using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArgumentParser.Internal;

/// <summary>
/// Factory for creating attribute instances from attribute data in source code.
/// </summary>
public class AttributeFactory
{
	/// <summary>
	/// The semantic model used for analysis.
	/// </summary>
	private readonly SemanticModel _semanticModel;

	/// <summary>
	/// The list of property declarations to analyze.
	/// </summary>
	private readonly ReadOnlyCollection<PropertyDeclarationSyntax> _properties;

	/// <summary>
	/// Initializes a new instance of the <see cref="AttributeFactory"/> class.
	/// </summary>
	/// <param name="semanticModel">The semantic model used for analysis.</param>
	/// <param name="properties">The list of property declarations to analyze.</param>
	public AttributeFactory(SemanticModel semanticModel, ReadOnlyCollection<PropertyDeclarationSyntax> properties)
	{
		_semanticModel = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
		_properties = properties;
	}

	private const string OptionAttributeName = "ArgumentParser.OptionAttribute";
	private const string PositionalAttributeName = "ArgumentParser.PositionalAttribute";
	private const string FlagAttributeName = "ArgumentParser.FlagAttribute";
	private const string ParsedWithMethodAttributeName = "ArgumentParser.ParsedWithMethodAttribute";

	/// <summary>
	/// Gets all properties with OptionAttributes and creates PropertyAndAttributeInfo instances for them.
	/// </summary>
	/// <returns>A list of PropertyAndAttributeInfo instances for OptionAttributes.</returns>
	public IList<PropertyAndAttributeInfo> GetOptionAttributes()
	{
		return _properties
			.Where(p => HasAttribute(p, OptionAttributeName))
			.Select(CreateOptionAttribute)
			.ToList();
	}

	/// <summary>
	/// Gets all properties with PositionalAttributes and creates PropertyAndAttributeInfo instances for them.
	/// </summary>
	/// <returns>A list of PropertyAndAttributeInfo instances for PositionalAttributes.</returns>
	public IList<PropertyAndAttributeInfo> GetPositionalAttributes()
	{
		return _properties
			.Where(p => HasAttribute(p, PositionalAttributeName))
			.Select(CreatePositionalInfo)
			.ToList();
	}

	/// <summary>
	/// Gets all properties with FlagAttributes and creates PropertyAndAttributeInfo instances for them.
	/// </summary>
	/// <returns>A list of PropertyAndAttributeInfo instances for FlagAttributes.</returns>
	public IList<PropertyAndAttributeInfo> GetFlagAttributes()
	{
		return _properties
			.Where(p => HasAttribute(p, FlagAttributeName))
			.Select(CreateFlagAttribute)
			.ToList();
	}

	private bool HasAttribute(PropertyDeclarationSyntax property, string attributeName)
	{
		return property.AttributeLists
			.SelectMany(al => al.Attributes)
			.Any(attr => _semanticModel.GetSymbolInfo(attr).Symbol is IMethodSymbol symbol
				&& symbol.ContainingType.ToDisplayString() == attributeName);
	}

	private static AttributeData? FindAttribute(IPropertySymbol? property, string attributeName)
	{
		return property?.GetAttributes()
			.FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == attributeName);
	}

	// The attribute properties are get-only, so every value reaches the generator as a constructor argument,
	// whether the user wrote it positionally or by name. Omitted optional arguments are filled with their defaults by Roslyn.
	private static string StringArgument(AttributeData? data, int index)
	{
		if (data == null || data.ConstructorArguments.Length <= index)
		{
			return string.Empty;
		}
		return data.ConstructorArguments[index].Value?.ToString() ?? string.Empty;
	}

	private static bool BoolArgument(AttributeData? data, int index)
	{
		if (data == null || data.ConstructorArguments.Length <= index)
		{
			return false;
		}
		return data.ConstructorArguments[index].Value is true;
	}

	private static PropertyAndAttributeInfo CreateInfo(PropertyDeclarationSyntax property, IPropertySymbol? propertySymbol, AttributeInfo attribute)
	{
		var parsedWithMethod = StringArgument(FindAttribute(propertySymbol, ParsedWithMethodAttributeName), 0);
		return new PropertyAndAttributeInfo
		{
			Attribute = attribute,
			PropertyName = property.Identifier.Text,
			PropertyType = propertySymbol?.Type.ToDisplayString() ?? "string", //default, no parsing
			IsEnum = propertySymbol?.Type.TypeKind == TypeKind.Enum,
			ParseMethodName = parsedWithMethod
		};
	}

	/// <summary>
	/// Creates a PropertyAndAttributeInfo instance for a property with a FlagAttribute.
	/// </summary>
	/// <param name="property">The property declaration syntax.</param>
	/// <returns>A PropertyAndAttributeInfo instance for the FlagAttribute.</returns>
	private PropertyAndAttributeInfo CreateFlagAttribute(PropertyDeclarationSyntax property)
	{
		var propertySymbol = _semanticModel.GetDeclaredSymbol(property) as IPropertySymbol;
		var data = FindAttribute(propertySymbol, FlagAttributeName);
		var attribute = new AttributeInfo(StringArgument(data, 0), StringArgument(data, 1), StringArgument(data, 2), false, -1);
		return CreateInfo(property, propertySymbol, attribute);
	}

	/// <summary>
	/// Creates a PropertyAndAttributeInfo instance for a property with an OptionAttribute.
	/// </summary>
	/// <param name="property">The property declaration syntax.</param>
	/// <returns>A PropertyAndAttributeInfo instance for the OptionAttribute.</returns>
	private PropertyAndAttributeInfo CreateOptionAttribute(PropertyDeclarationSyntax property)
	{
		var propertySymbol = _semanticModel.GetDeclaredSymbol(property) as IPropertySymbol;
		var data = FindAttribute(propertySymbol, OptionAttributeName);
		var attribute = new AttributeInfo(StringArgument(data, 0), StringArgument(data, 1), StringArgument(data, 2), BoolArgument(data, 3), -1);
		return CreateInfo(property, propertySymbol, attribute);
	}

	/// <summary>
	/// Creates a PropertyAndAttributeInfo instance for a property with a PositionalAttribute.
	/// </summary>
	/// <param name="property">The property declaration syntax.</param>
	/// <returns>A PropertyAndAttributeInfo instance for the PositionalAttribute.</returns>
	private PropertyAndAttributeInfo CreatePositionalInfo(PropertyDeclarationSyntax property)
	{
		var propertySymbol = _semanticModel.GetDeclaredSymbol(property) as IPropertySymbol;
		var data = FindAttribute(propertySymbol, PositionalAttributeName);
		var position = int.Parse(StringArgument(data, 0), CultureInfo.InvariantCulture);
		var attribute = new AttributeInfo("", "", StringArgument(data, 1), BoolArgument(data, 2), position);
		return CreateInfo(property, propertySymbol, attribute);
	}
}

/// <summary>
/// Represents an attribute and its associated property information.
/// </summary>
public struct PropertyAndAttributeInfo : IEquatable<PropertyAndAttributeInfo>
{
	/// <summary>
	/// The attribute instance.
	/// </summary>
	public AttributeInfo Attribute { get; set; }

	/// <summary>
	/// The name of the property.
	/// </summary>
	public string PropertyName { get; set; }

	/// <summary>
	/// The type of the property.
	/// </summary>
	public string PropertyType { get; set; }

	/// <summary>
	/// Indicates whether the property type is an enum.
	/// </summary>
	public bool IsEnum { get; set; }

	/// <summary>
	/// The name of the method used for parsing the property value.
	/// </summary>
	public string ParseMethodName { get; set; }

	/// <summary>
	/// Indicates whether the property has a parse method defined.
	/// </summary>
	public readonly bool HasParseMethod { get => !string.IsNullOrEmpty(ParseMethodName); }

	/// <summary>
	/// Determines whether the current instance is equal to another object.
	/// </summary>
	/// <param name="obj"></param>
	/// <returns></returns>
	public override bool Equals(object? obj)
	{
		if (obj is PropertyAndAttributeInfo other)
		{
			return Equals(other);
		}
		return false;
	}

	/// <summary>
	/// Returns a hash code for the current instance.
	/// </summary>
	/// <returns></returns>
	public override int GetHashCode()
	{
		return 17 * 23 + (PropertyName?.GetHashCode() ?? 0) * 31 + (PropertyType?.GetHashCode() ?? 0) * 37 + (IsEnum ? 1 : 0) * 43 +
		       Attribute.GetHashCode() * 41;
	}

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

	public static bool operator ==(PropertyAndAttributeInfo left, PropertyAndAttributeInfo right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(PropertyAndAttributeInfo left, PropertyAndAttributeInfo right)
	{
		return !(left == right);
	}

	public bool Equals(PropertyAndAttributeInfo other)
	{
		return PropertyName == other.PropertyName &&
			PropertyType == other.PropertyType &&
			IsEnum == other.IsEnum &&
			ParseMethodName == other.ParseMethodName &&
			Attribute.Equals(other.Attribute);
	}
}

/// <summary>
/// Represents an attribute and its associated property information
/// </summary> 
public struct AttributeInfo : IEquatable<AttributeInfo>
{
	public string ShortName { get; set; }
	public string LongName { get; set; }
	public string Description { get; set; }
	public bool Required { get; set; }
	public int Position { get; set; }

	public AttributeInfo(string shortName, string longName, string description, bool required, int position)
	{
		ShortName = shortName;
		LongName = longName;
		Description = description;
		Required = required;
		Position = position;
	}

	public override bool Equals(object? obj)
	{
		if (obj is AttributeInfo other)
		{
			return Equals(other);
		}
		return false;
	}

	public bool Equals(AttributeInfo other)
	{
		return string.Equals(ShortName, other.ShortName, StringComparison.Ordinal) &&
			string.Equals(LongName, other.LongName, StringComparison.Ordinal) &&
			string.Equals(Description, other.Description, StringComparison.Ordinal) &&
			Required == other.Required &&
			Position == other.Position;
	}

	public override int GetHashCode()
	{
		int hash = 17;
		hash = hash * 23 + (ShortName != null ? ShortName.GetHashCode() : 0);
		hash = hash * 23 + (LongName != null ? LongName.GetHashCode() : 0);
		hash = hash * 23 + (Description != null ? Description.GetHashCode() : 0);
		hash = hash * 23 + Required.GetHashCode();
		hash = hash * 23 + Position;
		return hash;
	}

	public static bool operator ==(AttributeInfo left, AttributeInfo right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(AttributeInfo left, AttributeInfo right)
	{
		return !(left == right);
	}
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
