// this file contains enums used to configure the parser and influence the generated code.
using System;

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

/// <summary>
/// Describes how a command behaves, for tools that want to know before running it (for example a tool list for an MCP server).
/// The values are the hints of a tool of the Model Context Protocol (MCP): a value that is set states that its hint is true,
/// a value that is not set states that it is false. The default of <see cref="ParameterCollectionAttribute.Annotations"/>
/// is the MCP default, <see cref="Destructive"/> and <see cref="OpenWorld"/>.
/// </summary>
[Flags]
public enum CommandAnnotations
{
	/// <summary>
	/// Every hint is false: the command may change things, but only adds to them, running it again has a further effect,
	/// and it does not reach outside of the application.
	/// </summary>
	None = 0,

	/// <summary>
	/// The command does not change anything (MCP <c>readOnlyHint</c>, false by default).
	/// When it is set, MCP ignores <see cref="Destructive"/> and <see cref="Idempotent"/>.
	/// </summary>
	ReadOnly = 1,

	/// <summary>
	/// The command may delete or overwrite existing data, instead of only adding to it (MCP <c>destructiveHint</c>, true by default).
	/// </summary>
	Destructive = 2,

	/// <summary>
	/// Running the command again with the same arguments has no further effect (MCP <c>idempotentHint</c>, false by default).
	/// </summary>
	Idempotent = 4,

	/// <summary>
	/// The command interacts with things outside of the application, such as other systems or the web,
	/// instead of a closed set of its own (MCP <c>openWorldHint</c>, true by default).
	/// </summary>
	OpenWorld = 8
}
