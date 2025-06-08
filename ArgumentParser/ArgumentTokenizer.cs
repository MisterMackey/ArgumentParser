using System.Collections.Generic;
using System.Linq;

namespace ArgumentParser
{

	/// <summary>
	/// This class is responsible for tokenizing command line arguments.
	/// </summary>
	public class ArgumentTokenizer
	{
		private Dictionary<string, int> flagRepeatCounts = new Dictionary<string, int>();
		private void ProcessLongNameArgument(string arg, string[] args, ref int i, OptionAttribute[] options, FlagAttribute[] flags, List<Token> tokens, List<ArgumentParserException> errors)
		{
			var name = arg.Substring(2);
			var option = options.FirstOrDefault(o => o.LongName == name);
			var flag = flags.FirstOrDefault(f => f.LongName == name);

			if (option != null)
			{
				if (i + 1 < args.Length)
				{
					tokens.Add(new OptionToken(name, args[++i]));
				}
				else
				{
					errors.Add(new MissingRequiredArgumentException($"Option '--{name}' requires a value."));
				}
			}
			else if (flag != null)
			{
				// check if there is a corresponding shortname for this flag and use that instead for convenience
				// that way, something like -vv --verbose will be counted to level 3 without any issues
				if (!string.IsNullOrEmpty(flag.ShortName))
				{
					name = flag.ShortName;
				}
				if (flagRepeatCounts.TryGetValue(name, out int count))
				{
					flagRepeatCounts[name] = count + 1;
				}
				else
				{
					flagRepeatCounts[name] = 1;
				}
			}
			else
			{
				errors.Add(new UnexpectedArgumentException($"Unknown argument '--{name}'."));
			}
		}

		private void ProcessShortNameArguments(string arg, string[] args, ref int i, OptionAttribute[] options, FlagAttribute[] flags, List<Token> tokens, List<ArgumentParserException> errors)
		{
			var flagsOrOption = arg.Substring(1);

			for (int j = 0; j < flagsOrOption.Length; j++)
			{
				var name = flagsOrOption[j].ToString();
				var flag = flags.FirstOrDefault(f => f.ShortName == name);
				var option = options.FirstOrDefault(o => o.ShortName == name);

				if (flag != null)
				{
					if (flagRepeatCounts.TryGetValue(name, out int count))
					{
						flagRepeatCounts[name] = count + 1;
					}
					else
					{
						flagRepeatCounts[name] = 1;
					}
				}
				// option arguments are only allowed to appear at the end of the string
				// otherwise it's an error
				else if (j < flagsOrOption.Length - 1)
				{
					errors.Add(new UnexpectedArgumentException($"Unexpected flag '-{name}'."));
				}
				else if (option != null)
				{

					if (i + 1 < args.Length)
					{
						tokens.Add(new OptionToken(name, args[++i]));
					}
					else
					{
						errors.Add(new MissingRequiredArgumentException($"Option '-{name}' requires a value."));
					}
				}
				else
				{
					errors.Add(new UnexpectedArgumentException($"Unknown flag '-{name}'."));
				}
			}
		}

		private static void ProcessPositionalArgument(string arg, PositionalAttribute[] positionals, ref int positionalIndex, List<Token> tokens, List<ArgumentParserException> errors)
		{
			if (positionalIndex < positionals.Length)
			{
				tokens.Add(new PositionalToken(positionalIndex, arg));
				positionalIndex++;
			}
			else
			{
				errors.Add(new UnexpectedArgumentException($"Unexpected positional argument '{arg}'."));
			}
		}

		private void AddFlagTokensWithLevels(List<Token> tokens)
		{
			foreach (var kvp in flagRepeatCounts)
			{
				tokens.Add(new FlagToken(kvp.Key, kvp.Value));
			}
		}

		/// <summary>
		/// Tokenizes the provided arguments into a list of tokens and errors.
		/// </summary>
		/// <param name="args">The array of arguments to tokenize.</param>
		/// <param name="options">The array of option attributes to match against.</param>
		/// <param name="positionals">The array of positional attributes to match against.</param>
		/// <param name="flags">The array of flag attributes to match against.</param>
		/// <returns>A tuple containing a list of tokens and a list of errors.
		/// Keep in mind that the order of the tokens is not guaranteed to be the same as the order of the arguments.
		/// Also, flagtokens may be that have a shortname will always be tokenized with their shortname, even if a longname is provided.
		/// i.e. --verbose will be tokenized as -v, even if --verbose is provided.
		/// </returns>
		public (List<Token> result, List<ArgumentParserException> err) TokenizeArguments(string[] args, OptionAttribute[] options, PositionalAttribute[] positionals, FlagAttribute[] flags)
		{
			flagRepeatCounts.Clear();
			if (args is null)
				throw new System.ArgumentNullException(nameof(args));
			if (options is null)
				throw new System.ArgumentNullException(nameof(options));
			if (positionals is null)
				throw new System.ArgumentNullException(nameof(positionals));
			if (flags is null)
				throw new System.ArgumentNullException(nameof(flags));
			var tokens = new List<Token>();
			var errors = new List<ArgumentParserException>();
			var positionalIndex = 0;

			for (int i = 0; i < args.Length; i++)
			{
				var arg = args[i];

				if (arg.StartsWith("--", System.StringComparison.InvariantCulture))
				{
					ProcessLongNameArgument(arg, args, ref i, options, flags, tokens, errors);
				}
				else if (arg.StartsWith("-", System.StringComparison.InvariantCulture))
				{
					ProcessShortNameArguments(arg, args, ref i, options, flags, tokens, errors);
				}
				else
				{
					ProcessPositionalArgument(arg, positionals, ref positionalIndex, tokens, errors);
				}
			}
			AddFlagTokensWithLevels(tokens);
			return (tokens, errors);
		}
	}

}