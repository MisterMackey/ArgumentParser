using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using ArgumentParser.Internal.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArgumentParser.Internal;

/// <summary>
/// Generates source code text for argument parsing methods based on class declaration and attribute information.
/// </summary>
public class SourceTextGenerator : ISourceTextGenerator
{
	private readonly string ClassName;
	private readonly string? NamespaceName;
	private readonly EquatableArray<ContainingType> ContainingTypes;
	private readonly ReadOnlyCollection<PropertyAndAttributeInfo> Options;
	private readonly ReadOnlyCollection<PropertyAndAttributeInfo> Positionals;
	private readonly ReadOnlyCollection<PropertyAndAttributeInfo> Flags;
	private readonly HelptextProvider HelptextProvider;
	private readonly GeneratorConfiguration Config;
	private readonly bool IsCommand;

	/// <summary>
	/// The table of the flags of a leaf of a tree of commands without the leaf's own help argument.
	/// The part a tree generates for the leaf passes it to <c>ParseCore</c>, together with the help argument of the tree.
	/// </summary>
	internal const string CommandFlagsName = "commandFlags";

	/// <summary>
	/// Initializes a new instance of the <see cref="SourceTextGenerator"/> class.
	/// </summary>
	/// <param name="className">The name of the class to generate parsing code for.</param>
	/// <param name="namespaceName">The namespace of the class, or null if it has none.</param>
	/// <param name="argumentProvider">The argument provider to retrieve argument information from.</param>
	/// <param name="helptextProvider">The help text provider to generate help text if needed.</param>
	/// <param name="config">The configuration settings for code generation and help text.</param>
	/// <param name="containingTypes">The types the class is nested in, the outermost first.</param>
	/// <param name="isCommand">Whether the class is a leaf of a tree of commands, which parses it with the help argument of the tree.</param>
	public SourceTextGenerator(
		string className,
		string? namespaceName,
		ArgumentProvider argumentProvider,
		HelptextProvider helptextProvider,
		GeneratorConfiguration config,
		EquatableArray<ContainingType> containingTypes = default,
		bool isCommand = false
	)
	{
		if (argumentProvider == null)
		{
			throw new ArgumentNullException(nameof(argumentProvider));
		}
		ClassName = className ?? throw new ArgumentNullException(nameof(className));
		NamespaceName = namespaceName;
		ContainingTypes = containingTypes;
		Options = argumentProvider.GetOptionArguments();
		Positionals = argumentProvider.GetPositionalArguments();
		Flags = argumentProvider.GetFlagArguments();
		HelptextProvider = helptextProvider ?? throw new ArgumentNullException(nameof(helptextProvider));
		Config = config ?? throw new ArgumentNullException(nameof(config));
		IsCommand = isCommand;
	}

	/// <summary>
	/// Generates the source text for the argument parsing method.
	/// </summary>
	/// <returns>A string containing the generated source code for the parser.</returns>
	public string GenerateSourceText()
	{
		var className = ClassName;
		var namespaceName = NamespaceName;

		using var writer = new IndentedTextWriter(new StringWriter());

		// file header, namespace, class decl
		WriteClassStart(writer, namespaceName, ContainingTypes, className);

		// static options, flags and positionals lists
		WriteArgumentTables(writer, Options, Positionals, Flags);

		// help text field (provider may return empty string if no help text is needed)
		if (Config.HelpTextShouldBeGenerated())
		{
			var helpText = HelptextProvider.GenerateHelpText();
			writer.Write(helpText);
			writer.WriteLine();
		}

		// add DisplayHelp property if needed
		if (Config.HelpArgumentShouldBeGenerated())
		{
			writer.WriteLine("public bool DisplayHelp { get; set; } = false;");
			writer.WriteLine();
		}

		// ParseCore binds the arguments and collects errors, it never prints, exits or throws
		writer.WriteLine($"internal static ({className} result, List<global::ArgumentParser.ArgumentParserException> errors, List<global::ArgumentParser.MissingRequiredArgumentException> missingRequired) ParseCore(string[] args)");
		writer.WriteLine("{");
		writer.Indent++;
		if (IsCommand)
		{
			// the body is shared with the tree, which parses the leaf with other flags
			writer.WriteLine("var (instance, errors, missingRequired, _) = ParseCore(args, flags, null);");
		}
		else
		{
			WriteParseCoreBody(writer, className, "flags");
		}
		writer.WriteLine("return (instance, errors, missingRequired);");
		writer.Indent--;
		writer.WriteLine("}");
		writer.WriteLine();

		// Parse method: ParseCore plus what the configuration asks to happen with the outcome
		writer.WriteLine($"public static ({className} result, List<global::ArgumentParser.ArgumentParserException> errors) Parse(string[] args)");
		writer.WriteLine("{");
		writer.Indent++;
		var displayHelpOnRequest = Config.HelpTextShouldDisplayOnRequest();
		var behaviourOnError = GetEffectiveBehaviourOnError();
		if (displayHelpOnRequest || behaviourOnError != "ThrowNever")
		{
			var helpTextArgument = displayHelpOnRequest || Config.HelpTextShouldDisplayOnError() ? $"{className}.HelpText" : "null";
			writer.WriteLine("var (instance, errors, missingRequired) = ParseCore(args);");
			writer.WriteLine($"global::ArgumentParser.ParseOutcomePolicy.Apply(global::ArgumentParser.BehaviourOnError.{behaviourOnError}, {(displayHelpOnRequest ? "instance.DisplayHelp" : "false")}, {helpTextArgument}, errors, missingRequired);");
		}
		else
		{
			writer.WriteLine("var (instance, errors, _) = ParseCore(args);");
		}
		writer.WriteLine("return (instance, errors);");
		writer.Indent--;
		writer.WriteLine("}");
		if (IsCommand)
		{
			writer.WriteLine();
			WriteCommandParseCore(writer, className);
		}
		WriteClassEnd(writer, namespaceName, ContainingTypes);
		writer.Flush();
		var text = writer.InnerWriter.ToString();
		writer.Close();
		return text;
	}

	/// <summary>
	/// Writes the usings, the namespace, the types the class is nested in and the start of the partial class
	/// that the generated members go into. The GeneratedCode attribute can only be on one part of a class,
	/// so a second generated part of the same class (the one a tree of commands adds to a leaf) leaves it out.
	/// </summary>
	internal static void WriteClassStart(IndentedTextWriter writer, string? namespaceName, EquatableArray<ContainingType> containingTypes, string className, bool markAsGenerated = true)
	{
		writer.WriteLine("// <auto-generated/>");
		writer.WriteLine("using System;");
		writer.WriteLine("using System.Collections.Generic;");
		writer.WriteLine("using System.Linq;");
		if (namespaceName is not null)
		{
			writer.WriteLine($"namespace {namespaceName}");
			writer.WriteLine("{");
			writer.Indent++;
		}
		// a class nested in another is a different class than a top level one with the same name, so the outer types are declared again
		for (int i = 0; i < containingTypes.Count; i++)
		{
			writer.WriteLine($"partial {containingTypes[i].Keyword} {containingTypes[i].Name}");
			writer.WriteLine("{");
			writer.Indent++;
		}
		if (markAsGenerated)
		{
			writer.WriteLine($"[System.CodeDom.Compiler.GeneratedCodeAttribute(\"ArgumentParser\", \"{AssemblyVersionProvider.FullSemVer}\")]");
		}
		// no access modifier: the part takes the one the user wrote, which may be internal
		writer.WriteLine($"partial class {className}");
		writer.WriteLine("{");
		writer.Indent++;
	}

	/// <summary>
	/// Writes the end of the class, the types it is nested in and the namespace that <see cref="WriteClassStart"/> opened.
	/// </summary>
	internal static void WriteClassEnd(IndentedTextWriter writer, string? namespaceName, EquatableArray<ContainingType> containingTypes)
	{
		writer.Indent--;
		writer.WriteLine("}");
		for (int i = 0; i < containingTypes.Count; i++)
		{
			writer.Indent--;
			writer.WriteLine("}");
		}
		if (namespaceName is not null)
		{
			writer.Indent--;
			writer.WriteLine("}");
		}
	}

	// Tokenizes args against the flags table of the given expression and binds the tokens to a new instance.
	private void WriteParseCoreBody(IndentedTextWriter writer, string className, string flagsTable, string? ownHelpCondition = null)
	{
		writer.WriteLine("var tokenizer = new global::ArgumentParser.ArgumentTokenizer();");
		writer.WriteLine($"var (tokens, errors) = tokenizer.TokenizeArguments(args, options, positionals, {flagsTable});");
		writer.WriteLine($"var instance = new {className}();");
		// required arguments are tracked per call, never in static state
		WriteRequiredProperties(writer, Options, Positionals);
		WriteTokenLoop(writer, Options, Positionals, Flags, ownHelpCondition);
		WriteMissingRequired(writer);
	}

	// A tree of commands parses a leaf with the help argument of the tree instead of the one of the leaf's own settings
	// (commandFlags leaves that one out, so DisplayHelp is never set), and needs to know whether it was given.
	private void WriteCommandParseCore(IndentedTextWriter writer, string className)
	{
		if (Config.HelpArgumentShouldBeGenerated())
		{
			WriteFlagsTable(writer, CommandFlagsName, Flags.Where(f => f.PropertyName != ArgumentProvider.HelpPropertyName), null);
		}
		else
		{
			writer.WriteLine($"private static readonly global::ArgumentParser.FlagAttribute[] {CommandFlagsName} = flags;");
		}
		writer.WriteLine();
		writer.WriteLine($"internal static ({className} result, List<global::ArgumentParser.ArgumentParserException> errors, List<global::ArgumentParser.MissingRequiredArgumentException> missingRequired, bool helpRequested) ParseCore(string[] args, global::ArgumentParser.FlagAttribute[] flagsTable, global::ArgumentParser.FlagAttribute help)");
		writer.WriteLine("{");
		writer.Indent++;
		// the help argument of the tree may have the names of the leaf's own, which must then not set DisplayHelp
		WriteParseCoreBody(writer, className, "help is null ? flagsTable : flagsTable.Append(help).ToArray()", "help is null");
		writer.WriteLine("return (instance, errors, missingRequired, help is not null && global::ArgumentParser.ArgumentTokenizer.ContainsFlag(tokens, help));");
		writer.Indent--;
		writer.WriteLine("}");
	}

	/// <summary>
	/// Writes the static tables of options, positionals and flags the tokenizer matches against.
	/// A root or group has no positionals: without them the positionals table is left out.
	/// An extra flag, given as an expression (a field declared before the tables), is added to the flags without a property to set.
	/// </summary>
	internal static void WriteArgumentTables(
		IndentedTextWriter writer,
		IEnumerable<PropertyAndAttributeInfo> options,
		IEnumerable<PropertyAndAttributeInfo>? positionals,
		IEnumerable<PropertyAndAttributeInfo> flags,
		string? extraFlag = null)
	{
		writer.WriteLine("private static readonly global::ArgumentParser.OptionAttribute[] options = new global::ArgumentParser.OptionAttribute[] {");
		writer.Indent++;
		foreach (var option in options)
		{
			writer.WriteLine($"new global::ArgumentParser.OptionAttribute({Literal(option.Attribute.ShortName)}, {Literal(option.Attribute.LongName)}, {Literal(option.Attribute.Description)}),");
		}
		writer.Indent--;
		writer.WriteLine("};");

		if (positionals is not null)
		{
			writer.WriteLine("private static readonly global::ArgumentParser.PositionalAttribute[] positionals = new global::ArgumentParser.PositionalAttribute[] {");
			writer.Indent++;
			foreach (var positional in positionals)
			{
				writer.WriteLine($"new global::ArgumentParser.PositionalAttribute({positional.Attribute.Position}, {Literal(positional.Attribute.Description)}),");
			}
			writer.Indent--;
			writer.WriteLine("};");
		}

		WriteFlagsTable(writer, "flags", flags, extraFlag);
	}

	private static void WriteFlagsTable(IndentedTextWriter writer, string name, IEnumerable<PropertyAndAttributeInfo> flags, string? extraFlag)
	{
		writer.WriteLine($"private static readonly global::ArgumentParser.FlagAttribute[] {name} = new global::ArgumentParser.FlagAttribute[] {{");
		writer.Indent++;
		foreach (var flag in flags)
		{
			writer.WriteLine($"new global::ArgumentParser.FlagAttribute({Literal(flag.Attribute.ShortName)}, {Literal(flag.Attribute.LongName)}, {Literal(flag.Attribute.Description)}),");
		}
		if (extraFlag is not null)
		{
			writer.WriteLine($"{extraFlag},");
		}
		writer.Indent--;
		writer.WriteLine("};");
	}

	/// <summary>
	/// Writes the dictionary that tracks which of the required arguments were seen.
	/// </summary>
	internal static void WriteRequiredProperties(
		IndentedTextWriter writer,
		IEnumerable<PropertyAndAttributeInfo> options,
		IEnumerable<PropertyAndAttributeInfo> positionals)
	{
		writer.WriteLine("var requiredProperties = new Dictionary<string, bool>() {");
		writer.Indent++;
		foreach (var option in options)
		{
			if (option.Attribute.Required)
			{
				writer.WriteLine($"{{ {Literal(RequiredKey(option))}, false }},");
			}
		}
		foreach (var positional in positionals)
		{
			if (positional.Attribute.Required)
			{
				writer.WriteLine($"{{ {Literal(positional.PropertyName)}, false }},");
			}
		}
		writer.Indent--;
		writer.WriteLine("};");
	}

	/// <summary>
	/// Writes the loop that binds the tokens to the properties of <c>instance</c> and adds the errors it finds to <c>errors</c>.
	/// A condition for the generated help argument, when given, has to hold as well before it sets DisplayHelp.
	/// </summary>
	internal static void WriteTokenLoop(
		IndentedTextWriter writer,
		IEnumerable<PropertyAndAttributeInfo> options,
		IEnumerable<PropertyAndAttributeInfo> positionals,
		IEnumerable<PropertyAndAttributeInfo> flags,
		string? ownHelpCondition = null)
	{
		writer.WriteLine("foreach (var token in tokens)");
		writer.WriteLine("{");
		writer.Indent++;
		writer.WriteLine("switch (token)");
		writer.WriteLine("{");
		writer.Indent++;

		writer.WriteLine("case global::ArgumentParser.OptionToken optionToken:");
		writer.Indent++;
		foreach (var option in options)
		{
			writer.WriteLine($"if ({NameCondition("optionToken", option.Attribute.ShortName, option.Attribute.LongName)})");
			writer.WriteLine("{");
			writer.Indent++;
			WriteValueParseCode(option, "optionToken", option.PropertyName, writer);
			if (option.Attribute.Required)
			{
				writer.WriteLine($"requiredProperties[{Literal(RequiredKey(option))}] = true;");
			}
			writer.Indent--;
			writer.WriteLine("}");
		}
		writer.WriteLine("break;");
		writer.Indent--;

		writer.WriteLine("case global::ArgumentParser.PositionalToken positionalToken:");
		writer.Indent++;
		foreach (var positional in positionals)
		{
			writer.WriteLine($"if (positionalToken.Position == {positional.Attribute.Position})");
			writer.WriteLine("{");
			writer.Indent++;
			WriteValueParseCode(positional, "positionalToken", positional.PropertyName, writer);
			if (positional.Attribute.Required)
			{
				writer.WriteLine($"requiredProperties[{Literal(positional.PropertyName)}] = true;");
			}
			writer.Indent--;
			writer.WriteLine("}");
		}
		writer.WriteLine("break;");
		writer.Indent--;

		writer.WriteLine("case global::ArgumentParser.FlagToken flagToken:");
		writer.Indent++;
		foreach (var flag in flags)
		{
			var condition = NameCondition("flagToken", flag.Attribute.ShortName, flag.Attribute.LongName);
			if (ownHelpCondition is not null && flag.PropertyName == ArgumentProvider.HelpPropertyName)
			{
				condition = $"{ownHelpCondition} && ({condition})";
			}
			writer.WriteLine($"if ({condition})");
			writer.WriteLine("{");
			writer.Indent++;
			WriteFlagParseCode(flag, writer);
			writer.Indent--;
			writer.WriteLine("}");
		}
		writer.WriteLine("break;");
		writer.Indent--;

		writer.WriteLine("default:");
		writer.Indent++;
		writer.WriteLine("errors.Add(new global::ArgumentParser.InvalidTokenTypeException($\"Unknown token type: {token.GetType().Name}\"));");
		writer.WriteLine("break;");
		writer.Indent--;

		writer.Indent--;
		writer.WriteLine("}");
		writer.Indent--;
		writer.WriteLine("}");
	}

	/// <summary>
	/// Writes the check for required arguments that were not seen: it defines <c>missingRequired</c> and adds it to <c>errors</c>.
	/// </summary>
	internal static void WriteMissingRequired(IndentedTextWriter writer)
	{
		writer.WriteLine("var missingRequired = requiredProperties");
		writer.Indent++;
		writer.WriteLine(".Where(kvp => !kvp.Value).Select(kvp => kvp.Key)");
		writer.WriteLine(".Select(k => new global::ArgumentParser.MissingRequiredArgumentException($\"Missing required argument: {k}\"))");
		writer.WriteLine(".ToList();");
		writer.Indent--;
		writer.WriteLine("errors.AddRange(missingRequired);");
	}

	/// <summary>
	/// Gets the name of the BehaviourOnError member the generated Parse hands to the runtime policy.
	/// The order matches how the settings were applied before the policy moved to the runtime library:
	/// displaying the help text and exiting wins over throwing, and throwing for any error wins over throwing for missing arguments.
	/// </summary>
	private string GetEffectiveBehaviourOnError()
	{
		if (Config.HelpTextShouldDisplayOnError())
		{
			return "DisplayHelpAndExit";
		}
		if (Config.ShouldThrowIfAnyError())
		{
			return "ThrowIfAnyError";
		}
		if (Config.ShouldThrowIfMissingRequired())
		{
			return "ThrowIfMissingRequired";
		}
		return "ThrowNever";
	}

	/// <summary>
	/// Gets the appropriate code to parse a string value into the specified property type.
	/// </summary>
	/// <param name="propertyInfo">The info of the property to parse to.</param>
	/// <param name="localVariableName">The name of the token property to read the value from.</param>
	/// <param name="propertyName">The name of the property in the partial class to set.</param>
	/// <param name="writer">The writer to write the code to.</param>
	/// <returns>A string containing the code needed to parse the value.</returns>
	internal static void WriteValueParseCode(PropertyAndAttributeInfo propertyInfo, string localVariableName, string propertyName, IndentedTextWriter writer)
	{
		// check if its an enum and construct enum with string value in that case
		if (propertyInfo.IsEnum)
		{
			writer.WriteLine($"if (!Enum.TryParse<{propertyInfo.PropertyType}>({localVariableName}.Value, out var parsedValue))");
			writer.WriteLine("{");
			writer.Indent++;
			writer.WriteLine($"errors.Add(new global::ArgumentParser.InvalidArgumentValueException($\"Invalid value for {propertyName}: {{ {localVariableName}.Value }}\"));");
			writer.Indent--;
			writer.WriteLine("}");
			writer.WriteLine("else");
			writer.WriteLine("{");
			writer.Indent++;
			writer.WriteLine($"instance.{propertyName} = parsedValue;");
			writer.Indent--;
			writer.WriteLine("}");
			return;
		}
		// check if custom parse logic is specified
		if (propertyInfo.HasParseMethod)
		{
			var typeWithoutNullableIndicator = propertyInfo.PropertyType.Replace("?", "");
			writer.WriteLine($"if (!{typeWithoutNullableIndicator}.{propertyInfo.ParseMethodName}({localVariableName}.Value, out var parsedValue))");
			writer.WriteLine("{");
			writer.Indent++;
			writer.WriteLine($"errors.Add(new global::ArgumentParser.InvalidArgumentValueException($\"Invalid value for {propertyName}: {{ {localVariableName}.Value }}\"));");
			writer.Indent--;
			writer.WriteLine("}");
			writer.WriteLine("else");
			writer.WriteLine("{");
			writer.Indent++;
			writer.WriteLine($"instance.{propertyName} = parsedValue;");
			writer.Indent--;
			writer.WriteLine("}");
			return;
		}
		// otherwise just parse the value based on the (simple) type
		var propertyType = propertyInfo.PropertyType;
		switch (propertyType)
		{
			case "int":
				writer.WriteLine($"if (!int.TryParse({localVariableName}.Value, out var parsedValue))");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"errors.Add(new global::ArgumentParser.InvalidArgumentValueException($\"Invalid value for {propertyName}: {{ {localVariableName}.Value }}\"));");
				writer.Indent--;
				writer.WriteLine("}");
				writer.WriteLine("else");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"instance.{propertyName} = parsedValue;");
				writer.Indent--;
				writer.WriteLine("}");
				break;
			case "double":
				writer.WriteLine($"if (!double.TryParse({localVariableName}.Value, out var parsedValue))");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"errors.Add(new global::ArgumentParser.InvalidArgumentValueException($\"Invalid value for {propertyName}: {{ {localVariableName}.Value }}\"));");
				writer.Indent--;
				writer.WriteLine("}");
				writer.WriteLine("else");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"instance.{propertyName} = parsedValue;");
				writer.Indent--;
				writer.WriteLine("}");
				break;
			case "float":
				writer.WriteLine($"if (!float.TryParse({localVariableName}.Value, out var parsedValue))");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"errors.Add(new global::ArgumentParser.InvalidArgumentValueException($\"Invalid value for {propertyName}: {{ {localVariableName}.Value }}\"));");
				writer.Indent--;
				writer.WriteLine("}");
				writer.WriteLine("else");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"instance.{propertyName} = parsedValue;");
				writer.Indent--;
				writer.WriteLine("}");
				break;
			case "long":
				writer.WriteLine($"if (!long.TryParse({localVariableName}.Value, out var parsedValue))");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"errors.Add(new global::ArgumentParser.InvalidArgumentValueException($\"Invalid value for {propertyName}: {{ {localVariableName}.Value }}\"));");
				writer.Indent--;
				writer.WriteLine("}");
				writer.WriteLine("else");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"instance.{propertyName} = parsedValue;");
				writer.Indent--;
				writer.WriteLine("}");
				break;
			case "short":
				writer.WriteLine($"if (!short.TryParse({localVariableName}.Value, out var parsedValue))");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"errors.Add(new global::ArgumentParser.InvalidArgumentValueException($\"Invalid value for {propertyName}: {{ {localVariableName}.Value }}\"));");
				writer.Indent--;
				writer.WriteLine("}");
				writer.WriteLine("else");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"instance.{propertyName} = parsedValue;");
				writer.Indent--;
				writer.WriteLine("}");
				break;
			case "decimal":
				writer.WriteLine($"if (!decimal.TryParse({localVariableName}.Value, out var parsedValue))");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"errors.Add(new global::ArgumentParser.InvalidArgumentValueException($\"Invalid value for {propertyName}: {{ {localVariableName}.Value }}\"));");
				writer.Indent--;
				writer.WriteLine("}");
				writer.WriteLine("else");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"instance.{propertyName} = parsedValue;");
				writer.Indent--;
				writer.WriteLine("}");
				break;
			case "byte":
				writer.WriteLine($"if (!byte.TryParse({localVariableName}.Value, out var parsedValue))");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"errors.Add(new global::ArgumentParser.InvalidArgumentValueException($\"Invalid value for {propertyName}: {{ {localVariableName}.Value }}\"));");
				writer.Indent--;
				writer.WriteLine("}");
				writer.WriteLine("else");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"instance.{propertyName} = parsedValue;");
				writer.Indent--;
				writer.WriteLine("}");
				break;
			case "sbyte":
				writer.WriteLine($"if (!sbyte.TryParse({localVariableName}.Value, out var parsedValue))");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"errors.Add(new global::ArgumentParser.InvalidArgumentValueException($\"Invalid value for {propertyName}: {{ {localVariableName}.Value }}\"));");
				writer.Indent--;
				writer.WriteLine("}");
				writer.WriteLine("else");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"instance.{propertyName} = parsedValue;");
				writer.Indent--;
				writer.WriteLine("}");
				break;
			case "char":
				writer.WriteLine($"if (!char.TryParse({localVariableName}.Value, out var parsedValue))");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"errors.Add(new global::ArgumentParser.InvalidArgumentValueException($\"Invalid value for {propertyName}: {{ {localVariableName}.Value }}\"));");
				writer.Indent--;
				writer.WriteLine("}");
				writer.WriteLine("else");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"instance.{propertyName} = parsedValue;");
				writer.Indent--;
				writer.WriteLine("}");
				break;
			case "uint":
				writer.WriteLine($"if (!uint.TryParse({localVariableName}.Value, out var parsedValue))");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"errors.Add(new global::ArgumentParser.InvalidArgumentValueException($\"Invalid value for {propertyName}: {{ {localVariableName}.Value }}\"));");
				writer.Indent--;
				writer.WriteLine("}");
				writer.WriteLine("else");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"instance.{propertyName} = parsedValue;");
				writer.Indent--;
				writer.WriteLine("}");
				break;
			case "ulong":
				writer.WriteLine($"if (!ulong.TryParse({localVariableName}.Value, out var parsedValue))");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"errors.Add(new global::ArgumentParser.InvalidArgumentValueException($\"Invalid value for {propertyName}: {{ {localVariableName}.Value }}\"));");
				writer.Indent--;
				writer.WriteLine("}");
				writer.WriteLine("else");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"instance.{propertyName} = parsedValue;");
				writer.Indent--;
				writer.WriteLine("}");
				break;
			case "ushort":
				writer.WriteLine($"if (!ushort.TryParse({localVariableName}.Value, out var parsedValue))");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"errors.Add(new global::ArgumentParser.InvalidArgumentValueException($\"Invalid value for {propertyName}: {{ {localVariableName}.Value }}\"));");
				writer.Indent--;
				writer.WriteLine("}");
				writer.WriteLine("else");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"instance.{propertyName} = parsedValue;");
				writer.Indent--;
				writer.WriteLine("}");
				break;
			case "System.Guid":
				writer.WriteLine($"if (!Guid.TryParse({localVariableName}.Value, out var parsedValue))");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"errors.Add(new global::ArgumentParser.InvalidArgumentValueException($\"Invalid value for {propertyName}: {{ {localVariableName}.Value }}\"));");
				writer.Indent--;
				writer.WriteLine("}");
				writer.WriteLine("else");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"instance.{propertyName} = parsedValue;");
				writer.Indent--;
				writer.WriteLine("}");
				break;
			case "System.Uri":
				writer.WriteLine($"if (!Uri.TryCreate({localVariableName}.Value, UriKind.RelativeOrAbsolute, out var parsedValue))");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"errors.Add(new global::ArgumentParser.InvalidArgumentValueException($\"Invalid value for {propertyName}: {{ {localVariableName}.Value }}\"));");
				writer.Indent--;
				writer.WriteLine("}");
				writer.WriteLine("else");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"instance.{propertyName} = parsedValue;");
				writer.Indent--;
				writer.WriteLine("}");
				break;
			case "System.TimeSpan":
				writer.WriteLine($"if (!TimeSpan.TryParse({localVariableName}.Value, out var parsedValue))");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"errors.Add(new global::ArgumentParser.InvalidArgumentValueException($\"Invalid value for {propertyName}: {{ {localVariableName}.Value }}\"));");
				writer.Indent--;
				writer.WriteLine("}");
				writer.WriteLine("else");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"instance.{propertyName} = parsedValue;");
				writer.Indent--;
				writer.WriteLine("}");
				break;
			case "bool":
				writer.WriteLine($"if (!bool.TryParse({localVariableName}.Value, out var parsedValue))");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"errors.Add(new global::ArgumentParser.InvalidArgumentValueException($\"Invalid value for {propertyName}: {{ {localVariableName}.Value }}\"));");
				writer.Indent--;
				writer.WriteLine("}");
				writer.WriteLine("else");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"instance.{propertyName} = parsedValue;");
				writer.Indent--;
				writer.WriteLine("}");
				break;
			case "System.DateTime":
				writer.WriteLine($"if (!DateTime.TryParse({localVariableName}.Value, out var parsedValue))");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"errors.Add(new global::ArgumentParser.InvalidArgumentValueException($\"Invalid value for {propertyName}: {{ {localVariableName}.Value }}\"));");
				writer.Indent--;
				writer.WriteLine("}");
				writer.WriteLine("else");
				writer.WriteLine("{");
				writer.Indent++;
				writer.WriteLine($"instance.{propertyName} = parsedValue;");
				writer.Indent--;
				writer.WriteLine("}");
				break;
			case "string":
				writer.WriteLine($"instance.{propertyName} = {localVariableName}.Value;");
				break;
			case "string?":
				writer.WriteLine($"instance.{propertyName} = {localVariableName}.Value;");
				break;
			default:
				writer.WriteLine($"// Unsupported property type: {propertyType}");
				break;
		}
	}

	internal static string NameCondition(string token, string shortName, string longName)
	{
		var conditions = new List<string>();
		if (!string.IsNullOrEmpty(shortName))
		{
			conditions.Add($"({token}.IsShort && {token}.Name == {Literal(shortName)})");
		}
		if (!string.IsNullOrEmpty(longName))
		{
			conditions.Add($"(!{token}.IsShort && {token}.Name == {Literal(longName)})");
		}
		return conditions.Count == 0 ? "false" : string.Join(" || ", conditions);
	}

	internal static string Literal(string value)
	{
		return SymbolDisplay.FormatLiteral(value ?? string.Empty, quote: true);
	}

	internal static string RequiredKey(PropertyAndAttributeInfo option)
	{
		return $"{option.Attribute.ShortName} | {option.Attribute.LongName}";
	}

	internal static void WriteFlagParseCode(PropertyAndAttributeInfo flagInfo, IndentedTextWriter writer)
	{
		var propertyName = flagInfo.PropertyName;
		bool isBool = flagInfo.PropertyType == "bool";
		bool isEnum = flagInfo.IsEnum;
		if (!isBool && !isEnum)
		{
			// could maybe throw? but that might annoy a user who somehow manages to pass validation on an invalid type
			writer.WriteLine($"// Unsupported flag type: {flagInfo.PropertyType}");
			return;
		}
		if (isBool)
		{
			writer.WriteLine($"instance.{propertyName} = true;");
			return;
		}
		if (isEnum)
		{
			// take the level of the token and cast to enum type
			writer.WriteLine($"instance.{propertyName} = ({flagInfo.PropertyType})flagToken.Level;");
		}
	}
}
