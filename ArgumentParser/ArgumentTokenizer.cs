using System.Collections.Generic;
using System.Linq;

namespace ArgumentParser
{

	/// <summary>
	/// This class is responsible for tokenizing command line arguments.
	/// </summary>
	public class ArgumentTokenizer
	{
		private Dictionary<(string Name, bool IsShort), int> flagRepeatCounts = new Dictionary<(string Name, bool IsShort), int>();
		private void ProcessLongNameArgument(string arg, string[] args, ref int i, OptionAttribute[] options, FlagAttribute[] flags, List<Token> tokens, List<ArgumentParserException> errors)
		{
			var name = arg.Substring(2);
			// an argument without a long name has an empty LongName, which must never match
			var option = name.Length == 0 ? null : options.FirstOrDefault(o => o.LongName == name);
			var flag = name.Length == 0 ? null : flags.FirstOrDefault(f => f.LongName == name);

			if (option != null)
			{
				if (i + 1 < args.Length)
				{
					tokens.Add(new OptionToken(name, args[++i], false));
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
				var isShort = !string.IsNullOrEmpty(flag.ShortName);
				if (isShort)
				{
					name = flag.ShortName;
				}
				CountFlag(name, isShort);
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
					CountFlag(name, true);
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
						tokens.Add(new OptionToken(name, args[++i], true));
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

		private static void ProcessPositionalArgument(string arg, PositionalAttribute[] positionals, ref int positionalIndex, bool lastPositionalRepeats, List<Token> tokens, List<ArgumentParserException> errors)
		{
			if (positionalIndex < positionals.Length)
			{
				tokens.Add(new PositionalToken(positionalIndex, arg));
				// a positional that holds a list keeps its position, so every value that follows is a value of the list
				if (!lastPositionalRepeats || positionalIndex < positionals.Length - 1)
				{
					positionalIndex++;
				}
			}
			else
			{
				errors.Add(new UnexpectedArgumentException($"Unexpected positional argument '{arg}'."));
			}
		}

		private void CountFlag(string name, bool isShort)
		{
			var key = (name, isShort);
			flagRepeatCounts.TryGetValue(key, out int count);
			flagRepeatCounts[key] = count + 1;
		}

		private void AddFlagTokensWithLevels(List<Token> tokens)
		{
			foreach (var kvp in flagRepeatCounts)
			{
				tokens.Add(new FlagToken(kvp.Key.Name, kvp.Value, kvp.Key.IsShort));
			}
		}

		/// <summary>
		/// Determines whether the tokens hold the given flag, written with its short or its long name.
		/// A tree of commands uses it to see whether its help argument was given on a level.
		/// </summary>
		/// <param name="tokens">The tokens of one call to the tokenizer.</param>
		/// <param name="flag">The flag to look for.</param>
		/// <returns>True if the flag was given at least once; otherwise, false.</returns>
		public static bool ContainsFlag(IEnumerable<Token> tokens, FlagAttribute flag)
		{
			if (tokens is null)
				throw new System.ArgumentNullException(nameof(tokens));
			if (flag is null)
				throw new System.ArgumentNullException(nameof(flag));
			// a flag that has a short name is always reported with it, whichever name was written
			return tokens.OfType<FlagToken>().Any(t => t.IsShort
				? !string.IsNullOrEmpty(flag.ShortName) && t.Name == flag.ShortName
				: string.IsNullOrEmpty(flag.ShortName) && !string.IsNullOrEmpty(flag.LongName) && t.Name == flag.LongName);
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
			return TokenizeArguments(args, options, positionals, flags, lastPositionalRepeats: false);
		}

		/// <summary>
		/// Tokenizes the provided arguments into a list of tokens and errors, optionally letting the last positional take every remaining value.
		/// </summary>
		/// <param name="args">The array of arguments to tokenize.</param>
		/// <param name="options">The array of option attributes to match against.</param>
		/// <param name="positionals">The array of positional attributes to match against.</param>
		/// <param name="flags">The array of flag attributes to match against.</param>
		/// <param name="lastPositionalRepeats">True when the last positional holds a list: every positional argument after the ones before it
		/// is then tokenized with the position of the last positional, instead of being an unexpected argument.</param>
		/// <returns>A tuple containing a list of tokens and a list of errors, ordered as with the overload without <paramref name="lastPositionalRepeats"/>.</returns>
		public (List<Token> result, List<ArgumentParserException> err) TokenizeArguments(string[] args, OptionAttribute[] options, PositionalAttribute[] positionals, FlagAttribute[] flags, bool lastPositionalRepeats)
		{
			if (positionals is null)
				throw new System.ArgumentNullException(nameof(positionals));
			var (tokens, errors, _) = Tokenize(args, options, positionals, flags, stopAtFirstPositional: false, lastPositionalRepeats);
			return (tokens, errors);
		}

		/// <summary>
		/// Tokenizes the options and flags at the start of the arguments, up to the first argument that is neither.
		/// That argument is the name of a command, so everything from it on belongs to the command and is left alone.
		/// An option consumes the argument after it as its value, whatever that looks like.
		/// </summary>
		/// <param name="args">The array of arguments to tokenize.</param>
		/// <param name="options">The array of option attributes to match against.</param>
		/// <param name="flags">The array of flag attributes to match against.</param>
		/// <returns>A tuple containing a list of tokens, a list of errors, and the index of the first argument that was not tokenized
		/// (the length of <paramref name="args"/> when there is none).
		/// The order of the tokens is not guaranteed, as with <see cref="TokenizeArguments(string[], OptionAttribute[], PositionalAttribute[], FlagAttribute[])"/>.</returns>
		public (List<Token> result, List<ArgumentParserException> err, int commandIndex) TokenizeLeadingArguments(string[] args, OptionAttribute[] options, FlagAttribute[] flags)
		{
			return Tokenize(args, options, System.Array.Empty<PositionalAttribute>(), flags, stopAtFirstPositional: true, lastPositionalRepeats: false);
		}

		private (List<Token> result, List<ArgumentParserException> err, int commandIndex) Tokenize(string[] args, OptionAttribute[] options, PositionalAttribute[] positionals, FlagAttribute[] flags, bool stopAtFirstPositional, bool lastPositionalRepeats)
		{
			flagRepeatCounts.Clear();
			if (args is null)
				throw new System.ArgumentNullException(nameof(args));
			if (options is null)
				throw new System.ArgumentNullException(nameof(options));
			if (flags is null)
				throw new System.ArgumentNullException(nameof(flags));
			var tokens = new List<Token>();
			var errors = new List<ArgumentParserException>();
			var positionalIndex = 0;
			var commandIndex = args.Length;

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
				else if (stopAtFirstPositional)
				{
					commandIndex = i;
					break;
				}
				else
				{
					ProcessPositionalArgument(arg, positionals, ref positionalIndex, lastPositionalRepeats, tokens, errors);
				}
			}
			AddFlagTokensWithLevels(tokens);
			return (tokens, errors, commandIndex);
		}
	}

}
