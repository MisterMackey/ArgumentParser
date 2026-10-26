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
	private const string CommandErrorCategory = "CommandDeclaration";

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

	private const string ARG013_Id = "ARG013";
	private const string ARG013_Title = "Flag attributes should only be applied to boolean or enum properties";
	private const string ARG013_Message = "Flag attribute is applied to property '{0}' of type '{1}', but flags can only be applied to boolean or enum properties";

	private const string ARG014_Id = "ARG014";
	private const string ARG014_Title = "Invalid command name";
	private const string ARG014_Message = "Command name '{0}' on class '{1}' is not valid. A name must not be empty, contain whitespace or start with '-'.";

	private const string ARG015_Id = "ARG015";
	private const string ARG015_Title = "Positional argument on a class with child commands";
	private const string ARG015_Message = "Property '{1}' of class '{0}' cannot be a Positional argument. The first positional argument of a command group or application root " +
					     "is the name of the next command. Only a leaf command (with the ParameterCollection attribute) can have positional arguments.";

	private const string ARG016_Id = "ARG016";
	private const string ARG016_Title = "Conflicting command attributes";
	private const string ARG016_Message = "Class '{0}' has the CommandApp attribute together with the {1} attribute. The root of the command tree cannot also be a command.";

	private const string ARG017_Id = "ARG017";
	private const string ARG017_Title = "Duplicate command name";
	private const string ARG017_Message = "Command name '{0}' is used by more than one child command of '{1}'. The names of the children of one command must be unique.";

	private const string ARG018_Id = "ARG018";
	private const string ARG018_Title = "Command class used more than once";
	private const string ARG018_Message = "Class '{0}' is the type of property '{2}' of '{1}', but it is also the type of another child command property, or it is the root of the tree. " +
					     "A command class must be the type of exactly one child command property, and the root of none.";

	private const string ARG019_Id = "ARG019";
	private const string ARG019_Title = "Command class is not part of a tree";
	private const string ARG019_Message = "Command class '{0}' cannot be reached from a class with the CommandApp attribute, so it can never be selected. " +
					     "Declare it as a property of its parent command.";

	private const string ARG020_Id = "ARG020";
	private const string ARG020_Title = "Command group without commands";
	private const string ARG020_Message = "Class '{0}' has no child commands. Add a property whose type is a command class, " +
					     "or give a command class the ParameterCollection attribute to make it a leaf command.";

	private const string ARG021_Id = "ARG021";
	private const string ARG021_Title = "Leaf command with child commands";
	private const string ARG021_Message = "Property '{1}' of leaf command '{0}' declares a child command, but a class with the ParameterCollection attribute cannot have child commands. " +
					     "Remove the ParameterCollection attribute to make the class a command group.";

	private const string ARG022_Id = "ARG022";
	private const string ARG022_Title = "Child command property cannot be set";
	private const string ARG022_Message = "Property '{1}' of '{0}' declares a child command, but it has no setter the generated code can call. " +
					     "Give it a private setter; an init-only setter does not work.";

	private const string ARG023_Id = "ARG023";
	private const string ARG023_Title = "Generic command class";
	private const string ARG023_Message = "Command class '{0}' is generic or nested in a generic type. The classes of a command tree are created by the generated parser, which cannot supply type arguments.";

	private const string ARG024_Id = "ARG024";
	private const string ARG024_Title = "Abstract command class";
	private const string ARG024_Message = "Command class '{0}' is abstract or static. The generated parser creates an object of every class in the command tree, so it must be a concrete class.";

	private const string ARG025_Id = "ARG025";
	private const string ARG025_Title = "Required member on a command group";
	private const string ARG025_Message = "Member '{1}' of '{0}' has the required modifier, but the generated parser creates groups and the root of a command tree without setting it. " +
					     "Remove the required modifier.";

	private const string ARG026_Id = "ARG026";
	private const string ARG026_Title = "Argument of a command class type";
	private const string ARG026_Message = "Property '{0}' has the {1} attribute, but its type '{2}' is a command class. A property of a command class type declares a child command " +
					     "and cannot also be an argument. Keep the argument attributes and the command attributes on separate types.";

	private const string ARG027_Id = "ARG027";
	private const string ARG027_Title = "Argument name used by a parent command";
	private const string ARG027_Message = "Property '{1}' of '{0}' has the {2} name '{3}', which {4} already uses. " +
					     "A command cannot reuse the option and flag names of the commands it is part of, or the names of the help argument of the tree. Rename one of the two.";

	private const string ARG028_Id = "ARG028";
	private const string ARG028_Title = "Help setting not supported by a command tree";
	private const string ARG028_Message = "The CommandApp attribute of '{0}' uses {1}, which a command tree does not support. The tree generates the help text of every level, " +
					     "so HelpTextGeneration must be GenerateAll, GenerateTextOnly or None, and None cannot be combined with BehaviourOnError.DisplayHelpAndExit, which displays the help text on an error.";

	private const string ARG029_Id = "ARG029";
	private const string ARG029_Title = "Help, error and culture settings of a leaf are not used by its command tree";
	private const string ARG029_Message = "The ParameterCollection attribute of leaf command '{0}' sets help, error or culture settings, but parsing through the command tree uses those of the CommandApp attribute of '{1}'. " +
					     "They only apply to the Parse method of '{0}' itself. Set them on the CommandApp attribute instead, or leave them out.";

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
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG006.md"
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

	/// <summary>
	/// Diagnostic descriptor for ARG013: Flag attributes should only be applied to boolean properties.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG013 = new DiagnosticDescriptor(
	    ARG013_Id,
	    ARG013_Title,
	    ARG013_Message,
	    AttributeErrorCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG013.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG014: A command name must not be empty, contain whitespace or start with '-'.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG014 = new DiagnosticDescriptor(
	    ARG014_Id,
	    ARG014_Title,
	    ARG014_Message,
	    CommandErrorCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG014.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG015: A command group or application root cannot have positional arguments.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG015 = new DiagnosticDescriptor(
	    ARG015_Id,
	    ARG015_Title,
	    ARG015_Message,
	    CommandErrorCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG015.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG016: The CommandApp attribute cannot be combined with the Command or ParameterCollection attribute.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG016 = new DiagnosticDescriptor(
	    ARG016_Id,
	    ARG016_Title,
	    ARG016_Message,
	    CommandErrorCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG016.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG017: The children of one command must have different names.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG017 = new DiagnosticDescriptor(
	    ARG017_Id,
	    ARG017_Title,
	    ARG017_Message,
	    CommandErrorCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG017.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG018: A command class can be the child of one property only.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG018 = new DiagnosticDescriptor(
	    ARG018_Id,
	    ARG018_Title,
	    ARG018_Message,
	    CommandErrorCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG018.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG019: A command class that cannot be reached from a CommandApp class is never selected.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG019 = new DiagnosticDescriptor(
	    ARG019_Id,
	    ARG019_Title,
	    ARG019_Message,
	    CommandErrorCategory,
	    DiagnosticSeverity.Warning,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG019.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG020: A command group or application root needs at least one child command.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG020 = new DiagnosticDescriptor(
	    ARG020_Id,
	    ARG020_Title,
	    ARG020_Message,
	    CommandErrorCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG020.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG021: A leaf command cannot have child commands.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG021 = new DiagnosticDescriptor(
	    ARG021_Id,
	    ARG021_Title,
	    ARG021_Message,
	    CommandErrorCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG021.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG022: A child command property needs a setter that the generated code can call.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG022 = new DiagnosticDescriptor(
	    ARG022_Id,
	    ARG022_Title,
	    ARG022_Message,
	    CommandErrorCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG022.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG023: A class of a command tree cannot be generic.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG023 = new DiagnosticDescriptor(
	    ARG023_Id,
	    ARG023_Title,
	    ARG023_Message,
	    CommandErrorCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG023.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG024: A class of a command tree cannot be abstract.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG024 = new DiagnosticDescriptor(
	    ARG024_Id,
	    ARG024_Title,
	    ARG024_Message,
	    CommandErrorCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG024.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG025: A command group or the root cannot have required members.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG025 = new DiagnosticDescriptor(
	    ARG025_Id,
	    ARG025_Title,
	    ARG025_Message,
	    CommandErrorCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG025.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG026: An argument cannot have a command class as its type.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG026 = new DiagnosticDescriptor(
	    ARG026_Id,
	    ARG026_Title,
	    ARG026_Message,
	    CommandErrorCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG026.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG027: A command cannot reuse an option or flag name of a command above it, or of the help argument of the tree.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG027 = new DiagnosticDescriptor(
	    ARG027_Id,
	    ARG027_Title,
	    ARG027_Message,
	    CommandErrorCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG027.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG028: The CommandApp attribute uses a help setting a tree cannot use.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG028 = new DiagnosticDescriptor(
	    ARG028_Id,
	    ARG028_Title,
	    ARG028_Message,
	    CommandErrorCategory,
	    DiagnosticSeverity.Error,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG028.md"
	);

	/// <summary>
	/// Diagnostic descriptor for ARG029: The help and error settings of a leaf in a tree are not used by the tree.
	/// </summary>
	public static readonly DiagnosticDescriptor ARG029 = new DiagnosticDescriptor(
	    ARG029_Id,
	    ARG029_Title,
	    ARG029_Message,
	    CommandErrorCategory,
	    DiagnosticSeverity.Warning,
	    true,
	    helpLinkUri: "https://github.com/MisterMackey/ArgumentParser/blob/main/ArgumentParser.Analyzer/docs/rules/ARG029.md"
	);
}
