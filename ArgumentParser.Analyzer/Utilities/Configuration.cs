using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArgumentParser.Internal.Utilities;

/// <summary>
/// Represents the configuration settings for the ArgumentParser.
/// </summary>
public class Configuration
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
	/// Initializes a new instance of the <see cref="Configuration"/> class by extracting configuration values from the <see cref="ClassDeclarationSyntax"/>.
	/// </summary>
	/// <param name="classDeclaration">The class declaration syntax node containing the ParameterCollection attribute.</param>
	/// <param name="semanticModel">The semantic model used to resolve symbols.</param>
	public Configuration(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel)
	{
		// set defaults
		HelpTextGenerationMode = "GenerateAll";
		HelpArgumentShortName = "h";
		HelpArgumentLongName = "Help";
		BehaviourOnError = "ThrowIfMissingRequired";
		if (classDeclaration == null)
		{
			throw new ArgumentNullException(nameof(classDeclaration));
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
			.Where(arg => arg.NameEquals != null)
			.ToDictionary(arg => arg.NameEquals!.Name.ToString(), arg =>
			{
				if (arg.Expression is MemberAccessExpressionSyntax memberAccess)
				{
					return memberAccess.Name.Identifier.ValueText;
				}
				else
				{
					return arg.GetFirstToken().ValueText;
				}
			});
		var positionalArguments = attribute.ArgumentList?.Arguments
			.Where(arg => arg.NameEquals == null)
			.Select(arg =>
			{
				if (arg.Expression is MemberAccessExpressionSyntax memberAccess)
				{
					return memberAccess.Name.Identifier.ValueText;
				}
				else
				{
					return arg.GetFirstToken().ValueText;
				}
			})
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
			if (namedArguments.TryGetValue("HelpTextGeneration", out string? textGen))
			{
				HelpTextGenerationMode = textGen;
			}
			if (namedArguments.TryGetValue("HelpArgumentShortName", out string? shortName))
			{
				HelpArgumentShortName = shortName;
			}
			if (namedArguments.TryGetValue("HelpArgumentLongName", out string? longName))
			{
				HelpArgumentLongName = longName;
			}
			if (namedArguments.TryGetValue("BehaviourOnError", out string? errBehaviour))
			{
				BehaviourOnError = errBehaviour;
			}
		}
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
