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
	    helpLinkUri: "https://github.com/yourusername/ArgumentParser/blob/main/docs/rules/ARG001.md"
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
	    helpLinkUri: "https://github.com/yourusername/ArgumentParser/blob/main/docs/rules/ARG002.md"
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
	    helpLinkUri: "https://github.com/yourusername/ArgumentParser/blob/main/docs/rules/ARG003.md"
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
	    helpLinkUri: "https://github.com/yourusername/ArgumentParser/blob/main/docs/rules/ARG004.md"
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
	    helpLinkUri: "https://github.com/yourusername/ArgumentParser/blob/main/docs/rules/ARG005.md"
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
}
