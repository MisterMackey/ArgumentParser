# This document will track changes starting in v1.1.0

## UnReleased / Unplanned changes
- Quick action code fix support
- Overriding program names in generated helptext

### Added
- Command trees (`appname subcommand action`, such as `myFileManagingTool directories list --showhidden`): the `CommandApp` and `Command` attributes. A `CommandApp` class is the root of the tree and names the application, a `Command` class is a node in it (a leaf when it also has `ParameterCollection`, a group otherwise), and the properties whose type is a node are its child commands. For every class of the tree the generator adds an internal `ParseCommand`; for the root and every group it reads the `Option` and `Flag` properties of the class and hands the arguments after the command word to the child it names. The root also gets a public `Parse(string[])`, which returns the root with only the typed path filled in, and a static `Commands` list with a `CommandInfo` (path, description, annotations, help text and a `Parse` method) for every leaf. `Selected` on the root is the leaf that was parsed (`null` when none was reached) and `SelectedCommand` its `CommandInfo`, so a program can switch on the class of the leaf. The options of a level must come before the next command word. A leaf is parsed by its own parser, on the arguments after its command word. See "Command trees" in the README.
  - `CommandApp` takes the same help and error settings as `ParameterCollection` (`HelpTextGeneration`, help argument names, `BehaviourOnError`, with the same defaults), and `Parse` of the root applies them to the whole tree. Every level gets a generated help text, the public `CommandHelpText` constant: the root and a group show their commands and options, a leaf shows its full path (`myFileManagingTool directories list`) and its arguments, and every level lists the options of the levels above it. The help argument can be given on every level and shows the help of the level it is given on (`myFileManagingTool -h directories` shows the help of `myFileManagingTool`); an error shows the help of the deepest level that was reached. The settings of a leaf's own `ParameterCollection` attribute only apply to its own `Parse`.
  - `ParameterCollection` has an `Annotations` property (`CommandAnnotations` flags: `ReadOnly`, `Destructive`, `Idempotent`, `OpenWorld`), which a leaf hands on to its `CommandInfo` so a tool list (for example for an MCP server) can be built from `Commands`. The flags are the MCP tool hints: a flag that is set states that its hint is true, one that is not set that it is false, and the default is the MCP default, `Destructive | OpenWorld`. A value that is given replaces the default as a whole (`ReadOnly` alone also states that the command does not reach outside of the application). The annotations do not change parsing or help text.
  - The tree adds members to its classes: `ParseCommand`, `CommandHelpText` and `CommandHelpArgument` on every level, and `commandFlags` on a leaf (besides `Parse`, `Commands`, `Selected` and `SelectedCommand` on the root). A class of the tree that declares a member with one of these names does not compile; there is no rule for it yet.
  - A tree with a leaf that has errors of its own (an ARG rule about the class or its arguments) is not generated, so those errors are not followed by compiler errors in the generated code of the tree.
  - ARG014: a command name must not be empty, contain whitespace or start with `-`.
  - ARG015: a command group or the root cannot have positional arguments, since the first positional argument is the name of the next command.
  - ARG016: `CommandApp` cannot be combined with `Command` or `ParameterCollection`.
  - ARG017: the children of one command must have different names.
  - ARG018: a command class can be the type of one child command property only (not two, in one class or in two), and the root of none.
  - ARG019 (warning): a `Command` class that no `CommandApp` class reaches is never selected.
  - ARG020: a group or the root needs at least one child command.
  - ARG021: a leaf (a class with `ParameterCollection`) cannot have child commands.
  - ARG022: a child command property needs a setter the generated code can call (`{ get; private set; }`).
  - ARG023: a class of the tree cannot be generic (or nested in a generic type).
  - ARG024: a class of the tree cannot be abstract or static.
  - ARG025: the root and groups cannot have `required` members.
  - ARG026: an `Option`, `Flag` or `Positional` property cannot have a command class as its type, in any class. Such a property declares a child command; keep argument and command attributes on separate types.
  - ARG027: an option or flag cannot have a short or long name that a command above it (its group, their groups or the root) already uses, or a name of the help argument of the tree. Commands side by side may share names.
  - ARG028: the `CommandApp` attribute cannot use the help modes in which the user writes the help text (`GenerateArgumentOnly`, `GenerateArgumentAndHandler`), nor `None` together with `DisplayHelpAndExit`.
  - ARG029 (warning): a leaf in a tree whose `ParameterCollection` attribute sets help, error or culture settings (`UseCurrentCulture`) of its own, which the tree does not use.
  - Classes of the tree can be nested in other (partial) types: the generated code declares the surrounding types again. Options and flags of a group or the root are read from every part of a partial class, also from other files. Child commands and options that are inherited from a base class are not supported yet. An explicit interface implementation is never a child command.
- `MissingCommandException` and `UnknownCommandException` for a command that is missing or not one of the commands of its group, `CommandInfo` for the entries of `Commands`, `ArgumentTokenizer.TokenizeLeadingArguments`, which tokenizes only the options and flags before the first other argument, and `ArgumentTokenizer.ContainsFlag`, which tells whether a flag is among the tokens.
- `UseCurrentCulture` on `ParameterCollection` and `CommandApp`: true reads numbers, dates and times with the culture of the current thread, the way the `TryParse(string, out T)` method of each type does (what every version before this one did). On `CommandApp` it applies to every level of the tree, leaves included. See "Numbers, dates and times" in the README.
- `ValueParser`, the runtime class that the generated parsers call to convert the text of numbers, dates and times, with a `TryParse(string, bool useCurrentCulture, out T)` overload per type.
- `ParseOutcomePolicy.Apply` applies a `BehaviourOnError` setting and the help text to the outcome of a parse. An overload takes the `TextWriter` to write to and the `Action<int>` to call instead of `Environment.Exit`, so the policy can be used and tested without ending the process.

### Changed
- Numbers, dates and times are read with the invariant culture, and only in forms that mean the same everywhere, instead of with the culture of the machine. Numbers never accept a thousands separator and always use `.` as the decimal point: `1,5` is now an error, where it used to be 15 (or 1.5 with a Dutch or German culture). A `DateTime` must be ISO 8601 (`2026-10-26`, `2026-10-26T14:30:15Z`, `2026-10-26T14:30+02:00`), so `01/02/2026` is now an error instead of a date that depends on the culture. `TimeSpan` uses `.` for fractions of a second. Set `UseCurrentCulture = true` on the attribute to keep the old behaviour.
- A `DateTime` given with `Z` now has `DateTimeKind.Utc` and keeps its time, instead of being converted to local time; a value with an offset is still converted to local time, and one without either is `DateTimeKind.Unspecified` (as before).
- The generated parser file is named `<Namespace>.<Outer>.<ClassName>_Parser.g.cs` (the namespace and the types the class is nested in are part of the name) instead of `<ClassName>_Parser.g.cs`. Two classes with the same name in different namespaces made the generator fail with a duplicate file name, which is much more likely with command trees (an `Add` below each of two groups).
- The generated `Parse` is split in two. A new `internal static ParseCore(string[] args)` binds the arguments and collects errors without printing, exiting or throwing; `Parse` calls it and then applies the help text and `BehaviourOnError` handling exactly as before. The behaviour of `Parse` does not change, but a class that already declares its own `ParseCore(string[])` no longer compiles.
- The help and error handling of `Parse` (printing, exit codes 0 and 2, throwing the `AggregateException`) moved out of the generated code into the runtime library, so the generated `Parse` is a single call to the new `ParseOutcomePolicy.Apply`. The output and exit codes are unchanged.
- The generator is now incremental: it extracts a plain, comparable model of each annotated class and only generates again when that model changes, so edits elsewhere in a project no longer rerun the generator for every annotated class.
- Diagnostics are now reported at a file path and span instead of a syntax node. Because of this, `#pragma warning disable` and `.editorconfig` severity settings no longer silence the ARG008 warning; use `<NoWarn>ARG008</NoWarn>` in the project file.

### Fixed
- A `ParameterCollection` class declared in several parts (in one file or in several) only had the `Option`, `Flag` and `Positional` properties of the part with the attribute. The arguments of every part are read now, and the checks for an existing `Parse`, constructors, `HelpText` and `DisplayHelp` look at every part too.
- An `internal` (or otherwise not public) `ParameterCollection` class did not compile, because the generated part was always declared `public`. The generated part now has no access modifier and takes the one of the class.
- A class in a namespace with a segment named `ArgumentParser` (such as `MyApp.ArgumentParser`) did not compile, because the generated code named the library types `ArgumentParser.X`, which then pointed into that namespace. The generated code uses `global::ArgumentParser.X` now.
- A class named after a keyword (`@event`) made the generator fail on the file name, and a class nested in such a type was declared again without its `@`.
- A `ParameterCollection` class nested in another type got a generated parser in a new top level class with the same name instead of in the nested class. The generated code now declares the surrounding types (which must be partial) again.
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
