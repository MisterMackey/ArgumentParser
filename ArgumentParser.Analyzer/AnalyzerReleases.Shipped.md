## Release 1.3.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ARG001 | ClassDeclaration | Error | ParameterCollection must be partial
ARG002 | ClassDeclaration | Error | Parse method already exists
ARG003 | AttributeDeclaration | Error | Duplicate argument name
ARG004 | AttributeDeclaration | Error | Duplicate positional argument position
ARG005 | AttributeDeclaration | Error | Invalid positional argument position
ARG006 | ClassDeclaration | Error | Constructor with no parameters required
ARG007 | AttributeDeclaration | Error | Specified property type is not supported.
ARG008 | GenerationOptions | Warning | Helptext argument names specified but argument generation disabled.
ARG009 | GenerationOptions | Error | HelpText property is missing but argument handler is generated.
ARG010 | GenerationOptions | Error | HelpText property is missing but display help on error handler is generated.
ARG011 | GenerationOptions | Error | DisplayHelp property specified but generator is supplying it.
ARG012 | GenerationOptions | Error | HelpText const string specified but generator is supplying it.
ARG013 | AttributeDeclaration | Error | Flag attributes should only be applied to boolean or enum properties.
ARG014 | CommandDeclaration | Error | Invalid command name
ARG015 | CommandDeclaration | Error | Positional argument on a class with child commands
ARG016 | CommandDeclaration | Error | Conflicting command attributes
ARG017 | CommandDeclaration | Error | Duplicate command name
ARG018 | CommandDeclaration | Error | Command class used more than once
ARG019 | CommandDeclaration | Warning | Command class is not part of a tree
ARG020 | CommandDeclaration | Error | Command group without commands
ARG021 | CommandDeclaration | Error | Leaf command with child commands
ARG022 | CommandDeclaration | Error | Child command property cannot be set
ARG023 | CommandDeclaration | Error | Generic command class
ARG024 | CommandDeclaration | Error | Abstract command class
ARG025 | CommandDeclaration | Error | Required member on a command group
ARG026 | CommandDeclaration | Error | Argument of a command class type
ARG027 | CommandDeclaration | Error | Argument name used by a parent command
ARG028 | CommandDeclaration | Error | Help setting not supported by a command tree
ARG029 | CommandDeclaration | Warning | Help, error, culture and tool settings of a leaf are not used by its command tree
ARG030 | AttributeDeclaration | Error | Parse method not found
ARG031 | AttributeDeclaration | Error | A list positional must be the last positional
ARG032 | AttributeDeclaration | Error | The elements of a list cannot be nullable value types
ARG033 | AttributeDeclaration | Error | A required argument cannot be excluded from the manifest
ARG034 | AttributeDeclaration | Error | Two arguments have the same key in a JSON Schema
ARG035 | GenerationOptions | Error | The JSON binder needs System.Text.Json