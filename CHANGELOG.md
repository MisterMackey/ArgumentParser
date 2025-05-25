# This document will track changes starting in v1.1.0

## UnReleased changes

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
