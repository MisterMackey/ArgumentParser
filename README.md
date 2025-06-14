# ArgumentParser
Licensed under the Apache License, Version 2.0

## Table of contents
- [ArgumentParser](#argumentparser)
	- [Table of contents](#table-of-contents)
	- [About](#about)
	- [Quickstart](#quickstart)
		- [Overriding the default behaviour](#overriding-the-default-behaviour)
		- [Supported types for argument properties](#supported-types-for-argument-properties)
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
Since this code is essentially peanuts and free of any using directives it should play nice with just about any target framework and user code.

*ExampleConsole/obj/net9.0/Generated/ArgumentParser/ArgumentParser.Internal.ParserGenerator/MyCommandLineArguments_Parser.g.cs*
```C#
// <auto-generated/>
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
        private static readonly Dictionary<string,bool> requiredProperties = new Dictionary<string, bool>() {
            { " | Target", false },
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
        
        public static (MyCommandLineArguments result, List<ArgumentParser.ArgumentParserException> errors) Parse(string[] args)
        {
            var tokenizer = new ArgumentParser.ArgumentTokenizer();
            var (tokens, errors) = tokenizer.TokenizeArguments(args, options, positionals, flags);
            var instance = new MyCommandLineArguments();
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
            if (instance.DisplayHelp)
            {
                System.Console.WriteLine(MyCommandLineArguments.HelpText);
                Environment.Exit(0);
            }
            if (errors.Any())
            {
                foreach (var error in errors)
                {
                    System.Console.WriteLine(error);
                }
                System.Console.WriteLine();
                System.Console.WriteLine(MyCommandLineArguments.HelpText);
                Environment.Exit(2);
            }
            return (instance, errors);
        }
    }
}
```

## Default behaviour
The string[] parameter is tokenized and then the tokens are evaluated in a loop to determine which properties on the class to change. During tokenization, errors are collected when something doesn't tokenize correctly or when arguments are encountered that have no corresponding property in the class.

Parameters marked as required are tracked and checked off when found. At the end of the parse method, if any required properties are missing an Exception is added to the error list.

By default, if any error is encountered during the parsing, the help text is displayed and the program exits via a call to Environment.Exit(2)
Also by default, if the help argument is specified on the command line, the help text is displayed and the program exits via a call to Environment.Exit(0)

## Diagnostics and Errors

see [AnalyzerRelease.Shipped.md](ArgumentParser/AnalyzerReleases.Shipped.md)

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
run ```make build-example``` to build the example project
run ```make test``` and ```make smoke-test``` to run unit testing and smoke testing, respectively. Smoke testing runs the example project

## creating a release
For a release, the version of the final nuget package, the version specified in the GeneratedCodeAttribute, and the git tag should all match.

The easiest way to do this is to install dotnet-gitversion and run ```make set-pre-release-version``` or ```make set-version-stable```.
The first creates a pre-release tag and updates the corresponding files in the project. The second creates a major.minor.patch release tag and also updates files. Both targets amend the latest commit to include these changes in the source tree.

```make pre-release``` and ```make release-stable``` respectively create a prerelease and a stable release, along with the required git tag.
Stable releases should only be created from the main branch.
