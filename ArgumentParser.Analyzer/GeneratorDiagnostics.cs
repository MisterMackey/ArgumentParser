using System;
using Microsoft.CodeAnalysis;

namespace ArgumentParser.Internal;

/// <summary>
/// Contains diagnostic descriptors used by the ArgumentParser analyzer.
/// </summary>
public static class GeneratorDiagnostics
{
	private const string ClassErrorCategory = "ClassDeclaration";
	private const string AttributeErrorCategory = "AttributeDeclaration";
	private const string GenerationOptionsCategory = "GenerationOptions";

	private const string ARG001_Id = "ARG001";
	private const string ARG001_Title = "ParameterCollection must be partial";
	private const string ARG001_Message = "Class '{0}' must be declared as a partial class to use the ArgumentParser generator";

	private const string ARG002_Id = "ARG002";
	private const string ARG002_Title = "Parse method already exists";
	private const string ARG002_Message = "Class '{0}' already contains a Parse(string[] args) method. The ArgumentParser generator will not overwrite it. " +
					     "Remove the Parse method to allow the generator to create one.";

	private const string ARG003_Id = "ARG003";
	private const string ARG003_Title = "Duplicate argument name";
	private const string ARG003_Message = "Duplicate {0} name '{1}' found in Option and Flag attributes";

	private const string ARG004_Id = "ARG004";
	private const string ARG004_Title = "Duplicate positional argument position";
	private const string ARG004_Message = "Duplicate positional argument position '{0}' found in Positional attributes";

	private const string ARG005_Id = "ARG005";
	private const string ARG005_Title = "Invalid positional argument position";
	private const string ARG005_Message = "Invalid positional argument position '{0}' found in Positional attributes. " +
					     "Positions must be non-negative and less than the number of positional arguments.";

	private const string ARG006_Id = "ARG006";
	private const string ARG006_Title = "Constructor with no parameters required";
	private const string ARG006_Message = "Class '{0}' must have a constructor with no parameters to use the ArgumentParser generator";

	private const string ARG007_Id = "ARG007";
	private const string ARG007_Title = "Specified property type is not supported";
	private const string ARG007_Message = "Specified property type '{0}' is not supported for argument parsing";

	private const string ARG008_Id = "ARG008";
	private const string ARG008_Title = "Helptext argument names specified but argument generation disabled";
	private const string ARG008_Message = "Helptext argument names are specified, but argument generation is disabled. These settings will be ignored.";

	private const string ARG009_Id = "ARG009";
	private const string ARG009_Title = "HelpText property is missing but argument handler is generated";
	private const string ARG009_Message = "HelpText property is missing, but argument handler is generated. Please provide a HelpText property or disable handler generation.";

	private const string ARG010_Id = "ARG010";
	private const string ARG010_Title = "HelpText property is missing but display help on error handler is generated";
	private const string ARG010_Message = "HelpText property is missing, but display help on error handler is generated. Please provide a HelpText property or disable this handler.";

	private const string ARG011_Id = "ARG011";
	private const string ARG011_Title = "DisplayHelp property specified but generator is supplying it";
	private const string ARG011_Message = "DisplayHelp property is specified by the user, but the generator is supplying it. Remove the property from your class.";

	private const string ARG012_Id = "ARG012";
	private const string ARG012_Title = "HelpText const string specified but generator is supplying it";
	private const string ARG012_Message = "HelpText const string is specified by the user, but the generator is supplying it. Remove the const string from your class.";

	/// <summary>
	/// Diagnostic descriptor for ARG001: Classes with ParameterCollection attribute must be partial.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG001 = new DiagnosticDescriptor(
	    ARG001_Id,
	    ARG001_Title,
	    ARG001_Message,
	    ClassErrorCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG001.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG002: Class already contains a Parse method.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG002 = new DiagnosticDescriptor(
	    ARG002_Id,
	    ARG002_Title,
	    ARG002_Message,
	    ClassErrorCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG002.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG003: Duplicate argument names found in Options and Flags.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG003 = new DiagnosticDescriptor(
	    ARG003_Id,
	    ARG003_Title,
	    ARG003_Message,
	    AttributeErrorCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG003.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG004: Duplicate positions found in Positional attributes.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG004 = new DiagnosticDescriptor(
	    ARG004_Id,
	    ARG004_Title,
	    ARG004_Message,
	    AttributeErrorCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG004.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG005: Invalid position values in Positional attributes.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG005 = new DiagnosticDescriptor(
	    ARG005_Id,
	    ARG005_Title,
	    ARG005_Message,
	    AttributeErrorCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG005.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG006: Class must have a constructor with no parameters.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG006 = new DiagnosticDescriptor(
	    ARG006_Id,
	    ARG006_Title,
	    ARG006_Message,
	    ClassErrorCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "blah"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG007: Helptext argument names specified but argument generation disabled.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG007 = new DiagnosticDescriptor(
	    ARG007_Id,
	    ARG007_Title,
	    ARG007_Message,
	    AttributeErrorCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG007.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG008: HelpText property is missing but argument handler is generated.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG008 = new DiagnosticDescriptor(
	    ARG008_Id,
	    ARG008_Title,
	    ARG008_Message,
	    GenerationOptionsCategory,
	    DiagnosticSeverity.Warning,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG008.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG009: HelpText property is missing but display help on error handler is generated.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG009 = new DiagnosticDescriptor(
	    ARG009_Id,
	    ARG009_Title,
	    ARG009_Message,
	    GenerationOptionsCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG009.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG010: Specified property type is not supported.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG010 = new DiagnosticDescriptor(
	    ARG010_Id,
	    ARG010_Title,
	    ARG010_Message,
	    GenerationOptionsCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG010.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG011: DisplayHelp property specified but generator is supplying it.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG011 = new DiagnosticDescriptor(
	    ARG011_Id,
	    ARG011_Title,
	    ARG011_Message,
	    GenerationOptionsCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG011.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG012: HelpText const string specified but generator is supplying it.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG012 = new DiagnosticDescriptor(
	    ARG012_Id,
	    ARG012_Title,
	    ARG012_Message,
	    GenerationOptionsCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG012.md"
	);
}
