using System;
using System.Collections.ObjectModel;
using System.Text;

namespace ArgumentParser.Internal;

/// <summary>
/// Provides functionality to generate help text for command-line argument parsers, including required and optional arguments, flags, and options.
/// </summary>
public static class HelptextProvider
{
	/// <summary>
	/// Generates help text for the specified program, including required and optional arguments, flags, and options.
	/// </summary>
	/// <param name="programName">The name of the program or command.</param>
	/// <param name="options">A collection of option parameters and their metadata.</param>
	/// <param name="flags">A collection of flag parameters and their metadata.</param>
	/// <param name="positionals">A collection of positional parameters and their metadata.</param>
	/// <returns>A formatted help text string describing the usage, required, and optional arguments.</returns>
	public static string GenerateHelpText(
		string programName,
		ReadOnlyCollection<PropertyAndAttributeInfo> options,
		ReadOnlyCollection<PropertyAndAttributeInfo> flags,
		ReadOnlyCollection<PropertyAndAttributeInfo> positionals
	)
	{
		if (options == null)
		{
			throw new ArgumentNullException(nameof(options));
		}
		if (flags == null)
		{
			throw new ArgumentNullException(nameof(flags));
		}
		if (positionals == null)
		{
			throw new ArgumentNullException(nameof(positionals));
		}
		StringBuilder helpText = new StringBuilder();
		helpText.AppendLine(programName);
		helpText.AppendLine();
		helpText.AppendLine("Example usage: ");

		// put example use in the style of "programName [optional stuff] (required stuff)"
		helpText.AppendLine($"\t{programName} [optional args] (required args)");

		helpText.AppendLine();
		helpText.AppendLine("Required arguments: ");
		bool hasRequiredArgs = false;
		foreach (var positional in positionals)
		{
			if (positional.Attribute.Required)
			{
				hasRequiredArgs = true;
				helpText.AppendLine($"\tPosition: {positional.Attribute.Position}: {positional.Attribute.Description}");
			}
		}
		foreach (var flag in flags)
		{
			if (flag.Attribute.Required)
			{
				hasRequiredArgs = true;
				helpText.Append("\tFlag: ");
				if (!string.IsNullOrEmpty(flag.Attribute.ShortName))
				{
					helpText.Append($"-{flag.Attribute.ShortName} ");
					if (!string.IsNullOrEmpty(flag.Attribute.LongName))
					{
						helpText.Append($"| --{flag.Attribute.LongName} ");
					}
				}
				else if (!string.IsNullOrEmpty(flag.Attribute.LongName))
				{
					helpText.Append($"--{flag.Attribute.LongName} ");
				}
				helpText.AppendLine($": {flag.Attribute.Description}");
			}
		}
		foreach (var option in options)
		{
			if (option.Attribute.Required)
			{
				hasRequiredArgs = true;
				helpText.Append("\tOption: ");
				if (!string.IsNullOrEmpty(option.Attribute.ShortName))
				{
					helpText.Append($"-{option.Attribute.ShortName} ");
					if (!string.IsNullOrEmpty(option.Attribute.LongName))
					{
						helpText.Append($"| --{option.Attribute.LongName} ");
					}
				}
				else if (!string.IsNullOrEmpty(option.Attribute.LongName))
				{
					helpText.Append($"--{option.Attribute.LongName} ");
				}
				helpText.AppendLine($": {option.Attribute.Description}");
			}
		}
		if (!hasRequiredArgs)
		{
			helpText.AppendLine("\tNone");
		}

		helpText.AppendLine();
		helpText.AppendLine("Optional arguments: ");
		bool hasOptionalArgs = false;
		foreach (var positional in positionals)
		{
			if (!positional.Attribute.Required)
			{
				hasOptionalArgs = true;
				helpText.AppendLine($"\tPosition: {positional.Attribute.Position}: {positional.Attribute.Description}");
			}
		}
		foreach (var flag in flags)
		{
			if (!flag.Attribute.Required)
			{
				hasOptionalArgs = true;
				helpText.Append("\tFlag: ");
				if (!string.IsNullOrEmpty(flag.Attribute.ShortName))
				{
					helpText.Append($"-{flag.Attribute.ShortName} ");
					if (!string.IsNullOrEmpty(flag.Attribute.LongName))
					{
						helpText.Append($"| --{flag.Attribute.LongName} ");
					}
				}
				else if (!string.IsNullOrEmpty(flag.Attribute.LongName))
				{
					helpText.Append($"--{flag.Attribute.LongName} ");
				}
				helpText.AppendLine($": {flag.Attribute.Description}");
			}
		}
		foreach (var option in options)
		{
			if (!option.Attribute.Required)
			{
				hasOptionalArgs = true;
				helpText.Append("\tOption: ");
				if (!string.IsNullOrEmpty(option.Attribute.ShortName))
				{
					helpText.Append($"-{option.Attribute.ShortName} ");
					if (!string.IsNullOrEmpty(option.Attribute.LongName))
					{
						helpText.Append($"| --{option.Attribute.LongName} ");
					}
				}
				else if (!string.IsNullOrEmpty(option.Attribute.LongName))
				{
					helpText.Append($"--{option.Attribute.LongName} ");
				}
				helpText.AppendLine($": {option.Attribute.Description}");
			}
		}
		if (!hasOptionalArgs)
		{
			helpText.AppendLine("\tNone");
		}

		helpText.AppendLine();
		
		return helpText.ToString();
	}
}
