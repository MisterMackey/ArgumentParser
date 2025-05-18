// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Maintainability", "CA1510:Use ArgumentNullException throw helper", Justification = "Incompatible with netstandard2.0 target", Scope = "member", Target = "~M:ArgumentParser.ArgumentTokenizer.TokenizeArguments(System.String[],ArgumentParser.OptionAttribute[],ArgumentParser.PositionalAttribute[],ArgumentParser.FlagAttribute[])~System.ValueTuple{System.Collections.Generic.List{ArgumentParser.Token},System.Collections.Generic.List{ArgumentParser.ArgumentParserException}}")]
[assembly: SuppressMessage("Style", "IDE0057:Use range operator", Justification = "Incompatible with netstandard2.0 target", Scope = "member", Target = "~M:ArgumentParser.ArgumentTokenizer.ProcessLongNameArgument(System.String,System.String[],System.Int32@,ArgumentParser.OptionAttribute[],ArgumentParser.FlagAttribute[],System.Collections.Generic.List{ArgumentParser.Token},System.Collections.Generic.List{ArgumentParser.ArgumentParserException})")]
[assembly: SuppressMessage("Style", "IDE0057:Use range operator", Justification = "Incompatible with netstandard2.0 target", Scope = "member", Target = "~M:ArgumentParser.ArgumentTokenizer.ProcessShortNameArguments(System.String,System.String[],System.Int32@,ArgumentParser.OptionAttribute[],ArgumentParser.FlagAttribute[],System.Collections.Generic.List{ArgumentParser.Token},System.Collections.Generic.List{ArgumentParser.ArgumentParserException})")]
[assembly: SuppressMessage("Performance", "CA1865:Use char overload", Justification = "Incompatible with netstandard2.0 target", Scope = "member", Target = "~M:ArgumentParser.ArgumentTokenizer.TokenizeArguments(System.String[],ArgumentParser.OptionAttribute[],ArgumentParser.PositionalAttribute[],ArgumentParser.FlagAttribute[])~System.ValueTuple{System.Collections.Generic.List{ArgumentParser.Token},System.Collections.Generic.List{ArgumentParser.ArgumentParserException}}")]
