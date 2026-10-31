using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ArgumentParser.Internal.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArgumentParser.Internal;

/// <summary>
/// Contains methods for validating class declarations and attributes.
/// </summary>
public static class Validation
{
	/// <summary>
	/// Validates that a class with the ParameterCollection attribute meets the requirements.
	/// </summary>
	/// <param name="classDeclaration">The class declaration to validate.</param>
	/// <param name="checkParseMethod">Whether the class must not already have a Parse(string[]) method, because the generator is going to add one.</param>
	/// <param name="parts">Every part of the class, whose members all count. Only <paramref name="classDeclaration"/> when left out.</param>
	/// <returns>List of diagnostics if validation fails, empty list if validation passes.</returns>
	public static ReadOnlyCollection<DiagnosticInfo> ValidateClassDeclaration(
		ClassDeclarationSyntax classDeclaration,
		bool checkParseMethod = true,
		IReadOnlyList<ClassDeclarationSyntax>? parts = null)
	{
		if (classDeclaration is null)
		{
			throw new ArgumentNullException(nameof(classDeclaration));
		}
		var diagnostics = new List<DiagnosticInfo>();
		var members = Members(classDeclaration, parts);

		// Validate that the class is partial
		if (!classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
		{
			diagnostics.Add(DiagnosticInfo.Create(
			    GeneratorDiagnostics.ARG001,
			    classDeclaration.GetLocation(),
			    classDeclaration.Identifier.Text));
		}

		// Validate that the class doesn't already have a Parse method
		if (checkParseMethod && members.OfType<MethodDeclarationSyntax>()
		    .Any(m => m.Identifier.Text == "Parse" && m.ParameterList.Parameters.Count == 1
			&& IsStringArray(m.ParameterList.Parameters[0].Type)))
		{
			diagnostics.Add(DiagnosticInfo.Create(
			    GeneratorDiagnostics.ARG002,
			    classDeclaration.GetLocation(),
			    classDeclaration.Identifier.Text));
		}

		// Validate that the class has a constructor with no parameters
		// or no constructor at all (which implies a default constructor).
		// Static constructors are not instance constructors, and a primary constructor replaces the default one.
		var instanceConstructors = members
			.OfType<ConstructorDeclarationSyntax>()
			.Where(c => !c.Modifiers.Any(SyntaxKind.StaticKeyword))
			.ToList();
		var hasParameterlessConstructor = instanceConstructors.Any(c => c.ParameterList.Parameters.Count == 0);
		var hasPrimaryConstructorWithParameters = (parts ?? new[] { classDeclaration }).Any(c => c.ParameterList?.Parameters.Count > 0);
		if (!hasParameterlessConstructor && (instanceConstructors.Count > 0 || hasPrimaryConstructorWithParameters))
		{
			diagnostics.Add(DiagnosticInfo.Create(
			    GeneratorDiagnostics.ARG006,
			    classDeclaration.GetLocation(),
			    classDeclaration.Identifier.Text));
		}

		return diagnostics.AsReadOnly();
	}

	/// <summary>
	/// Validates the place of a class in a tree of commands: its attributes, its name and its arguments.
	/// Rules that need the whole tree, such as duplicate names or unreachable commands, are not checked here.
	/// </summary>
	/// <param name="node">The node to validate.</param>
	/// <param name="attributeLocation">Where the Command or CommandApp attribute is applied.</param>
	/// <param name="hasCommandAttribute">Whether the class has the Command attribute.</param>
	/// <param name="hasParameterCollectionAttribute">Whether the class has the ParameterCollection attribute.</param>
	/// <param name="positionals">The properties of the class with the Positional attribute, with their locations.</param>
	/// <returns>List of diagnostics if validation fails, empty list if validation passes.</returns>
	public static ReadOnlyCollection<DiagnosticInfo> ValidateCommandNode(
		CommandNodeInfo node,
		Location? attributeLocation,
		bool hasCommandAttribute,
		bool hasParameterCollectionAttribute,
		IEnumerable<(string PropertyName, Location? Location)> positionals
	)
	{
		if (node is null)
		{
			throw new ArgumentNullException(nameof(node));
		}
		if (positionals is null)
		{
			throw new ArgumentNullException(nameof(positionals));
		}
		var diagnostics = new List<DiagnosticInfo>();

		// The root names the application, it is not a word that users type, so only the commands are held to the naming rules
		if (node.Kind == CommandNodeKind.Root)
		{
			if (hasCommandAttribute)
			{
				diagnostics.Add(DiagnosticInfo.Create(GeneratorDiagnostics.ARG016, attributeLocation, node.ClassName, "Command"));
			}
			if (hasParameterCollectionAttribute)
			{
				diagnostics.Add(DiagnosticInfo.Create(GeneratorDiagnostics.ARG016, attributeLocation, node.ClassName, "ParameterCollection"));
			}
			if (node.Settings.UnsupportedByATree is { } unsupported)
			{
				diagnostics.Add(DiagnosticInfo.Create(GeneratorDiagnostics.ARG028, attributeLocation, node.ClassName, unsupported));
			}
		}
		else if (!IsValidCommandName(node.Name))
		{
			diagnostics.Add(DiagnosticInfo.Create(GeneratorDiagnostics.ARG014, attributeLocation, node.Name, node.ClassName));
		}

		// The first positional argument of a group is the name of the next command, so only a leaf can have positionals
		if (node.Kind != CommandNodeKind.Leaf)
		{
			foreach (var (propertyName, location) in positionals)
			{
				diagnostics.Add(DiagnosticInfo.Create(GeneratorDiagnostics.ARG015, location, node.ClassName, propertyName));
			}
		}

		return diagnostics.AsReadOnly();
	}

	/// <summary>
	/// The longest command name: a tool name of the Model Context Protocol has at most 128 characters.
	/// </summary>
	public const int MaxCommandNameLength = 127;

	/// <summary>
	/// Tells whether a command name is valid: not empty, at most <see cref="MaxCommandNameLength"/> characters, not starting with '-',
	/// and only the characters a tool name of the Model Context Protocol may have (ASCII letters, digits, '_', '-' and '.'),
	/// without "__", which separates the commands in the name of a tool.
	/// </summary>
	/// <param name="name">The name of the command.</param>
	/// <returns>True when the name is valid.</returns>
	public static bool IsValidCommandName(string name)
	{
		return !string.IsNullOrEmpty(name)
			&& name.Length <= MaxCommandNameLength
			&& name[0] != '-'
			&& name.All(c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' or '-' or '.')
			&& name.IndexOf(ToolSpecProvider.ToolNameSeparator, StringComparison.Ordinal) < 0;
	}

	/// <summary>
	/// Validates how a class of a tree of commands is declared. The generated parser creates an object of every class in the tree
	/// and assigns its properties, so the class must be concrete and, for a group or the root, have nothing it must set.
	/// </summary>
	/// <param name="node">The node to validate.</param>
	/// <param name="classLocation">Where the name of the class is.</param>
	/// <param name="isGeneric">Whether the class, or a type it is nested in, has type parameters.</param>
	/// <param name="isAbstract">Whether the class is abstract or static.</param>
	/// <param name="requiredMembers">The members with the required modifier that a group or the root has, with their locations.</param>
	/// <returns>List of diagnostics if validation fails, empty list if validation passes.</returns>
	public static ReadOnlyCollection<DiagnosticInfo> ValidateNodeDeclaration(
		CommandNodeInfo node,
		Location? classLocation,
		bool isGeneric,
		bool isAbstract,
		IEnumerable<(string MemberName, Location? Location)> requiredMembers
	)
	{
		if (node is null)
		{
			throw new ArgumentNullException(nameof(node));
		}
		if (requiredMembers is null)
		{
			throw new ArgumentNullException(nameof(requiredMembers));
		}
		var diagnostics = new List<DiagnosticInfo>();
		if (isGeneric)
		{
			diagnostics.Add(DiagnosticInfo.Create(GeneratorDiagnostics.ARG023, classLocation, node.ClassName));
		}
		if (isAbstract)
		{
			diagnostics.Add(DiagnosticInfo.Create(GeneratorDiagnostics.ARG024, classLocation, node.ClassName));
		}
		foreach (var (memberName, location) in requiredMembers)
		{
			diagnostics.Add(DiagnosticInfo.Create(GeneratorDiagnostics.ARG025, location, node.ClassName, memberName));
		}
		return diagnostics.AsReadOnly();
	}

	/// <summary>
	/// Validates that the attributes don't have conflicting values.
	/// </summary>
	/// <param name="provider">The argument provider to validate.</param>
	/// <param name="classDeclarationSyntax"></param>
	/// <returns>List of diagnostics if validation fails, empty list if validation passes.</returns>
	public static ReadOnlyCollection<DiagnosticInfo> ValidateAttributes(
		ArgumentProvider provider,
		ClassDeclarationSyntax classDeclarationSyntax
	)
	{
		if (classDeclarationSyntax is null)
		{
			throw new ArgumentNullException(nameof(classDeclarationSyntax));
		}
		if (provider is null)
		{
			throw new ArgumentNullException(nameof(provider));
		}
		return ValidateAttributes(provider.GetOptionArguments(), provider.GetFlagArguments(), provider.GetPositionalArguments(), new[] { classDeclarationSyntax });
	}

	/// <summary>
	/// Validates that the attributes don't have conflicting values.
	/// </summary>
	/// <param name="options">The option arguments to validate.</param>
	/// <param name="flags">The flag arguments to validate.</param>
	/// <param name="positionals">The positional arguments to validate.</param>
	/// <param name="classDeclarations">The parts of the class that declares the arguments, which is where the diagnostics are located.</param>
	/// <returns>List of diagnostics if validation fails, empty list if validation passes.</returns>
	public static ReadOnlyCollection<DiagnosticInfo> ValidateAttributes(
		IReadOnlyList<PropertyAndAttributeInfo> options,
		IReadOnlyList<PropertyAndAttributeInfo> flags,
		IReadOnlyList<PropertyAndAttributeInfo> positionals,
		IReadOnlyList<ClassDeclarationSyntax> classDeclarations
	)
	{
		if (options is null)
		{
			throw new ArgumentNullException(nameof(options));
		}
		if (flags is null)
		{
			throw new ArgumentNullException(nameof(flags));
		}
		if (positionals is null)
		{
			throw new ArgumentNullException(nameof(positionals));
		}
		if (classDeclarations is null)
		{
			throw new ArgumentNullException(nameof(classDeclarations));
		}
		var diagnostics = new List<DiagnosticInfo>(ValidateCommandClassArguments(options, flags, positionals, classDeclarations));

		// Check for duplicate short names
		var duplicateShortNames = options.
			Select(x => x.Attribute.ShortName)
			.Where(x => !string.IsNullOrEmpty(x))
			.Concat(flags.Select(x => x.Attribute.ShortName))
			.Where(x => !string.IsNullOrEmpty(x))
			.GroupBy(x => x)
			.Where(g => g.Count() > 1)
			.Select(g => g.Key)
			.ToList();

		var duplicateOptions = options
			.Where(x => duplicateShortNames.Contains(x.Attribute.ShortName));
		var duplicateFlags = flags
			.Where(x => duplicateShortNames.Contains(x.Attribute.ShortName));

		foreach (var option in duplicateOptions)
		{
			var location = GetLocation(option, classDeclarations);
			diagnostics.Add(DiagnosticInfo.Create(
			    GeneratorDiagnostics.ARG003,
			    location,
			    "short",
			    option.Attribute.ShortName));
		}
		foreach (var flag in duplicateFlags)
		{
			var location = GetLocation(flag, classDeclarations);
			diagnostics.Add(DiagnosticInfo.Create(
			    GeneratorDiagnostics.ARG003,
			    location,
			    "short",
			    flag.Attribute.ShortName));
		}

		// Check for duplicate long names
		var duplicateLongNames = options.
			Select(x => x.Attribute.LongName)
			.Where(x => !string.IsNullOrEmpty(x))
			.Concat(flags.Select(x => x.Attribute.LongName))
			.Where(x => !string.IsNullOrEmpty(x))
			.GroupBy(x => x)
			.Where(g => g.Count() > 1)
			.Select(g => g.Key)
			.ToList();
		var duplicateLongOptions = options
			.Where(x => duplicateLongNames.Contains(x.Attribute.LongName));
		var duplicateLongFlags = flags
			.Where(x => duplicateLongNames.Contains(x.Attribute.LongName));
		foreach (var option in duplicateLongOptions)
		{
			var location = GetLocation(option, classDeclarations);
			diagnostics.Add(DiagnosticInfo.Create(
			    GeneratorDiagnostics.ARG003,
			    location,
			    "long",
			    option.Attribute.LongName));
		}
		foreach (var flag in duplicateLongFlags)
		{
			var location = GetLocation(flag, classDeclarations);
			diagnostics.Add(DiagnosticInfo.Create(
			    GeneratorDiagnostics.ARG003,
			    location,
			    "long",
			    flag.Attribute.LongName));
		}

		// Check for duplicate positional positions
		var duplicatePositions = positionals.GroupBy(x => x.Attribute.Position)
		    .Where(g => g.Count() > 1)
		    .SelectMany(g => g.ToList())
		    .ToList();
		foreach (var position in duplicatePositions)
		{
			var location = GetLocation(position, classDeclarations);
			diagnostics.Add(DiagnosticInfo.Create(
			    GeneratorDiagnostics.ARG004,
			    location,
			    position.Attribute.Position));
		}

		// Check that positional attributes form a sequence starting from 0
		var invalidPositions = positionals.Where(p => p.Attribute.Position < 0 || p.Attribute.Position >= positionals.Count).ToList();
		foreach (var position in invalidPositions)
		{
			var location = GetLocation(position, classDeclarations);
			diagnostics.Add(DiagnosticInfo.Create(
			    GeneratorDiagnostics.ARG005,
			    location,
			    position.Attribute.Position));
		}

		// Check for unsupported property types (for properties that don't specify custom parsing)
		// A command class is reported by ARG026, which says more than that the type is not supported.
		var allProperties = options
			.Concat(positionals)
			.Concat(flags)
			.Where(x => !x.HasParseMethod && !x.IsCommandClass && !HasNullableValueElement(x))
			.ToList();
		foreach (var prop in allProperties)
		{
			if (!IsSupportedPropertyType(prop))
			{
				var location = GetLocation(prop, classDeclarations);
				diagnostics.Add(DiagnosticInfo.Create(
					GeneratorDiagnostics.ARG007,
					location,
					prop.PropertyType));
			}
		}

		// Check that Flag attributes are only applied to boolean and enum properties
		foreach (var flag in flags)
		{
			// the elements of a list are bool or an enum for List<bool>, but a flag never reads a value, so it cannot hold a list
			if ((flag.IsList || (flag.UnderlyingType != "bool" && !flag.IsEnum))
				&& !flag.IsCommandClass
			)

			{
				var location = GetLocation(flag, classDeclarations);
				diagnostics.Add(DiagnosticInfo.Create(
					GeneratorDiagnostics.ARG013,
					location,
					flag.PropertyName,
					flag.PropertyType));
			}
		}

		// The elements of a list are parsed one by one, and null is not a value of the list
		foreach (var prop in options.Concat(positionals).Where(HasNullableValueElement))
		{
			diagnostics.Add(DiagnosticInfo.Create(
				GeneratorDiagnostics.ARG032,
				GetLocation(prop, classDeclarations),
				prop.PropertyName,
				prop.PropertyType));
		}

		// A list positional takes every value that is left, so no positional can come after it
		var lastPosition = positionals.Count == 0 ? 0 : positionals.Max(p => p.Attribute.Position);
		foreach (var prop in positionals.Where(p => p.IsList && p.Attribute.Position < lastPosition))
		{
			diagnostics.Add(DiagnosticInfo.Create(
				GeneratorDiagnostics.ARG031,
				GetLocation(prop, classDeclarations),
				prop.PropertyName));
		}

		// A flag does not read a value, so only options and positionals need the method that ParsedWithMethod names
		foreach (var prop in options.Concat(positionals).Where(x => x.HasParseMethod && string.IsNullOrEmpty(x.ParseMethodOwner) && !x.IsCommandClass))
		{
			diagnostics.Add(DiagnosticInfo.Create(
				GeneratorDiagnostics.ARG030,
				GetLocation(prop, classDeclarations),
				prop.ParseMethodName,
				prop.PropertyName,
				prop.UnderlyingType));
		}

		// A tool call could never give an argument that is left out of the schema, so it cannot be required (a flag never is).
		// This holds whether or not the class emits a schema: the attribute says the argument is set by the program, required says it is not.
		foreach (var prop in options.Concat(positionals).Where(x => x.ExcludeFromManifest && x.Attribute.Required))
		{
			diagnostics.Add(DiagnosticInfo.Create(
				GeneratorDiagnostics.ARG033,
				GetLocation(prop, classDeclarations),
				prop.PropertyName));
		}

		return diagnostics.AsReadOnly();
	}

	/// <summary>
	/// Validates that no two arguments of a class have the same key in its JSON Schema (ARG034). The keys are the property names in camelCase,
	/// so <c>Name</c> and <c>name</c> are the same key. Only checked when the class emits a schema or a binder; in a tree,
	/// <see cref="CommandTreeLinker"/> also compares the keys with those of the commands above.
	/// </summary>
	/// <param name="options">The option arguments to validate.</param>
	/// <param name="flags">The flag arguments to validate.</param>
	/// <param name="positionals">The positional arguments to validate.</param>
	/// <param name="className">The name of the class, for the message.</param>
	/// <param name="classDeclarations">The parts of the class that declares the arguments, which is where the diagnostics are located.</param>
	/// <returns>A diagnostic for every argument whose key an earlier argument already has.</returns>
	public static ReadOnlyCollection<DiagnosticInfo> ValidateManifestKeys(
		IReadOnlyList<PropertyAndAttributeInfo> options,
		IReadOnlyList<PropertyAndAttributeInfo> flags,
		IReadOnlyList<PropertyAndAttributeInfo> positionals,
		string className,
		IReadOnlyList<ClassDeclarationSyntax> classDeclarations)
	{
		if (classDeclarations is null)
		{
			throw new ArgumentNullException(nameof(classDeclarations));
		}
		var diagnostics = new List<DiagnosticInfo>();
		foreach (var (argument, first) in DuplicateKeys(ToolSpecProvider.FieldArguments(options, flags, positionals).Select(item => item.Argument)))
		{
			diagnostics.Add(DiagnosticInfo.Create(
				GeneratorDiagnostics.ARG034,
				GetLocation(argument, classDeclarations),
				argument.PropertyName,
				className,
				ToolSpecProvider.Key(argument.PropertyName),
				first.PropertyName,
				className));
		}
		return diagnostics.AsReadOnly();
	}

	/// <summary>
	/// Finds the arguments whose key an earlier argument of the same sequence already has.
	/// </summary>
	/// <param name="arguments">The arguments, in the order of the fields.</param>
	/// <returns>Every such argument, with the first argument that has its key.</returns>
	internal static IEnumerable<(PropertyAndAttributeInfo Argument, PropertyAndAttributeInfo First)> DuplicateKeys(IEnumerable<PropertyAndAttributeInfo> arguments)
	{
		return arguments
			.GroupBy(a => ToolSpecProvider.Key(a.PropertyName), StringComparer.Ordinal)
			.Where(g => g.Count() > 1)
			.SelectMany(g => g.Skip(1).Select(a => (a, g.First())));
	}

	/// <summary>
	/// Validates that no argument has a command class as its type. A property of a command class type declares a child command,
	/// so the attributes of an argument and of a command must be on separate types.
	/// </summary>
	/// <param name="options">The option arguments to validate.</param>
	/// <param name="flags">The flag arguments to validate.</param>
	/// <param name="positionals">The positional arguments to validate.</param>
	/// <param name="classDeclarations">The parts of the class that declares the arguments, which is where the diagnostics are located.</param>
	/// <returns>The diagnostics, one for every argument of a command class type.</returns>
	public static ReadOnlyCollection<DiagnosticInfo> ValidateCommandClassArguments(
		IReadOnlyList<PropertyAndAttributeInfo> options,
		IReadOnlyList<PropertyAndAttributeInfo> flags,
		IReadOnlyList<PropertyAndAttributeInfo> positionals,
		IReadOnlyList<ClassDeclarationSyntax> classDeclarations
	)
	{
		if (options is null)
		{
			throw new ArgumentNullException(nameof(options));
		}
		if (flags is null)
		{
			throw new ArgumentNullException(nameof(flags));
		}
		if (positionals is null)
		{
			throw new ArgumentNullException(nameof(positionals));
		}
		if (classDeclarations is null)
		{
			throw new ArgumentNullException(nameof(classDeclarations));
		}
		var diagnostics = new List<DiagnosticInfo>();
		foreach (var (arguments, attributeName) in new[] { (options, "Option"), (flags, "Flag"), (positionals, "Positional") })
		{
			foreach (var argument in arguments.Where(a => a.IsCommandClass))
			{
				diagnostics.Add(DiagnosticInfo.Create(
					GeneratorDiagnostics.ARG026,
					GetLocation(argument, classDeclarations),
					argument.PropertyName,
					attributeName,
					argument.PropertyType));
			}
		}
		return diagnostics.AsReadOnly();
	}

	/// <summary>
	/// Inspect the generation options in combination with the arguments and the class declaration.
	/// </summary>
	/// <param name="config"></param>
	/// <param name="provider"></param>
	/// <param name="classDeclarationSyntax">The part of the class with the ParameterCollection attribute.</param>
	/// <param name="parts">Every part of the class, whose members all count. Only <paramref name="classDeclarationSyntax"/> when left out.</param>
	/// <returns></returns>
	/// <exception cref="ArgumentNullException"></exception>
	public static ReadOnlyCollection<DiagnosticInfo> ValidateGenerationOptions(
		GeneratorConfiguration config,
		ArgumentProvider provider,
		ClassDeclarationSyntax classDeclarationSyntax,
		IReadOnlyList<ClassDeclarationSyntax>? parts = null
	)
	{
		if (config is null)
		{
			throw new ArgumentNullException(nameof(config));
		}
		if (provider is null)
		{
			throw new ArgumentNullException(nameof(provider));
		}
		if (classDeclarationSyntax is null)
		{
			throw new ArgumentNullException(nameof(classDeclarationSyntax));
		}

		var diagnostics = new List<DiagnosticInfo>();
		var members = Members(classDeclarationSyntax, parts);

		// Find the ParameterCollectionAttribute location
		var parameterCollectionAttribute = classDeclarationSyntax.AttributeLists
			.SelectMany(list => list.Attributes)
			.FirstOrDefault(attr => attr.Name.ToString().Contains("ParameterCollection"));
		var attributeLocation = parameterCollectionAttribute?.GetLocation() ?? classDeclarationSyntax.GetLocation();

		// Check if helptext argument names are specified but argument generation is disabled (ARG008)
		if (!config.HelpArgumentShouldBeGenerated() &&
			(config.HelpArgumentShortName != "h" ||
			 config.HelpArgumentLongName != "Help"))
		{
			diagnostics.Add(DiagnosticInfo.Create(
				GeneratorDiagnostics.ARG008,
				attributeLocation));
		}

		// Check if helptext property should be displayed
		bool hasHelpTextConst = members
			.OfType<FieldDeclarationSyntax>()
			.Any(f => f.Modifiers.Any(SyntaxKind.ConstKeyword) &&
				 f.Declaration.Variables.Any(v => v.Identifier.Text == "HelpText"));

		// Check for missing HelpText property when argument handler is generated (ARG009)
		if (config.HelpArgumentShouldBeGenerated() && config.HelpTextShouldDisplayOnRequest() && !hasHelpTextConst && !config.HelpTextShouldBeGenerated())
		{
			diagnostics.Add(DiagnosticInfo.Create(
				GeneratorDiagnostics.ARG009,
				attributeLocation));
		}

		// Check for missing HelpText property when display help on error is enabled (ARG010)
		if (config.HelpTextShouldDisplayOnError() && !hasHelpTextConst && !config.HelpTextShouldBeGenerated())
		{
			diagnostics.Add(DiagnosticInfo.Create(
				GeneratorDiagnostics.ARG010,
				attributeLocation));
		}

		// Check for user-defined DisplayHelp property when generator is creating one (ARG011)
		if (config.HelpArgumentShouldBeGenerated())
		{
			bool hasDisplayHelpProperty = members
				.OfType<PropertyDeclarationSyntax>()
				.Any(p => p.Identifier.Text == "DisplayHelp");

			if (hasDisplayHelpProperty)
			{
				diagnostics.Add(DiagnosticInfo.Create(
					GeneratorDiagnostics.ARG011,
					attributeLocation));
			}
		}

		// Check for user-defined HelpText const string when generator is creating one (ARG012)
		if (config.HelpTextShouldBeGenerated() && hasHelpTextConst)
		{
			diagnostics.Add(DiagnosticInfo.Create(
				GeneratorDiagnostics.ARG012,
				attributeLocation));
		}

		return diagnostics.AsReadOnly();
	}

	// List<int?>: an element is a value that was given, so it cannot be null. A reference type (List<string?>) has no nullable element in the model.
	private static bool HasNullableValueElement(PropertyAndAttributeInfo info)
	{
		return info.IsList && info.UnderlyingType.EndsWith("?", StringComparison.Ordinal);
	}

	/// <summary>
	/// Determines if a property type is supported by the argument parser generator.
	/// </summary>
	/// <param name="propInfo">The property type to check.</param>
	/// <returns>True if the property type is supported; otherwise, false.</returns>
	private static bool IsSupportedPropertyType(PropertyAndAttributeInfo propInfo)
	{
		// check if its a 'simple' BCL type
		var propertyType = propInfo.UnderlyingType;
		var simpleSupportedType = propertyType switch
		{
			"int" => true,
			"double" => true,
			"float" => true,
			"long" => true,
			"short" => true,
			"decimal" => true,
			"byte" => true,
			"sbyte" => true,
			"char" => true,
			"uint" => true,
			"ulong" => true,
			"ushort" => true,
			"System.Guid" => true,
			"System.Uri" => true,
			"System.TimeSpan" => true,
			"bool" => true,
			"System.DateTime" => true,
			"string" => true,
			_ => false
		};
		if (simpleSupportedType)
		{
			return true;
		}
		// Enum types are also supported
		if (propInfo.IsEnum)
		{
			return true;
		}
		return false;
	}

	private static bool IsStringArray(TypeSyntax? type)
	{
		if (type is not ArrayTypeSyntax array || array.RankSpecifiers.Count != 1 || array.RankSpecifiers[0].Rank != 1)
		{
			return false;
		}
		var elementType = array.ElementType.ToString();
		return elementType is "string" or "String" or "System.String" or "global::System.String";
	}

	private static List<MemberDeclarationSyntax> Members(ClassDeclarationSyntax classDeclaration, IReadOnlyList<ClassDeclarationSyntax>? parts)
	{
		return (parts ?? new[] { classDeclaration }).SelectMany(c => c.Members).ToList();
	}

	// the members of the class itself: a property with the same name in a nested class is not one of its arguments
	private static Location? GetLocation(PropertyAndAttributeInfo info, IEnumerable<ClassDeclarationSyntax> classDeclarations)
	{
		var property = classDeclarations
		    .SelectMany(c => c.Members)
		    .OfType<PropertyDeclarationSyntax>()
		    .FirstOrDefault(p => p.Identifier.Text == info.PropertyName);
		if (property != null)
		{
			return property.GetLocation();
		}
		return null;
	}

	/// <summary>
	/// Reports a list of diagnostics to the source production context.
	/// </summary>
	/// <param name="context">The source production context to report to.</param>
	/// <param name="diagnostics">The diagnostics to report.</param>
	public static void ReportDiagnostics(SourceProductionContext context, ReadOnlyCollection<DiagnosticInfo> diagnostics)
	{
		if (diagnostics is null)
		{
			return;
		}
		foreach (var diagnostic in diagnostics)
		{
			context.ReportDiagnostic(diagnostic.ToDiagnostic());
		}
	}
}
