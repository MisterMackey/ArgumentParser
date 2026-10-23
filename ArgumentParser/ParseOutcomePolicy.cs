using System;
using System.Collections.Generic;
using System.IO;

namespace ArgumentParser
{

	/// <summary>
	/// Applies the help text and <see cref="BehaviourOnError"/> settings to the outcome of a parse.
	/// The generated Parse methods call this after binding the arguments, so the policy lives in one place
	/// and can be used and tested without generating or compiling anything.
	/// </summary>
	public static class ParseOutcomePolicy
	{
		/// <summary>
		/// The exit code used after the help text was displayed on request.
		/// </summary>
		public const int HelpExitCode = 0;

		/// <summary>
		/// The exit code used after the errors and the help text were displayed.
		/// </summary>
		public const int ErrorExitCode = 2;

		/// <summary>
		/// Applies the policy, writing to the console and ending the process through <see cref="Environment.Exit(int)"/> where the policy asks for it.
		/// </summary>
		/// <param name="behaviourOnError">What to do when errors were found.</param>
		/// <param name="helpRequested">True if the help text must be displayed because the help argument was given.</param>
		/// <param name="helpText">The help text. May be null when it is neither displayed on request nor on error.</param>
		/// <param name="errors">All errors found while parsing.</param>
		/// <param name="missingRequired">The errors for required arguments that were not given, which are also part of <paramref name="errors"/>.</param>
		public static void Apply(
			BehaviourOnError behaviourOnError,
			bool helpRequested,
			string? helpText,
			IReadOnlyList<ArgumentParserException> errors,
			IReadOnlyList<ArgumentParserException> missingRequired)
		{
			Apply(behaviourOnError, helpRequested, helpText, errors, missingRequired, Console.Out, Environment.Exit);
		}

		/// <summary>
		/// Applies the policy, writing to <paramref name="output"/> and ending the process through <paramref name="exit"/> where the policy asks for it.
		/// Code that must survive a bad call can pass an exit that does not end the process;
		/// nothing else happens after <paramref name="exit"/> has been called.
		/// </summary>
		/// <param name="behaviourOnError">What to do when errors were found.</param>
		/// <param name="helpRequested">True if the help text must be displayed because the help argument was given.</param>
		/// <param name="helpText">The help text. May be null when it is neither displayed on request nor on error.</param>
		/// <param name="errors">All errors found while parsing.</param>
		/// <param name="missingRequired">The errors for required arguments that were not given, which are also part of <paramref name="errors"/>.</param>
		/// <param name="output">Where the help text and errors are written.</param>
		/// <param name="exit">Called with the exit code when the process should end.</param>
		/// <exception cref="AggregateException">Thrown when <paramref name="behaviourOnError"/> asks for the errors to be thrown.</exception>
		public static void Apply(
			BehaviourOnError behaviourOnError,
			bool helpRequested,
			string? helpText,
			IReadOnlyList<ArgumentParserException> errors,
			IReadOnlyList<ArgumentParserException> missingRequired,
			TextWriter output,
			Action<int> exit)
		{
			if (errors is null)
				throw new ArgumentNullException(nameof(errors));
			if (missingRequired is null)
				throw new ArgumentNullException(nameof(missingRequired));
			if (output is null)
				throw new ArgumentNullException(nameof(output));
			if (exit is null)
				throw new ArgumentNullException(nameof(exit));

			if (helpRequested)
			{
				output.WriteLine(RequireHelpText(helpText));
				exit(HelpExitCode);
				return;
			}

			switch (behaviourOnError)
			{
				case BehaviourOnError.DisplayHelpAndExit:
					if (errors.Count > 0)
					{
						foreach (var error in errors)
						{
							output.WriteLine(error);
						}
						output.WriteLine();
						output.WriteLine(RequireHelpText(helpText));
						exit(ErrorExitCode);
					}
					break;
				case BehaviourOnError.ThrowIfAnyError:
					if (errors.Count > 0)
					{
						throw new AggregateException("One or more errors occurred during parsing", errors);
					}
					break;
				case BehaviourOnError.ThrowIfMissingRequired:
					if (missingRequired.Count > 0)
					{
						throw new AggregateException("One or more required arguments missing", missingRequired);
					}
					break;
				case BehaviourOnError.ThrowNever:
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(behaviourOnError), behaviourOnError, "Unknown behaviour on error.");
			}
		}

		private static string RequireHelpText(string? helpText)
		{
			return helpText ?? throw new ArgumentNullException(nameof(helpText), "The help text is needed to display it.");
		}
	}
}
