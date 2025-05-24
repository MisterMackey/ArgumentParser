using System;
using System.Collections.ObjectModel;
using System.Text;
using ArgumentParser.Internal.Utilities;

namespace ArgumentParser.Internal;

/// <summary>
/// Provides functionality to generate help text for command-line argument parsers, including required and optional arguments, flags, and options.
/// </summary>
public class HelptextProvider
{
	private readonly Configuration _config;
	private readonly ArgumentProvider _argumentProvider;
	private readonly string ProgramName;

	/// <summary>
	/// Initializes a new instance of the <see cref="HelptextProvider"/> class.
	/// </summary>
	/// <param name="config">The configuration settings for help text generation.</param>
	/// <param name="argumentProvider">The argument provider for retrieving argument information.</param>
	/// <param name="programName">The name of the program for which help text is generated.</param>
	public HelptextProvider(Configuration config,
		ArgumentProvider argumentProvider, string programName)
	{
		_config = config ?? throw new ArgumentNullException(nameof(config));
		_argumentProvider = argumentProvider ?? throw new ArgumentNullException(nameof(argumentProvider));
		ProgramName = programName ?? throw new ArgumentNullException(nameof(programName));
	}

	/// <summary>
	/// Generates help text for the specified program, including required and optional arguments, flags, and options, if configured to do so.
	/// Returns an empty string if help text generation is disabled in the configuration.
	/// </summary>
	/// <returns>A formatted help text string or an empty string if help text generation is disabled.</returns>
	public string GenerateHelpText()
	{
		var options = _argumentProvider.GetOptionArguments();
		var flags = _argumentProvider.GetFlagArguments();
		var positionals = _argumentProvider.GetPositionalArguments();
		StringBuilder helpText = new StringBuilder();
		helpText.AppendLine("public const string HelpText = \"\"\"");
		helpText.AppendLine(ProgramName);
		helpText.AppendLine();
		helpText.AppendLine("Example usage: ");

		// put example use in the style of "ProgramName [optional stuff] (required stuff)"
		helpText.AppendLine($"\t{ProgramName} [optional args] (required args)");

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
		helpText.AppendLine("\"\"\";");

		return helpText.ToString();
	}
}
