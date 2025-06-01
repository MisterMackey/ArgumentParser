## Release 1.1.1

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
ARG013 | AttributeDeclaration | Error | Flag attributes should only be applied to boolean properties.