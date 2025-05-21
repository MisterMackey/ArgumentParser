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

	/// <summary>
	/// Gets all properties with OptionAttributes and creates PropertyAndAttributeInfo instances for them.
	/// </summary>
	/// <returns>A list of PropertyAndAttributeInfo instances for OptionAttributes.</returns>
	public IList<PropertyAndAttributeInfo> GetOptionAttributes()
	{
		return _properties
			.Where(p => p.AttributeLists
				.SelectMany(al => al.Attributes)
				.Any(attr =>
				{
					if (!(_semanticModel.GetSymbolInfo(attr).Symbol is IMethodSymbol symbol))
						return false;
					return symbol.ContainingType.ToDisplayString().Contains("OptionAttribute");
				}))
			.Select(o => CreateOptionAttribute(o))
			.ToList();
	}

	/// <summary>
	/// Gets all properties with PositionalAttributes and creates PropertyAndAttributeInfo instances for them.
	/// </summary>
	/// <returns>A list of PropertyAndAttributeInfo instances for PositionalAttributes.</returns>
	public IList<PropertyAndAttributeInfo> GetPositionalAttributes()
	{
		return _properties
			.Where(p => p.AttributeLists
				.SelectMany(al => al.Attributes)
				.Any(attr =>
				{
					if (!(_semanticModel.GetSymbolInfo(attr).Symbol is IMethodSymbol symbol))
						return false;
					return symbol.ContainingType.ToDisplayString().Contains("PositionalAttribute");
				}))
			.Select(p => CreatePositionalInfo(p))
			.ToList();
	}

	/// <summary>
	/// Gets all properties with FlagAttributes and creates PropertyAndAttributeInfo instances for them.
	/// </summary>
	/// <returns>A list of PropertyAndAttributeInfo instances for FlagAttributes.</returns>
	public IList<PropertyAndAttributeInfo> GetFlagAttributes()
	{
		return _properties
			.Where(p => p.AttributeLists
				.SelectMany(al => al.Attributes)
				.Any(attr =>
				{
					if (!(_semanticModel.GetSymbolInfo(attr).Symbol is IMethodSymbol symbol))
						return false;
					return symbol.ContainingType.ToDisplayString().Contains("FlagAttribute");
				}))
			.Select(f => CreateFlagAttribute(f))
			.ToList();
	}

	/// <summary>
	/// Creates a PropertyAndAttributeInfo instance for a property with a FlagAttribute.
	/// </summary>
	/// <param name="property">The property declaration syntax.</param>
	/// <returns>A PropertyAndAttributeInfo instance for the FlagAttribute.</returns>
	private PropertyAndAttributeInfo CreateFlagAttribute(PropertyDeclarationSyntax property)
	{
		var flagAttributeData = _semanticModel.GetDeclaredSymbol(property)?.GetAttributes()
		    .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString().Contains("FlagAttribute") == true);

		string shortName = string.Empty;
		string longName = string.Empty;
		string description = string.Empty;

		if (flagAttributeData != null)
		{
			// Extract positional arguments
			if (flagAttributeData.ConstructorArguments.Length > 0)
			{
				shortName = flagAttributeData.ConstructorArguments[0].Value?.ToString() ?? string.Empty;
			}
			if (flagAttributeData.ConstructorArguments.Length > 1)
			{
				longName = flagAttributeData.ConstructorArguments[1].Value?.ToString() ?? string.Empty;
			}
			if (flagAttributeData.ConstructorArguments.Length > 2)
			{
				description = flagAttributeData.ConstructorArguments[2].Value?.ToString() ?? string.Empty;
			}

			// Extract named arguments
			foreach (var namedArg in flagAttributeData.NamedArguments)
			{
				if (namedArg.Key == "name")
				{
					shortName = namedArg.Value.Value?.ToString() ?? shortName; // Override if named argument is provided
				}
				else if (namedArg.Key == "longName")
				{
					longName = namedArg.Value.Value?.ToString() ?? longName;
				}
				else if (namedArg.Key == "description")
				{
					description = namedArg.Value.Value?.ToString() ?? description;
				}
			}
		}

		var propertyName = property.Identifier.Text;
		var propertySymbol = _semanticModel.GetDeclaredSymbol(property) as IPropertySymbol;
		var propertyType = propertySymbol?.Type.ToDisplayString() ?? "string"; //default, no parsing

		return new PropertyAndAttributeInfo
		{
			Attribute = new AttributeInfo(shortName, longName, description, false, -1),
			PropertyName = propertyName,
			PropertyType = propertyType
		};
	}

	/// <summary>
	/// Creates a PropertyAndAttributeInfo instance for a property with an OptionAttribute.
	/// </summary>
	/// <param name="property">The property declaration syntax.</param>
	/// <returns>A PropertyAndAttributeInfo instance for the OptionAttribute.</returns>
	private PropertyAndAttributeInfo CreateOptionAttribute(PropertyDeclarationSyntax property)
	{
		var optionAttributeData = _semanticModel.GetDeclaredSymbol(property)?.GetAttributes()
		    .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString().Contains("OptionAttribute") == true);

		string shortName = string.Empty;
		string longName = string.Empty;
		string description = string.Empty;
		bool required = false;

		if (optionAttributeData != null)
		{
			// Extract positional arguments
			if (optionAttributeData.ConstructorArguments.Length > 0)
			{
				shortName = optionAttributeData.ConstructorArguments[0].Value?.ToString() ?? string.Empty;
			}
			if (optionAttributeData.ConstructorArguments.Length > 1)
			{
				longName = optionAttributeData.ConstructorArguments[1].Value?.ToString() ?? string.Empty;
			}
			if (optionAttributeData.ConstructorArguments.Length > 2)
			{
				description = optionAttributeData.ConstructorArguments[2].Value?.ToString() ?? string.Empty;
			}
			if (optionAttributeData.ConstructorArguments.Length > 3)
			{
				var value = optionAttributeData.ConstructorArguments[3].Value;
				if (value is bool)
				{
					required = (bool)value;
				}
			}

			// Extract named arguments
			foreach (var namedArg in optionAttributeData.NamedArguments)
			{
				if (namedArg.Key == "shortName")
				{
					shortName = namedArg.Value.Value?.ToString() ?? shortName;
				}
				else if (namedArg.Key == "longName")
				{
					longName = namedArg.Value.Value?.ToString() ?? longName;
				}
				else if (namedArg.Key == "description")
				{
					description = namedArg.Value.Value?.ToString() ?? description;
				}
				else if (namedArg.Key == "required")
				{
					var value = namedArg.Value.Value;
					if (value is bool)
					{
						required = (bool)value;
					}
				}
			}
		}

		var propertyName = property.Identifier.Text;
		var propertySymbol = _semanticModel.GetDeclaredSymbol(property) as IPropertySymbol;
		var propertyType = propertySymbol?.Type.ToDisplayString() ?? "string"; //default, no parsing

		return new PropertyAndAttributeInfo
		{
			Attribute = new AttributeInfo(shortName, longName, description, required, -1),
			PropertyName = propertyName,
			PropertyType = propertyType
		};
	}

	/// <summary>
	/// Creates a PropertyAndAttributeInfo instance for a property with a PositionalAttribute.
	/// </summary>
	/// <param name="property">The property declaration syntax.</param>
	/// <returns>A PropertyAndAttributeInfo instance for the PositionalAttribute.</returns>
	private PropertyAndAttributeInfo CreatePositionalInfo(PropertyDeclarationSyntax property)
	{
		var positionalAttributeData = _semanticModel.GetDeclaredSymbol(property)?.GetAttributes()
		    .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString().Contains("PositionalAttribute") == true);

		string position = string.Empty;
		string description = string.Empty;
		bool required = false;

		if (positionalAttributeData != null)
		{
			// Extract positional arguments
			if (positionalAttributeData.ConstructorArguments.Length > 0)
			{
				position = positionalAttributeData.ConstructorArguments[0].Value?.ToString() ?? string.Empty;
			}
			if (positionalAttributeData.ConstructorArguments.Length > 1)
			{
				description = positionalAttributeData.ConstructorArguments[1].Value?.ToString() ?? string.Empty;
			}
			if (positionalAttributeData.ConstructorArguments.Length > 2)
			{
				var value = positionalAttributeData.ConstructorArguments[2].Value;
				if (value is bool)
				{
					required = (bool)value;
				}
			}

			// Extract named arguments
			foreach (var namedArg in positionalAttributeData.NamedArguments)
			{
				if (namedArg.Key == "position")
				{
					position = namedArg.Value.Value?.ToString() ?? position;
				}
				else if (namedArg.Key == "description")
				{
					description = namedArg.Value.Value?.ToString() ?? description;
				}
				else if (namedArg.Key == "required")
				{
					var value = namedArg.Value.Value;
					if (value is bool)
					{
						required = (bool)value;
					}
				}
			}
		}

		var propertyName = property.Identifier.Text;
		var propertySymbol = _semanticModel.GetDeclaredSymbol(property) as IPropertySymbol;
		var propertyType = propertySymbol?.Type.ToDisplayString() ?? "string"; //default, no parsing

		return new PropertyAndAttributeInfo
		{
			Attribute = new AttributeInfo("", "", description, required, int.Parse(position, CultureInfo.InvariantCulture)),
			PropertyName = propertyName,
			PropertyType = propertyType
		};
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
	/// Determines whether the current instance is equal to another object.
	/// </summary>
	/// <param name="obj"></param>
	/// <returns></returns>
	public override bool Equals(object obj)
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
		return 17 * 23 + PropertyName.GetHashCode() * 31 + PropertyType.GetHashCode() * 37 +
		       Attribute.GetType().GetHashCode() * 41;
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
			Attribute.GetType() == other.Attribute.GetType();
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
			Required == other.Required;
	}

	public override int GetHashCode()
	{
		int hash = 17;
		hash = hash * 23 + (ShortName != null ? ShortName.GetHashCode() : 0);
		hash = hash * 23 + (LongName != null ? LongName.GetHashCode() : 0);
		hash = hash * 23 + (Description != null ? Description.GetHashCode() : 0);
		hash = hash * 23 + Required.GetHashCode();
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
