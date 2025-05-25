// this file contains enums used to configure the parser and influence the generated code.
namespace ArgumentParser;

/// <summary>
/// Enum to specify the generation of help text.
/// If set to generate handler(s) only, user must provide a help text.
/// Help text can be provided by providing a 'const string _helpText' field in the class.
/// </summary>
public enum HelpTextGeneration
{
	/// <summary>
	/// Help Text will be generated.
	/// Argument will be generated to display help text.
	/// Help argument triggers help display and exits the program.
	/// </summary>
	GenerateAll = 0, //default

	/// <summary>
	/// Nothing is generated.
	/// </summary>
	None,

	/// <summary>
	/// Help Text will be generated.
	/// </summary>
	GenerateTextOnly,

	/// <summary>
	/// Help Text will not be generated.
	/// Argument will be generated to display help text.
	/// </summary>
	GenerateArgumentOnly,

	/// <summary>
	/// Help Text will not be generated.
	/// Argument will be generated to display help text.
	/// Help argument triggers help display and exits the program.
	/// </summary>
	GenerateArgumentAndHandler
}

/// <summary>
/// Specifies the behavior of the parser when errors are encountered during argument parsing.
/// </summary>
public enum BehaviourOnError
{
	/// <summary>
	/// Display help text and exit the program if any error occurs during parsing.
	/// </summary>
	DisplayHelpAndExit = 0, // this is used to display help text and exit the program

	/// <summary>
	/// Throw an exception if any error occurs during parsing (including missing required arguments or invalid values).
	/// </summary>
	ThrowIfAnyError,

	/// <summary>
	/// Never throw an exception for parsing errors; all errors must be handled by the caller.
	/// </summary>
	ThrowNever,

	/// <summary>
	/// Throw an exception only if required arguments are missing.
	/// </summary>
	ThrowIfMissingRequired
}
