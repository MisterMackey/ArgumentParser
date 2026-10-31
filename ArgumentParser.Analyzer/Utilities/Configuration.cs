using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArgumentParser.Internal.Utilities;

/// <summary>
/// Represents the configuration settings for the ArgumentParser.
/// </summary>
public class GeneratorConfiguration : IEquatable<GeneratorConfiguration>
{
	/// <summary>
	/// Gets or sets the help text generation mode.
	/// </summary>
	public string? HelpTextGenerationMode { get; set; }
	/// <summary>
	/// Gets or sets the short name for the help argument (e.g., 'h' for '-h').
	/// </summary>
	public string? HelpArgumentShortName { get; set; }
	/// <summary>
	/// Gets or sets the long name for the help argument (e.g., 'Help' for '--Help').
	/// </summary>
	public string? HelpArgumentLongName { get; set; }
	/// <summary>
	/// Gets or sets the behavior of the parser when errors are encountered during argument parsing.
	/// </summary>
	public string? BehaviourOnError { get; set; }
	/// <summary>
	/// Gets or sets a value indicating whether numbers, dates and times are read with the current culture instead of the invariant culture.
	/// </summary>
	public bool UseCurrentCulture { get; set; }
	/// <summary>
	/// Gets or sets a value indicating whether the JSON Schema of the class is generated.
	/// </summary>
	public bool EmitMcpManifest { get; set; }
	/// <summary>
	/// Gets or sets a value indicating whether the JSON binder of the class is generated.
	/// </summary>
	public bool EmitJsonBinder { get; set; }
	/// <summary>
	/// Gets a value indicating whether the class is described as a tool, by its JSON Schema, its JSON binder or both.
	/// The rules about the keys of the schema only apply then.
	/// </summary>
	public bool DescribesTool => EmitMcpManifest || EmitJsonBinder;

	/// <summary>
	/// Initializes a new instance of the <see cref="GeneratorConfiguration"/> class by extracting configuration values from the <see cref="ClassDeclarationSyntax"/>.
	/// </summary>
	/// <param name="classDeclaration">The class declaration syntax node containing the ParameterCollection attribute.</param>
	/// <param name="semanticModel">The semantic model used to resolve symbols.</param>
	public GeneratorConfiguration(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel)
	{
		// set defaults
		HelpTextGenerationMode = "GenerateAll";
		HelpArgumentShortName = "h";
		HelpArgumentLongName = "Help";
		BehaviourOnError = "DisplayHelpAndExit";
		if (classDeclaration == null)
		{
			throw new ArgumentNullException(nameof(classDeclaration));
		}
		if (semanticModel == null)
		{
			throw new ArgumentNullException(nameof(semanticModel));
		}

		// get the ParameterCollection attribute from the class
		var attribute = classDeclaration.AttributeLists
			.SelectMany(list => list.Attributes)
			.FirstOrDefault(attr =>
				semanticModel.GetSymbolInfo(attr).Symbol is IMethodSymbol methodSymbol &&
				methodSymbol.ContainingType.ToDisplayString() == "ArgumentParser.ParameterCollectionAttribute");
		if (attribute == null)
		{
			throw new InvalidOperationException("Configuration could not be created. No ParameterCollection attribute found.");
		}
		// get the named and positional arguments used in the attribute
		var namedArguments = attribute.ArgumentList?.Arguments
			.Where(arg => arg.NameColon != null)
			.ToDictionary(arg => arg.NameColon!.Name.ToString(), arg => ArgumentValue(arg, semanticModel));
		// a property set by name (Annotations = ...) is no constructor argument
		var positionalArguments = attribute.ArgumentList?.Arguments
			.Where(arg => arg.NameColon == null && arg.NameEquals == null)
			.Select(arg => ArgumentValue(arg, semanticModel))
			.ToList();
		// make sure to update this part if the attribute changes
		for (int i = 0; i < positionalArguments?.Count; i++)
		{
			switch (i)
			{
				case 0:
					HelpTextGenerationMode = positionalArguments[i];
					break;
				case 1:
					HelpArgumentShortName = positionalArguments[i];
					break;
				case 2:
					HelpArgumentLongName = positionalArguments[i];
					break;
				case 3:
					BehaviourOnError = positionalArguments[i];
					break;
			}
		}
		// deal with named arguments
		if (namedArguments != null)
		{
			if (namedArguments.TryGetValue("helpTextGeneration", out string? textGen))
			{
				HelpTextGenerationMode = textGen;
			}
			if (namedArguments.TryGetValue("helpArgumentShortName", out string? shortName))
			{
				HelpArgumentShortName = shortName;
			}
			if (namedArguments.TryGetValue("helpArgumentLongName", out string? longName))
			{
				HelpArgumentLongName = longName;
			}
			if (namedArguments.TryGetValue("behaviourOnError", out string? errBehaviour))
			{
				BehaviourOnError = errBehaviour;
			}
		}
		UseCurrentCulture = NamedBool(attribute, semanticModel, "UseCurrentCulture");
		EmitMcpManifest = NamedBool(attribute, semanticModel, "EmitMcpManifest");
		EmitJsonBinder = NamedBool(attribute, semanticModel, "EmitJsonBinder");
	}

	// a property of the attribute, set with Name = value
	private static bool NamedBool(AttributeSyntax attribute, SemanticModel semanticModel, string name)
	{
		var argument = attribute.ArgumentList?.Arguments
			.FirstOrDefault(arg => arg.NameEquals?.Name.Identifier.ValueText == name);
		return argument is not null && semanticModel.GetConstantValue(argument.Expression).Value is true;
	}

	// Strings are taken from their constant value, so literals and constants both work.
	// Enum values are taken from the member name, so qualified and unqualified spellings are the same.
	// The first token is taken from the expression, not the argument: for a named argument the argument starts with the name.
	private static string ArgumentValue(AttributeArgumentSyntax argument, SemanticModel semanticModel)
	{
		if (semanticModel.GetConstantValue(argument.Expression).Value is string text)
		{
			return text;
		}
		if (argument.Expression is MemberAccessExpressionSyntax memberAccess)
		{
			return memberAccess.Name.Identifier.ValueText;
		}
		return argument.Expression.GetFirstToken().ValueText;
	}

	/// <inheritdoc/>
	public bool Equals(GeneratorConfiguration? other)
	{
		return other is not null
			&& string.Equals(HelpTextGenerationMode, other.HelpTextGenerationMode, StringComparison.Ordinal)
			&& string.Equals(HelpArgumentShortName, other.HelpArgumentShortName, StringComparison.Ordinal)
			&& string.Equals(HelpArgumentLongName, other.HelpArgumentLongName, StringComparison.Ordinal)
			&& string.Equals(BehaviourOnError, other.BehaviourOnError, StringComparison.Ordinal)
			&& UseCurrentCulture == other.UseCurrentCulture
			&& EmitMcpManifest == other.EmitMcpManifest
			&& EmitJsonBinder == other.EmitJsonBinder;
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj)
	{
		return Equals(obj as GeneratorConfiguration);
	}

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		int hash = 17;
		hash = hash * 23 + (HelpTextGenerationMode?.GetHashCode() ?? 0);
		hash = hash * 23 + (HelpArgumentShortName?.GetHashCode() ?? 0);
		hash = hash * 23 + (HelpArgumentLongName?.GetHashCode() ?? 0);
		hash = hash * 23 + (BehaviourOnError?.GetHashCode() ?? 0);
		hash = hash * 23 + (UseCurrentCulture ? 1 : 0);
		hash = hash * 23 + (EmitMcpManifest ? 1 : 0);
		hash = hash * 23 + (EmitJsonBinder ? 1 : 0);
		return hash;
	}

	/// <summary>
	/// Determines if the help argument should be generated based on the help text generation mode.
	/// </summary>
	/// <returns>True if the help argument should be generated; otherwise, false.</returns>
	public bool HelpArgumentShouldBeGenerated()
	{
		if (HelpTextGenerationMode == null)
		{
			return false;
		}
		return HelpTextGenerationMode.Equals("GenerateAll", StringComparison.OrdinalIgnoreCase)
			|| HelpTextGenerationMode.Equals("GenerateArgumentAndHandler", StringComparison.OrdinalIgnoreCase)
			|| HelpTextGenerationMode.Equals("GenerateArgumentOnly", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Determines if help text should be generated based on the help text generation mode.
	/// </summary>
	/// <returns>True if help text should be generated; otherwise, false.</returns>
	public bool HelpTextShouldBeGenerated()
	{
		if (HelpTextGenerationMode == null)
		{
			return false;
		}
		var mode = HelpTextGenerationMode;
		return mode.Equals("GenerateAll", StringComparison.OrdinalIgnoreCase)
			|| mode.Equals("GenerateTextOnly", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Determines if help text should be displayed when an error occurs, based on the behavior on error setting.
	/// </summary>
	/// <returns>True if help text should be displayed on error; otherwise, false.</returns>
	public bool HelpTextShouldDisplayOnError()
	{
		if (BehaviourOnError == null)
		{
			return false;
		}
		return BehaviourOnError.Equals("DisplayHelpAndExit", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Determines if help text should be displayed on request, based on the help text generation mode.
	/// </summary>
	/// <returns>True if help text should be displayed on request; otherwise, false.</returns>
	public bool HelpTextShouldDisplayOnRequest()
	{
		if (HelpTextGenerationMode == null)
		{
			return false;
		}
		return HelpTextGenerationMode.Equals("GenerateAll", StringComparison.OrdinalIgnoreCase)
			|| HelpTextGenerationMode.Equals("GenerateArgumentAndHandler", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Determines if an exception should be thrown if required arguments are missing, based on the behavior on error setting.
	/// </summary>
	/// <returns>True if an exception should be thrown for missing required arguments; otherwise, false.</returns>
	public bool ShouldThrowIfMissingRequired()
	{
		if (BehaviourOnError == null)
		{
			return true; // default behavior is to throw if missing required arguments
		}
		return BehaviourOnError.Equals("ThrowIfMissingRequired", StringComparison.OrdinalIgnoreCase)
			|| BehaviourOnError.Equals("ThrowIfAnyError", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Determines if an exception should be thrown if any error occurs during parsing, based on the behavior on error setting.
	/// </summary>
	/// <returns>True if an exception should be thrown for any error; otherwise, false.</returns>
	public bool ShouldThrowIfAnyError()
	{
		if (BehaviourOnError == null)
		{
			return true; // default behavior is to throw if any error occurs
		}
		return BehaviourOnError.Equals("ThrowIfAnyError", StringComparison.OrdinalIgnoreCase);
	}

}
