# ArgumentParser
Licensed under the Apache License, Version 2.0

## Table of contents
- [ArgumentParser](#argumentparser)
	- [Table of contents](#table-of-contents)
	- [About](#about)
	- [Quickstart](#quickstart)
		- [Overriding the default behaviour](#overriding-the-default-behaviour)
		- [Supported types for argument properties](#supported-types-for-argument-properties)
	- [Command trees](#command-trees)
		- [Declaring a tree](#declaring-a-tree)
		- [Parsing](#parsing)
		- [Help and errors](#help-and-errors)
		- [The list of commands](#the-list-of-commands)
		- [Annotations](#annotations)
		- [Generated members](#generated-members)
		- [Rules of a tree](#rules-of-a-tree)
	- [Inspecting the generated code](#inspecting-the-generated-code)
	- [Default behaviour](#default-behaviour)
	- [Diagnostics and Errors](#diagnostics-and-errors)
	- [Known bugs (past and present)](#known-bugs-past-and-present)
- [Development](#development)
	- [building and testing locally](#building-and-testing-locally)
	- [creating a release](#creating-a-release)

## About
ArgumentParser provides functionality to parse commandline arguments without the use of Reflection. It is therefore compatible with AOT publishing.

It works by providing a roslyn analyzer, which will augment a user provided partial class with a static Parse(string[] args) method.
This method will take the raw arguments from the commandline (in the form of a string[]) and return an instance of the partial class, and any accumulated errors as a tuple. The properties on the class are set to what was specified on the commandline.

The method is generated during compilation, which allows the generated code to be very simple and not rely on Reflection. By being very simple it will compile fine when publishing an AOT executable.

## Quickstart
*For an example, check out the ExampleConsole project in this repository*

First, add ArgumentParser to your project: 
```
dotnet add package Aot.ArgumentParser
```

Next, create a public partial class and annotate it with the ParameterCollection Attribute.
Inside of it, create public properties annotated with one of the property attributes (Flag/Option/Positional).
```C#
using ArgumentParser;

namespace ExampleConsole;

[ParameterCollection]
public partial class MyCommandLineArguments
{
	// flag attributes are designed as true/false values
	// if -v or --Verbose is specified on the commandline
	// this value will be true after parsing.
	// Flag cannot be used on other types than bool
	// Flags do not have a 'required' property (a required flag is just a true value)
	[Flag(shortName: "v", longName: "Verbose", description: "Enable verbose output")]
	public bool Verbose {get; set;}

	// Option attributes are designed to be passed as a sort of kv pair
	// Options may be passed either via their longName or shortName
	// the argument immediately succeeding this name will be interpreted
	// as the value of the option.

	// You don't need to set both longname and shortname, one is sufficient
	// The C# propertyname also does not need to align with the longName
	// Options may be set to required, triggering an exception if not passed
	// Option properties may have any supported type
	[Option(longName: "Target", required: true)]
	public string Output {get; set;}

	// Positional attributes are passed on the commandline without any
	// named identifier marking them. They are instead distinguished by the
	// ordinal position they take respective of eachother. They may appear at
	// any point in the commandline argument array, as long as they cannot
	// be mistaken for an option value it will parse fine. 

	// Example: '-v --Target myTarget 15' and '15 --Target myTarget -v'
	// will both produce the same output (15 is the 0th positional argument in both cases)
	// BUT
	// '--Target 15 myTarget -v' will give you an exception (myTarget will be parsed to int)

	// Positional arguments may have any supported type
	// Positional arguments may be set to required
	// Positional argument positions must form a sequence from 0..n-1
	[Positional(0, "the amount of times to repeat")]
	public int RepeatTimes {get; set;}

	// To pass a value here, use the default format that .net expects
	// example: 2025-05-05T12:00:00Z or "2025-05-05T12:00:00 +02:00"
	// make sure to quote the value if it contains spaces or special characters
	[Option(shortName: "t", longName: "TimeStamp", description: "The timestamp to use", required: false)]
	public DateTime TimeStamp { get; set; }

	// Enum types are also supported, and when combined with a Flag, can be used
	// to implement levels of verbosity or other similar features.
	// ensure the enum values follow the pattern 0,1,2,3..n
	[Flag("l", "", "A level")]
	public MyVeryOwnEnum Level { get; set; }

	// Combined with an option it lets you set a value based on a string
	[Option(shortName: "", longName: "Level", description: "A level option with a long name")]
	public MyVeryOwnEnum LevelOption { get; set; }
}
```

Now, simply build your project. A Parse method will appear on your class. Simply call it and feed it your args[] to get a parsed MyCommandLineArguments instance back, along with a list of errors (if any).

```C#
var (myArgs, err) = MyCommandLineArguments.Parse(args); // generated method
if (err.Any())
	// handle any errors
// use myArgs object
```

### Overriding the default behaviour
It is possible to change the behaviour of the generator when it comes to the helptext and the behaviour on error. This is done by providing arguments to the constructor of the ParameterCollection attribute. The following parameters correspond to the default values if nothing is specified:
```C#
[ParameterCollection(HelpTextGeneration.GenerateAll, 
	helpArgumentShortName: "h", 
	helpArgumentLongName: "Help", 
	behaviourOnError: BehaviourOnError.DisplayHelpAndExit)]
```
It is possible to prevent generation of the HelpText, its corresponding arguments, or to change the value of the parameter long/short name.
The behaviour when a parsing error is encountered can also be configured.

*Some combinations may not be valid, an error diagnostic is displayed with more info in that case. Example: preventing helptext from generating but still asking for generated code to display the helptext. In this case the helptext must be supplied by the user*

### Supported types for argument properties
All types are parsed with their respective .Parse method from the BCL.
```C#
int
double
float
long
short
decimal
byte
sbyte
char
string
uint
ulong
ushort
bool
Guid
Uri
TimeSpan
DateTime
```

Custom enum types are also supported. When used in combination with a Flag, the amount of times the flag is repeated (int) will be casted as the enum type.
For Positionals and Options, the supplied string value will be used in a TryParse construction.

## Command trees
Instead of one flat set of arguments, an application can have commands, in the form `appname subcommand action`:
```
myFileManagingTool directories list --showhidden
myFileManagingTool directories create photos
myFileManagingTool --root /tmp files delete old.txt --force
```
Such an application is declared as a tree of classes. The generator adds the code that hands the arguments to the right command, a help text for every level, and a list of the commands, which a program can use to describe them elsewhere.

### Declaring a tree
A tree has three kinds of classes. The diagnostics and the rest of this section call them root, group and leaf:
- The **root** is the application (`myFileManagingTool`), marked with `[CommandApp(name, description)]`. The name is used in the help text.
- A **group** is a subcommand that holds other commands (`directories`), marked with `[Command(name, description)]`.
- A **leaf** is the action that does the work (`list`), marked with `[Command(name, description)]` and `[ParameterCollection]`. Its arguments are declared like those of any other parameter collection.

The commands below the root or a group are its properties whose type is a class with `[Command]`. The generated code sets them, so declare them as `{ get; private set; }`. Groups can hold groups, and the root can hold leaves directly.

```C#
using ArgumentParser;

[CommandApp("myFileManagingTool", "Manage files and directories")]
public partial class FileTool
{
	[Option("r", "root", "Directory to work in")]
	public string Root { get; set; } = ".";

	public DirectoriesCommand? Directories { get; private set; }
	public FilesCommand? Files { get; private set; }
}

[Command("directories", "Work with directories")]
public partial class DirectoriesCommand
{
	public ListDirectories? List { get; private set; }
	public CreateDirectory? Create { get; private set; }
}

[Command("files", "Work with files")]
public partial class FilesCommand
{
	public DeleteFile? Delete { get; private set; }
}

[ParameterCollection(Annotations = CommandAnnotations.ReadOnly)]
[Command("list", "List the directories")]
public partial class ListDirectories
{
	[Flag("", "showhidden", "Include hidden directories")]
	public bool ShowHidden { get; set; }
}

[ParameterCollection(Annotations = CommandAnnotations.Idempotent)]
[Command("create", "Create a directory")]
public partial class CreateDirectory
{
	[Positional(0, "Name of the new directory", true)]
	public string Name { get; set; } = "";
}

[ParameterCollection(Annotations = CommandAnnotations.Destructive | CommandAnnotations.Idempotent)]
[Command("delete", "Delete a file")]
public partial class DeleteFile
{
	[Positional(0, "Path of the file", true)]
	public string FilePath { get; set; } = "";

	[Flag("f", "force", "Also delete read-only files")]
	public bool Force { get; set; }
}
```

The generator reports an error when the tree breaks one of these rules (see [the rules](#rules-of-a-tree) below):
- The root and groups can have `[Option]` and `[Flag]` properties, but no `[Positional]` ones: the first argument that is not an option is the name of the next command. They cannot have `required` members either.
- A class must declare its child commands itself; properties inherited from a base class are not child commands. A command class can be the type of one child property only.
- The children of one command need different names. An option or flag cannot reuse a short or long name of a command above it, or a name of the help argument. Commands side by side may share names.
- The classes cannot be generic, abstract or static. They can be `internal`, and they can be nested in other types as long as those are `partial` too.

### Parsing
The root gets a public `Parse(string[] args)`, which returns the root and the errors, like the `Parse` of a parameter collection. Only the commands that were typed are filled in; the other child properties stay `null`. `Selected` on the root is the leaf that was parsed, and `SelectedCommand` its entry in [the list of commands](#the-list-of-commands), so a program can switch on the class of the leaf instead of following the child properties:

```C#
var (tool, errors) = FileTool.Parse(args);
switch (tool.Selected)
{
	case ListDirectories list:
		// myFileManagingTool directories list --showhidden
		ListDirectories(tool.Root, list.ShowHidden);
		break;
	case CreateDirectory create:
		CreateDirectory(tool.Root, create.Name);
		break;
	case DeleteFile delete:
		DeleteFile(tool.Root, delete.FilePath, delete.Force);
		break;
}
Console.WriteLine($"Ran {tool.SelectedCommand}"); // Ran directories list
```

The child properties are still set as before, so the options of a group are read from the group, such as `tool.Files`. `Selected` and `SelectedCommand` are `null` when no leaf was reached, for instance when the command is missing or unknown and `behaviourOnError` does not exit. A leaf that was named is selected even when its arguments have errors.

The options of a level come before the name of the next command. `myFileManagingTool --root /tmp directories list` works. In `myFileManagingTool directories list --root /tmp`, though, `--root` reaches `list`, which does not know it, so it reports an `UnexpectedArgumentException`. A leaf is parsed by its own generated parser, on the arguments after its name. It also keeps its own `Parse`, which parses only its own arguments.

### Help and errors
`[CommandApp]` takes the same help and error settings as `[ParameterCollection]`, with the same defaults, and they apply to the whole tree:
```C#
[CommandApp("myFileManagingTool", "Manage files and directories",
	HelpTextGeneration.GenerateAll,
	helpArgumentShortName: "h",
	helpArgumentLongName: "Help",
	behaviourOnError: BehaviourOnError.DisplayHelpAndExit)]
```
The help text of every level is generated, so only these help modes can be used:

| HelpTextGeneration | Result |
|---|---|
| `GenerateAll` (default) | Every level has a help text, and the help argument can be given on every level. |
| `GenerateTextOnly` | Every level has a help text, but there is no help argument. |
| `None` | No help text and no help argument. It cannot be combined with `DisplayHelpAndExit`, as there is no help to display. |

The help argument shows the help of the level where it appears, then the program exits with code 0. `myFileManagingTool -h` and `myFileManagingTool -h directories list` show the help of the application:
```
myFileManagingTool - Manage files and directories

Usage:
	myFileManagingTool [options] <command> [command args]

Commands:
	directories: Work with directories
	files: Work with files

Options:
	Option: -r | --root : Directory to work in
	Flag: -h | --Help : Display help text
```
`myFileManagingTool directories list -h` shows the help of `list`. It has the full path, the arguments of `list`, and the options of the levels above it, with the command they have to come before:
```
myFileManagingTool directories list - List the directories

Usage:
	myFileManagingTool [myFileManagingTool options] directories list [optional args] (required args)

Required arguments:
	None

Optional arguments:
	Flag: --showhidden : Include hidden directories
	Flag: -h | --Help : Display help text

Options of myFileManagingTool, before 'directories':
	Option: -r | --root : Directory to work in
```

When parsing fails, `behaviourOnError` decides what happens, as described in [Behaviour on error](#behaviour-on-error). With `DisplayHelpAndExit` the errors are followed by the help of the deepest level that was reached:
- `myFileManagingTool directories` reports a `MissingCommandException` (`Missing command. Expected one of: list, create.`) and shows the help of `directories`.
- `myFileManagingTool directories remove` reports an `UnknownCommandException` (`Unknown command 'remove'. Expected one of: list, create.`) and shows the same help.
- An error in the arguments of `list` shows the help of `list`.

The help text of every level is also a public constant, `CommandHelpText`, on its class. The `HelpText` of a leaf stays the help text of its own `Parse`. The settings of a leaf's `[ParameterCollection]` also only apply to that `Parse`. The tree ignores them, and the generator warns when a leaf in a tree sets them (ARG029).

### The list of commands
The root also gets a static `Commands` list, with a `CommandInfo` for every leaf:

| Member | Content |
|---|---|
| `Path` | The words that select the command, such as `["directories", "list"]`. `ToString()` joins them with spaces. |
| `Description` | The description of the command. |
| `Annotations` | How the command behaves, see [Annotations](#annotations). |
| `HelpText` | The help text of the command, as the tree shows it (empty with `HelpTextGeneration.None`). |
| `Parse(string[] args)` | Parses the arguments that come after the path into the class of the leaf, and returns it with the errors. It does not include the options of the levels above, and it never prints, exits or throws because of the arguments. The help argument is accepted but does nothing; show `HelpText` instead. |

With it, the commands can be offered somewhere else without listing them by hand, for example as tools for an AI agent:
```C#
foreach (var command in FileTool.Commands)
{
	var toolName = string.Join("_", command.Path); // directories_list
	RegisterTool(toolName, command.Description, command.Annotations);
}

var list = FileTool.Commands.Single(c => c.ToString() == "directories list");
var (result, errors) = list.Parse(["--showhidden"]); // result is a ListDirectories
```

### Annotations
The `Annotations` property of the ParameterCollection attribute describes how the command behaves, for programs that want to know before running it, such as an MCP server that offers the commands as tools. It does not change parsing or the help text. The values are flags of `CommandAnnotations` and match the hints of an MCP tool:

| Flag | MCP hint | Meaning when set |
|---|---|---|
| `ReadOnly` | `readOnlyHint` | The command does not change anything. MCP ignores `Destructive` and `Idempotent` when it is set. |
| `Destructive` | `destructiveHint` | The command may delete or overwrite data, instead of only adding to it. |
| `Idempotent` | `idempotentHint` | Running the command again with the same arguments has no further effect. |
| `OpenWorld` | `openWorldHint` | The command interacts with things outside of the application. |

A flag that is set states that its hint is true and a flag that is not set states that it is false. Without the property, the annotations are the MCP defaults, `Destructive | OpenWorld`. A value that is given replaces the default as a whole, so keep `OpenWorld` when it still applies:
```C#
[ParameterCollection(Annotations = CommandAnnotations.ReadOnly | CommandAnnotations.OpenWorld)]
```
In a tree of commands, every leaf hands its annotations on to its entry in the `Commands` list (`CommandInfo.Annotations`).

### Generated members
Besides `Parse`, `Commands`, `Selected` and `SelectedCommand` on the root, the generator adds `ParseCommand`, `CommandHelpText` and `CommandHelpArgument` to every class of the tree, and `commandFlags` to every leaf. Do not declare members with these names in those classes; there is no diagnostic for it yet, the code just does not compile.

A tree is only generated when it has no errors, including the errors of its leaves: fix the reported problems first.

### Rules of a tree
| Rule | Problem |
|---|---|
| [ARG014](ArgumentParser.Analyzer/docs/rules/ARG014.md) | A command name is empty, contains whitespace or starts with `-`. |
| [ARG015](ArgumentParser.Analyzer/docs/rules/ARG015.md) | A group or the root has positional arguments. |
| [ARG016](ArgumentParser.Analyzer/docs/rules/ARG016.md) | `CommandApp` is combined with `Command` or `ParameterCollection`. |
| [ARG017](ArgumentParser.Analyzer/docs/rules/ARG017.md) | Two children of one command have the same name. |
| [ARG018](ArgumentParser.Analyzer/docs/rules/ARG018.md) | A command class is the type of more than one child property, or the root is a child. |
| [ARG019](ArgumentParser.Analyzer/docs/rules/ARG019.md) (warning) | A `Command` class is not reached from any `CommandApp` class. |
| [ARG020](ArgumentParser.Analyzer/docs/rules/ARG020.md) | A group or the root has no child commands. |
| [ARG021](ArgumentParser.Analyzer/docs/rules/ARG021.md) | A leaf has child commands. |
| [ARG022](ArgumentParser.Analyzer/docs/rules/ARG022.md) | A child command property has no setter the generated code can call. |
| [ARG023](ArgumentParser.Analyzer/docs/rules/ARG023.md) | A class of the tree is generic, or nested in a generic type. |
| [ARG024](ArgumentParser.Analyzer/docs/rules/ARG024.md) | A class of the tree is abstract or static. |
| [ARG025](ArgumentParser.Analyzer/docs/rules/ARG025.md) | The root or a group has `required` members. |
| [ARG026](ArgumentParser.Analyzer/docs/rules/ARG026.md) | An `Option`, `Flag` or `Positional` property has a command class as its type. |
| [ARG027](ArgumentParser.Analyzer/docs/rules/ARG027.md) | An option or flag uses a name of a command above it, or of the help argument. |
| [ARG028](ArgumentParser.Analyzer/docs/rules/ARG028.md) | `CommandApp` uses a help mode a tree cannot use, or `None` with `DisplayHelpAndExit`. |
| [ARG029](ArgumentParser.Analyzer/docs/rules/ARG029.md) (warning) | A leaf in a tree sets help or error settings that the tree does not use. |

## Inspecting the generated code

Simply add the following properties to your .csproj file:
*(The outputPath can be customized to your liking)*
```
  <PropertyGroup>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)$(TargetFramework)/Generated</CompilerGeneratedFilesOutputPath>
  </PropertyGroup>
```

In the example case given above, something similar to the following code will be generated.
The generated file declares its own `using System;`, `using System.Collections.Generic;` and `using System.Linq;`, and is otherwise simple enough to play nice with just about any target framework and user code (it does not rely on `ImplicitUsings`).

*ExampleConsole/obj/net9.0/Generated/ArgumentParser/ArgumentParser.Internal.ParserGenerator/MyCommandLineArguments_Parser.g.cs*
```C#
// <auto-generated/>
using System;
using System.Collections.Generic;
using System.Linq;
namespace ExampleConsole
{
    [System.CodeDom.Compiler.GeneratedCodeAttribute("ArgumentParser", "1.2.0")]
    public partial class MyCommandLineArguments
    {
        private static readonly ArgumentParser.OptionAttribute[] options = new ArgumentParser.OptionAttribute[] {
            new ArgumentParser.OptionAttribute("", "Target", ""),
            new ArgumentParser.OptionAttribute("t", "TimeStamp", "The timestamp to use"),
            new ArgumentParser.OptionAttribute("", "Level", "A level option with a long name"),
        };
        private static readonly ArgumentParser.PositionalAttribute[] positionals = new ArgumentParser.PositionalAttribute[] {
            new ArgumentParser.PositionalAttribute(0, "the amount of times to repeat"),
        };
        private static readonly ArgumentParser.FlagAttribute[] flags = new ArgumentParser.FlagAttribute[] {
            new ArgumentParser.FlagAttribute("h", "Help", "Display help text"),
            new ArgumentParser.FlagAttribute("v", "Verbose", "Enable verbose output"),
            new ArgumentParser.FlagAttribute("l", "", "A level"),
        };
        public const string HelpText = """
ExampleConsole

Example usage: 
	ExampleConsole [optional args] (required args)

Required arguments: 
	Option: --Target : 

Optional arguments: 
	Position: 0: the amount of times to repeat
	Flag: -h | --Help : Display help text
	Flag: -v | --Verbose : Enable verbose output
	Flag: -l : A level
	Option: -t | --TimeStamp : The timestamp to use
	Option: --Level : A level option with a long name

""";

        public bool DisplayHelp { get; set; } = false;
        
        internal static (MyCommandLineArguments result, List<ArgumentParser.ArgumentParserException> errors, List<ArgumentParser.MissingRequiredArgumentException> missingRequired) ParseCore(string[] args)
        {
            var tokenizer = new ArgumentParser.ArgumentTokenizer();
            var (tokens, errors) = tokenizer.TokenizeArguments(args, options, positionals, flags);
            var instance = new MyCommandLineArguments();
            var requiredProperties = new Dictionary<string, bool>() {
                { " | Target", false },
            };
            foreach (var token in tokens)
            {
                switch (token)
                {
                    case ArgumentParser.OptionToken optionToken:
                        if (optionToken.Name == "" || optionToken.Name == "Target")
                        {
                            instance.Output = optionToken.Value;
                            requiredProperties[" | Target"] = true;
                        }
                        if (optionToken.Name == "t" || optionToken.Name == "TimeStamp")
                        {
                            if (!DateTime.TryParse(optionToken.Value, out var parsedValue))
                            {
                                errors.Add(new ArgumentParser.InvalidArgumentValueException($"Invalid value for TimeStamp: { optionToken.Value }"));
                            }
                            else
                            {
                                instance.TimeStamp = parsedValue;
                            }
                        }
                        if (optionToken.Name == "" || optionToken.Name == "Level")
                        {
                            if (!Enum.TryParse<ExampleConsole.MyVeryOwnEnum>(optionToken.Value, out var parsedValue))
                            {
                                errors.Add(new ArgumentParser.InvalidArgumentValueException($"Invalid value for LevelOption: { optionToken.Value }"));
                            }
                            else
                            {
                                instance.LevelOption = parsedValue;
                            }
                        }
                        break;
                    case ArgumentParser.PositionalToken positionalToken:
                        if (positionalToken.Position == 0)
                        {
                            if (!int.TryParse(positionalToken.Value, out var parsedValue))
                            {
                                errors.Add(new ArgumentParser.InvalidArgumentValueException($"Invalid value for RepeatTimes: { positionalToken.Value }"));
                            }
                            else
                            {
                                instance.RepeatTimes = parsedValue;
                            }
                        }
                        break;
                    case ArgumentParser.FlagToken flagToken:
                        if (flagToken.Name == "h" || flagToken.Name == "Help")
                        {
                            instance.DisplayHelp = true;
                        }
                        if (flagToken.Name == "v" || flagToken.Name == "Verbose")
                        {
                            instance.Verbose = true;
                        }
                        if (flagToken.Name == "l" || flagToken.Name == "")
                        {
                            instance.Level = (ExampleConsole.MyVeryOwnEnum)flagToken.Level;
                        }
                        break;
                    default:
                        errors.Add(new ArgumentParser.InvalidTokenTypeException($"Unknown token type: {token.GetType().Name}"));
                        break;
                }
            }
            var missingRequired = requiredProperties
                .Where(kvp => !kvp.Value).Select(kvp => kvp.Key)
                .Select(k => new ArgumentParser.MissingRequiredArgumentException($"Missing required argument: {k}"))
                .ToList();
            errors.AddRange(missingRequired);
            return (instance, errors, missingRequired);
        }

        public static (MyCommandLineArguments result, List<ArgumentParser.ArgumentParserException> errors) Parse(string[] args)
        {
            var (instance, errors, missingRequired) = ParseCore(args);
            ArgumentParser.ParseOutcomePolicy.Apply(ArgumentParser.BehaviourOnError.DisplayHelpAndExit, instance.DisplayHelp, MyCommandLineArguments.HelpText, errors, missingRequired);
            return (instance, errors);
        }
    }
}
```

## Default behaviour
The string[] parameter is tokenized and then the tokens are evaluated in a loop to determine which properties on the class to change. During tokenization, errors are collected when something doesn't tokenize correctly or when arguments are encountered that have no corresponding property in the class.

Parameters marked as required are tracked and checked off when found. At the end of the parse, if any required properties are missing an Exception is added to the error list.

This work is done by a generated `internal static ParseCore(string[] args)` method, which only collects errors: it never prints, exits or throws because of them. `Parse` calls it and then hands the outcome to `ArgumentParser.ParseOutcomePolicy.Apply`, which applies the help and error behaviour described below. That method is public, and an overload takes the output writer and the exit action, so code that must not end the process (a server, a test) can apply the same policy with its own.

By default, if any error is encountered during the parsing, the help text is displayed and the program exits via a call to Environment.Exit(2)
Also by default, if the help argument is specified on the command line, the help text is displayed and the program exits via a call to Environment.Exit(0)

### Behaviour on error
What happens when an error is encountered is set with the `behaviourOnError` argument of the ParameterCollection attribute:

| BehaviourOnError | Result |
|---|---|
| `DisplayHelpAndExit` (default) | Every error is printed, followed by the help text, and the program exits with code 2. `Parse` never returns. |
| `ThrowNever` | Nothing is thrown and the program does not exit. `Parse` returns the instance together with the list of errors, so the caller decides what to do. |
| `ThrowIfMissingRequired` | An `AggregateException` is thrown if a required argument is missing. Other errors (invalid values, unknown arguments) are only added to the returned list. |
| `ThrowIfAnyError` | An `AggregateException` is thrown if there is any error at all, including missing required arguments. |

The thrown exception is always a `System.AggregateException`, never an `ArgumentParserException` itself. The individual problems are its `InnerExceptions`, which are the `ArgumentParserException` subclasses that otherwise end up in the returned list (`MissingRequiredArgumentException`, `InvalidArgumentValueException`, `UnexpectedArgumentException`, ...):
```C#
try
{
	var (myArgs, _) = MyCommandLineArguments.Parse(args);
}
catch (AggregateException ex)
{
	foreach (var inner in ex.InnerExceptions)
	{
		Console.WriteLine(inner.Message);
	}
}
```
Independent of `behaviourOnError`, `HelpTextGeneration.GenerateAll` and `GenerateArgumentAndHandler` generate a handler that prints the help text and exits with code 0 when the help argument is given.

## Diagnostics and Errors

see [AnalyzerReleases.Shipped.md](ArgumentParser.Analyzer/AnalyzerReleases.Shipped.md) for the released rules, [AnalyzerReleases.UnShipped.md](ArgumentParser.Analyzer/AnalyzerReleases.UnShipped.md) for the rules that are not released yet, and [docs/rules](ArgumentParser.Analyzer/docs/rules) for a page per rule.

## Known bugs (past and present)
v1.1.1
	-Specifying named arguments to ParameterCollectionAttribute constructor doesn't work. They are treated as positional arguments

v1.1.0
	-DateTime, TimeStamp, Guid, and Uri types not working

v1.0.0:
	- AggregateException is always thrown when any parse error is encountered

# Development
The project comes with a makefile that should cover most typical needs during the development cycle.

## building and testing locally
run ```make build-analyzer``` to generate the nuget package locally
run ```make build-example``` to publish the example project as a native AOT executable (needs clang and zlib, as any NativeAOT publish does)
run ```make test``` and ```make smoke-test``` to run unit testing and smoke testing, respectively. Smoke testing runs the native AOT executable

## creating a release
For a release, the version of the final nuget package, the version specified in the GeneratedCodeAttribute, and the git tag should all match.

The easiest way to do this is to install dotnet-gitversion and run ```make set-pre-release-version``` or ```make set-version-stable```.
The first creates a pre-release tag and updates the corresponding files in the project. The second creates a major.minor.patch release tag and also updates files. Both targets amend the latest commit to include these changes in the source tree.

```make pre-release``` and ```make release-stable``` respectively create a prerelease and a stable release, along with the required git tag.
Stable releases should only be created from the main branch.
