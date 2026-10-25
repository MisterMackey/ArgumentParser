using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ArgumentParser.Internal.Utilities;

/// <summary>
/// Everything the generator needs to know about one annotated class, as plain data without symbols or syntax nodes.
/// The incremental pipeline compares models with <see cref="Equals(ClassModel)"/> to decide whether to generate again.
/// </summary>
public sealed class ClassModel : IEquatable<ClassModel>
{
	/// <summary>
	/// Gets the name of the annotated class.
	/// </summary>
	public string ClassName { get; }

	/// <summary>
	/// Gets the namespace of the class as it is displayed, or null when there is none.
	/// </summary>
	public string? NamespaceName { get; }

	/// <summary>
	/// Gets the types the class is nested in, the outermost first.
	/// </summary>
	public EquatableArray<ContainingType> ContainingTypes { get; }

	/// <summary>
	/// Gets the name of the assembly the class is in, which the help text uses as the program name.
	/// </summary>
	public string ProgramName { get; }

	/// <summary>
	/// Gets the settings of the ParameterCollection attribute, or null when the class was rejected before they were read.
	/// </summary>
	public GeneratorConfiguration? Configuration { get; }

	/// <summary>
	/// Gets the properties with an Option attribute.
	/// </summary>
	public EquatableArray<PropertyAndAttributeInfo> Options { get; }

	/// <summary>
	/// Gets the properties with a Flag attribute.
	/// </summary>
	public EquatableArray<PropertyAndAttributeInfo> Flags { get; }

	/// <summary>
	/// Gets the properties with a Positional attribute.
	/// </summary>
	public EquatableArray<PropertyAndAttributeInfo> Positionals { get; }

	/// <summary>
	/// Gets the diagnostics found while reading the class.
	/// </summary>
	public EquatableArray<DiagnosticInfo> Diagnostics { get; }

	/// <summary>
	/// Gets a value indicating whether the class also has the Command attribute, so a tree of commands can parse it with its own help argument.
	/// </summary>
	public bool IsCommand { get; }

	/// <summary>
	/// Gets the type of the class as it is displayed, the way a tree of commands names its nodes.
	/// </summary>
	public string TypeName { get; }

	/// <summary>
	/// Gets a value indicating whether any of the diagnostics is an error.
	/// </summary>
	public bool HasErrors => Diagnostics.AsReadOnly().Any(item => item.Severity == DiagnosticSeverity.Error);

	/// <summary>
	/// Gets a value indicating whether a parser can be generated for the class.
	/// </summary>
	public bool CanGenerate => Configuration is not null && !HasErrors;

	/// <summary>
	/// Initializes a new instance of the <see cref="ClassModel"/> class.
	/// </summary>
	/// <param name="className">The name of the annotated class.</param>
	/// <param name="namespaceName">The namespace of the class, if any.</param>
	/// <param name="programName">The name of the assembly the class is in.</param>
	/// <param name="configuration">The settings of the ParameterCollection attribute, or null when the class was rejected first.</param>
	/// <param name="options">The properties with an Option attribute.</param>
	/// <param name="flags">The properties with a Flag attribute.</param>
	/// <param name="positionals">The properties with a Positional attribute.</param>
	/// <param name="diagnostics">The diagnostics found while reading the class.</param>
	/// <param name="containingTypes">The types the class is nested in, the outermost first.</param>
	/// <param name="isCommand">Whether the class also has the Command attribute.</param>
	/// <param name="typeName">The type of the class as it is displayed, or null for the name of the class.</param>
	public ClassModel(
		string className,
		string? namespaceName,
		string programName,
		GeneratorConfiguration? configuration,
		EquatableArray<PropertyAndAttributeInfo> options,
		EquatableArray<PropertyAndAttributeInfo> flags,
		EquatableArray<PropertyAndAttributeInfo> positionals,
		EquatableArray<DiagnosticInfo> diagnostics,
		EquatableArray<ContainingType> containingTypes = default,
		bool isCommand = false,
		string? typeName = null)
	{
		ClassName = className ?? throw new ArgumentNullException(nameof(className));
		NamespaceName = namespaceName;
		ProgramName = programName ?? throw new ArgumentNullException(nameof(programName));
		Configuration = configuration;
		Options = options;
		Flags = flags;
		Positionals = positionals;
		Diagnostics = diagnostics;
		ContainingTypes = containingTypes;
		IsCommand = isCommand;
		TypeName = typeName ?? className;
	}

	/// <inheritdoc/>
	public bool Equals(ClassModel? other)
	{
		return other is not null
			&& ClassName == other.ClassName
			&& NamespaceName == other.NamespaceName
			&& ContainingTypes.Equals(other.ContainingTypes)
			&& ProgramName == other.ProgramName
			&& Equals(Configuration, other.Configuration)
			&& Options.Equals(other.Options)
			&& Flags.Equals(other.Flags)
			&& Positionals.Equals(other.Positionals)
			&& Diagnostics.Equals(other.Diagnostics)
			&& IsCommand == other.IsCommand
			&& TypeName == other.TypeName;
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj)
	{
		return Equals(obj as ClassModel);
	}

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		int hash = 17;
		hash = hash * 23 + ClassName.GetHashCode();
		hash = hash * 23 + (NamespaceName?.GetHashCode() ?? 0);
		hash = hash * 23 + ContainingTypes.GetHashCode();
		hash = hash * 23 + ProgramName.GetHashCode();
		hash = hash * 23 + (Configuration?.GetHashCode() ?? 0);
		hash = hash * 23 + Options.GetHashCode();
		hash = hash * 23 + Flags.GetHashCode();
		hash = hash * 23 + Positionals.GetHashCode();
		hash = hash * 23 + Diagnostics.GetHashCode();
		hash = hash * 23 + IsCommand.GetHashCode();
		hash = hash * 23 + TypeName.GetHashCode();
		return hash;
	}
}
