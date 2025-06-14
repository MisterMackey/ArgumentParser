# This document will track changes starting in v1.1.0

## UnReleased / Unplanned changes
- Quick action code fix support
- Overriding program names in generated helptext

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
