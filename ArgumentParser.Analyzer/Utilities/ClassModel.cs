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
	/// Gets a value indicating whether any of the diagnostics is an error.
	/// </summary>
	public bool HasErrors => Enumerable.Range(0, Diagnostics.Count).Any(i => Diagnostics[i].Severity == DiagnosticSeverity.Error);

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
	public ClassModel(
		string className,
		string? namespaceName,
		string programName,
		GeneratorConfiguration? configuration,
		EquatableArray<PropertyAndAttributeInfo> options,
		EquatableArray<PropertyAndAttributeInfo> flags,
		EquatableArray<PropertyAndAttributeInfo> positionals,
		EquatableArray<DiagnosticInfo> diagnostics)
	{
		ClassName = className ?? throw new ArgumentNullException(nameof(className));
		NamespaceName = namespaceName;
		ProgramName = programName ?? throw new ArgumentNullException(nameof(programName));
		Configuration = configuration;
		Options = options;
		Flags = flags;
		Positionals = positionals;
		Diagnostics = diagnostics;
	}

	/// <inheritdoc/>
	public bool Equals(ClassModel? other)
	{
		return other is not null
			&& ClassName == other.ClassName
			&& NamespaceName == other.NamespaceName
			&& ProgramName == other.ProgramName
			&& Equals(Configuration, other.Configuration)
			&& Options.Equals(other.Options)
			&& Flags.Equals(other.Flags)
			&& Positionals.Equals(other.Positionals)
			&& Diagnostics.Equals(other.Diagnostics);
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
		hash = hash * 23 + ProgramName.GetHashCode();
		hash = hash * 23 + (Configuration?.GetHashCode() ?? 0);
		hash = hash * 23 + Options.GetHashCode();
		hash = hash * 23 + Flags.GetHashCode();
		hash = hash * 23 + Positionals.GetHashCode();
		hash = hash * 23 + Diagnostics.GetHashCode();
		return hash;
	}
}
