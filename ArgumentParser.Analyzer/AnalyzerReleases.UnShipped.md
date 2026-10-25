## Release 2.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
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
ARG029 | CommandDeclaration | Warning | Help and error settings of a leaf are not used by its command tree
