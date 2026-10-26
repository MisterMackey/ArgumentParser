using System;
using System.Globalization;

namespace ArgumentParser.Internal.Utilities;

/// <summary>
/// The help, error and culture settings of a node, as plain data: those of the CommandApp attribute for the root, which apply to the whole tree,
/// and those of the ParameterCollection attribute for a leaf, which only apply to its own Parse. A group has the defaults.
/// The enum values are kept as their numbers, the way they reach the generator.
/// </summary>
public sealed class NodeSettings : IEquatable<NodeSettings>
{
	private static readonly string[] HelpModeNames = ["GenerateAll", "None", "GenerateTextOnly", "GenerateArgumentOnly", "GenerateArgumentAndHandler"];
	private static readonly string[] BehaviourNames = ["DisplayHelpAndExit", "ThrowIfAnyError", "ThrowNever", "ThrowIfMissingRequired"];

	private const int GenerateAll = 0;
	private const int None = 1;
	private const int GenerateTextOnly = 2;
	private const int DisplayHelpAndExit = 0;

	/// <summary>
	/// Gets the settings that both attributes have when none of their arguments are given.
	/// </summary>
	public static NodeSettings Default { get; } = new NodeSettings(GenerateAll, "h", "Help", DisplayHelpAndExit);

	/// <summary>
	/// Gets the numeric value of the HelpTextGeneration setting.
	/// </summary>
	public int HelpTextGeneration { get; }

	/// <summary>
	/// Gets the short name of the help argument.
	/// </summary>
	public string HelpShortName { get; }

	/// <summary>
	/// Gets the long name of the help argument.
	/// </summary>
	public string HelpLongName { get; }

	/// <summary>
	/// Gets the numeric value of the BehaviourOnError setting.
	/// </summary>
	public int BehaviourOnError { get; }

	/// <summary>
	/// Gets a value indicating whether numbers, dates and times are read with the current culture instead of the invariant culture.
	/// </summary>
	public bool UseCurrentCulture { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="NodeSettings"/> class.
	/// </summary>
	/// <param name="helpTextGeneration">The numeric value of the HelpTextGeneration setting.</param>
	/// <param name="helpShortName">The short name of the help argument.</param>
	/// <param name="helpLongName">The long name of the help argument.</param>
	/// <param name="behaviourOnError">The numeric value of the BehaviourOnError setting.</param>
	/// <param name="useCurrentCulture">The UseCurrentCulture setting.</param>
	public NodeSettings(int helpTextGeneration, string helpShortName, string helpLongName, int behaviourOnError, bool useCurrentCulture = false)
	{
		HelpTextGeneration = helpTextGeneration;
		HelpShortName = helpShortName ?? "";
		HelpLongName = helpLongName ?? "";
		BehaviourOnError = behaviourOnError;
		UseCurrentCulture = useCurrentCulture;
	}

	/// <summary>
	/// Gets a value indicating whether the help text of every level is generated.
	/// </summary>
	public bool GeneratesHelpText => HelpTextGeneration is GenerateAll or GenerateTextOnly;

	/// <summary>
	/// Gets a value indicating whether the help argument is accepted on every level, and displays the help and exits.
	/// </summary>
	public bool HasHelpArgument => HelpTextGeneration == GenerateAll && (HelpShortName.Length > 0 || HelpLongName.Length > 0);

	/// <summary>
	/// Gets the reason a tree cannot use these settings, or null when it can.
	/// The tree generates the help text of every level, so the modes in which the user writes it are not supported,
	/// and without any help text an error cannot display it.
	/// </summary>
	public string? UnsupportedByATree => HelpTextGeneration switch
	{
		GenerateAll or GenerateTextOnly => null,
		None when BehaviourOnError == DisplayHelpAndExit => "HelpTextGeneration.None together with BehaviourOnError.DisplayHelpAndExit",
		None => null,
		_ => "HelpTextGeneration." + HelpModeName,
	};

	/// <summary>
	/// Gets the name of the HelpTextGeneration value, or its number when it is not one of the named values.
	/// </summary>
	public string HelpModeName => Name(HelpModeNames, HelpTextGeneration);

	/// <summary>
	/// Gets the UseCurrentCulture value as a literal for the generated code.
	/// </summary>
	public string UseCurrentCultureLiteral => UseCurrentCulture ? "true" : "false";

	/// <summary>
	/// Gets the BehaviourOnError value as an expression for the generated code.
	/// </summary>
	public string BehaviourExpression => BehaviourOnError >= 0 && BehaviourOnError < BehaviourNames.Length
		? "global::ArgumentParser.BehaviourOnError." + BehaviourNames[BehaviourOnError]
		: "(global::ArgumentParser.BehaviourOnError)(" + BehaviourOnError.ToString(CultureInfo.InvariantCulture) + ")";

	private static string Name(string[] names, int value)
	{
		return value >= 0 && value < names.Length ? names[value] : value.ToString(CultureInfo.InvariantCulture);
	}

	/// <inheritdoc/>
	public bool Equals(NodeSettings? other)
	{
		return other is not null
			&& HelpTextGeneration == other.HelpTextGeneration
			&& HelpShortName == other.HelpShortName
			&& HelpLongName == other.HelpLongName
			&& BehaviourOnError == other.BehaviourOnError
			&& UseCurrentCulture == other.UseCurrentCulture;
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj)
	{
		return Equals(obj as NodeSettings);
	}

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		int hash = 17;
		hash = hash * 23 + HelpTextGeneration;
		hash = hash * 23 + HelpShortName.GetHashCode();
		hash = hash * 23 + HelpLongName.GetHashCode();
		hash = hash * 23 + BehaviourOnError;
		hash = hash * 23 + (UseCurrentCulture ? 1 : 0);
		return hash;
	}
}
