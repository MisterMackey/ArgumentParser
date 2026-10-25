# This document will track changes starting in v1.1.0

## UnReleased / Unplanned changes
- Quick action code fix support
- Overriding program names in generated helptext

### Added
- The first part of command trees (`app ingredient add`): the `CommandApp` and `Command` attributes and the `CommandAnnotations` enum. A `CommandApp` class is the root of the tree and names the application, a `Command` class is a node in it (a leaf when it also has `ParameterCollection`, a group otherwise), and the properties whose type is a node are its child commands. The classes are read and validated, but nothing is generated for them yet, so they cannot be used to parse anything.
  - ARG014: a command name must not be empty, contain whitespace or start with `-`.
  - ARG015: a command group or the root cannot have positional arguments, since the first positional argument is the name of the next command.
  - ARG016: `CommandApp` cannot be combined with `Command` or `ParameterCollection`.
- `ParseOutcomePolicy.Apply` applies a `BehaviourOnError` setting and the help text to the outcome of a parse. An overload takes the `TextWriter` to write to and the `Action<int>` to call instead of `Environment.Exit`, so the policy can be used and tested without ending the process.

### Changed
- The generated `Parse` is split in two. A new `internal static ParseCore(string[] args)` binds the arguments and collects errors without printing, exiting or throwing; `Parse` calls it and then applies the help text and `BehaviourOnError` handling exactly as before. The behaviour of `Parse` does not change, but a class that already declares its own `ParseCore(string[])` no longer compiles.
- The help and error handling of `Parse` (printing, exit codes 0 and 2, throwing the `AggregateException`) moved out of the generated code into the runtime library, so the generated `Parse` is a single call to the new `ParseOutcomePolicy.Apply`. The output and exit codes are unchanged.
- The generator is now incremental: it extracts a plain, comparable model of each annotated class and only generates again when that model changes, so edits elsewhere in a project no longer rerun the generator for every annotated class.
- Diagnostics are now reported at a file path and span instead of a syntax node. Because of this, `#pragma warning disable` and `.editorconfig` severity settings no longer silence the ARG008 warning; use `<NoWarn>ARG008</NoWarn>` in the project file.

### Fixed
- A class in the global namespace produced generated code that did not compile (`namespace <global namespace>`).
- Required arguments were tracked in static state, so after one `Parse` call that supplied them later calls in the same process no longer reported them missing. The tracking is now local to each call.
- Names and descriptions containing quotes or backslashes (and help text containing triple quotes) produced generated code that did not compile. They are now escaped.
- With `BehaviourOnError.ThrowIfAnyError` the thrown `AggregateException` only contained the missing-required errors when both missing and invalid arguments were present. It now contains all errors.
- The generated code only compiled in projects with `ImplicitUsings` enabled. It now declares the usings it needs.
- ARG002 did not recognise an existing `Parse(String[] args)` or `Parse(System.String[] args)`; the clash surfaced as a compiler error instead.
- ARG006 counted a static constructor as a parameterless constructor and did not notice a primary constructor with parameters, so the generated `new TestClass()` failed to compile instead of being reported.
- Short and long names were not told apart: `-v` also set the argument whose long name is `v`, and short and long flags with the same text shared a repeat count. Tokens now carry `IsShort` and the generated code checks it.
- A bare `--` was matched to an argument that has no long name, and swallowed the next argument as its value. It is now reported as an unknown argument.
- The ARG006 diagnostic linked to "blah" instead of its documentation page.
- Named string arguments of `[ParameterCollection]` were read as the argument name: `helpArgumentShortName: "?"` configured a help argument called `helpArgumentShortName`. String constants (`helpArgumentLongName: Names.Help`) gave the constant's identifier instead of its value. Both are now read by value.
- Property attributes were found by a substring match on the attribute name, so a user attribute such as `MyOptionAttribute` was treated as `[Option]`, or could shadow the real attribute. They are now matched on their full name.
- Two properties that differed only in their position, or whose attributes differed in anything but their names, compared as equal.

## v1.3.0
### Added
- Any type may now be used for a property if it is annotated with the ParsedWithMethod attribute
  - To use this attribute, create a static (extension) method that follows the same signature as TryParse (bool TryParse(string input, out var result)).
  - Initialize the ParsedWithMethodAttribute with the name of the method you created.
  - The generate parse method will use the indicated method.
### Improved
- Added more make targets

## v1.2.0
### Added
- All custom enum types are now allowed types. Parsing rules depend on whether the property is annotated with.
  - For Flags:
    - The value is interpreted as a 'level', and the number of times the value is encountered (as an int) is casted to the type
    - This is intended for Flags that can be repeated, i.e. -vv for 'extra verbose'
    - For the example above, an appropriate Verbosity enum would be: 'Silent=0, Verbose=1, ExtraVerbose=2'
    - The backing values for the enum should form a continuous sequence starting at 0
    - Out of range values will silently continue, the enum will hold the integer value (which may not make sense)
  - For Options and Positionals:
    - A TryParse is executed using the string value supplied
    - Failures are added to the error list
### Improved
- Added some automated testing
- Improved build system

## v1.1.1
### Fixed
- Bugfix for allowing DateTime, Timestamp, Guid, Uri types
###
- Started adding automated tests

## v1.1.0
### Added
- Added a way for users to influence the code generation via the ParameterCollection attribute constructor
- Added Helptext generation
  - The generation can be influenced via the HelpTextGeneration enum
    - [Default] GenerateAll -> help text is generated, program displays help text and exits if argument is supplied
    - None -> Nothing is generated
    - GenerateTextOnly -> Help text is generated but nothing is displayed automatically
    - GenerateArgumentOnly -> The 'DisplayHelp' Boolean property is generated, but no text. Use this if you want to supply your own text and handler.
    - GenerateArgumentAndHandler -> No text is generated but the DisplayHelp property and the display handler are generated. Use this to supply your own text.
  - Supply default text by choosing a non text mode and adding a const string HelpText property to your partial class
  - The short/long name argument can be set via the ParameterCollection constructor
- Added overrideable error behaviour.
  - You can control this via the BehaviourOnError enum on the ParameterCollection attribute constructor
    - [Default] DisplayHelpAndExit -> If any errors are encountered they are displayed along with the helptext and program exits.
    - ThrowIfMissingRequired -> Throws aggregate exception if a required parameter is not supplied
    - ThrowIfAnyError -> throws aggregate exception if any error is encountered
    - ThrowNever -> No exceptions are thrown. Errors are still accumulated in the return value of the Parse method and may be handled in any way.
### Improved
- Improved the project structure to allow better packaging
- Various changes to internal structure to make changes / features easier.
- Added generated code attribute to generated code
