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
	/// Help text will be displayed automatically if there is an error in parsing.
	/// Argument will be generated to display help text.
	/// </summary>
	GenerateAll = 0, //default

	/// <summary>
	/// No help text will be generated.
	/// No help text will be displayed.
	/// /// </summary>
	None,

	/// <summary>
	/// Help Text will be generated.
	/// Help text will not be displayed automatically.
	/// /// </summary>
	GenerateTextOnly,

	/// <summary>
	/// Help Text will be generated.
	/// Help text will be displayed automatically if there is an error in parsing.
	/// No argument will be generated to display help text.
	/// /// </summary>
	GenerateTextAndErrorHandler,

	/// <summary>
	/// Help Text will be generated.
	/// Help text will not be displayed automatically if there is an error in parsing.
	/// Argument will be generated to display help text.
	/// </summary>
	GenerateTextAndArgumentHandler,

	/// <summary>
	/// Help Text will not be generated.
	/// Help text will be displayed automatically if there is an error in parsing.
	/// No argument will be generated to display help text.
	/// </summary>
	GenerateErrorHandlerOnly,

	/// <summary>
	/// Help Text will not be generated.
	/// Help text will not be displayed automatically if there is an error in parsing.
	/// Argument will be generated to display help text.
	/// </summary>
	GenerateArgumentHandlerOnly,

	/// <summary>
	/// Help Text will not be generated.
	/// Help text will be displayed automatically if there is an error in parsing.
	/// Argument will be generated to display help text.
	/// </summary>
	GenerateHandlersOnly
}

/// <summary>
/// Specifies the behavior of the parser when errors are encountered during argument parsing.
/// </summary>
public enum BehaviourOnError
{
	/// <summary>
	/// Throw an exception only if required arguments are missing.
	/// </summary>
	ThrowIfMissingRequired = 0,

	/// <summary>
	/// Throw an exception if any error occurs during parsing (including missing required arguments or invalid values).
	/// </summary>
	ThrowIfAnyError,

	/// <summary>
	/// Never throw an exception for parsing errors; all errors must be handled by the caller.
	/// </summary>
	ThrowNever
}
