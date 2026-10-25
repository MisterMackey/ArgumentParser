using System;
using System.Collections.Generic;
using System.Linq;

namespace ArgumentParser
{

	/// <summary>
	/// Describes one command of an application: a leaf of the tree of commands that does the work.
	/// The generated <c>Commands</c> list of a class with the CommandApp attribute holds one for every leaf.
	/// </summary>
	public sealed class CommandInfo
	{
		private readonly Func<string[], (object Result, IReadOnlyList<ArgumentParserException> Errors)> parse;

		/// <summary>
		/// Gets the words that select the command, from the first one after the name of the application to the name of the command itself.
		/// </summary>
		public IReadOnlyList<string> Path { get; }

		/// <summary>
		/// Gets the description of the command.
		/// </summary>
		public string Description { get; }

		/// <summary>
		/// Gets the annotations that describe what the command does.
		/// </summary>
		public CommandAnnotations Annotations { get; }

		/// <summary>
		/// Gets the help text of the command, as the tree shows it: with its full path and the options of the commands above it.
		/// Empty when the application does not generate help text.
		/// </summary>
		public string HelpText { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="CommandInfo"/> class.
		/// </summary>
		/// <param name="path">The words that select the command.</param>
		/// <param name="description">The description of the command.</param>
		/// <param name="annotations">The annotations that describe what the command does.</param>
		/// <param name="helpText">The help text of the command.</param>
		/// <param name="parse">Parses the arguments of the command into its parameter collection.</param>
		public CommandInfo(
			IReadOnlyList<string> path,
			string description,
			CommandAnnotations annotations,
			string helpText,
			Func<string[], (object Result, IReadOnlyList<ArgumentParserException> Errors)> parse)
		{
			if (path is null)
			{
				throw new ArgumentNullException(nameof(path));
			}
			Path = path.ToArray();
			Description = description ?? string.Empty;
			Annotations = annotations;
			HelpText = helpText ?? string.Empty;
			this.parse = parse ?? throw new ArgumentNullException(nameof(parse));
		}

		/// <summary>
		/// Parses the arguments of the command: those that come after the words of <see cref="Path"/>.
		/// The options of the groups above the command are not part of them, and neither are applied by this method.
		/// It does not print, exit or throw because of the arguments; it returns what went wrong instead.
		/// The help argument of the application is accepted but does nothing here; the caller can show <see cref="HelpText"/>.
		/// </summary>
		/// <param name="args">The arguments of the command.</param>
		/// <returns>The parameter collection of the command, as an object of the class that declares it, and the errors found.</returns>
		public (object Result, IReadOnlyList<ArgumentParserException> Errors) Parse(string[] args)
		{
			if (args is null)
			{
				throw new ArgumentNullException(nameof(args));
			}
			return parse(args);
		}

		/// <summary>
		/// Gets the words that select the command, separated by spaces.
		/// </summary>
		/// <returns>The path of the command.</returns>
		public override string ToString()
		{
			return string.Join(" ", Path);
		}
	}

}
