using System;

namespace ArgumentParser
{


	/// <summary>
	/// This attribute is used to mark a class as a collection of parameters.
	/// The class should be a public partial class with a parameterless constructor.
	///  The parameters should be defined as public, mutable properties of the class
	/// and should in turn be decorated with one of the parameter attributes.
	/// 
	/// During compilation, the parser generator will generate a parser for this class,
	/// based on the parameters defined in the class. The parser will be generated as a
	/// static method called Parse in the class. This method will take a string[] as
	/// /// input and will return an instance of the class with the parameters set. 
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
	public sealed class ParameterCollectionAttribute : Attribute
	{
		/// <summary>
		/// Gets how help text is generated and displayed.
		/// </summary>
		public HelpTextGeneration HelpTextGeneration { get; }
		/// <summary>
		/// Gets the short name for the help argument (e.g., 'h' for '-h').
		/// </summary>
		public string HelpArgumentShortName { get; }
		/// <summary>
		/// Gets the long name for the help argument (e.g., 'Help' for '--Help').
		/// </summary>
		public string HelpArgumentLongName { get; }
		/// <summary>
		/// Gets the behavior of the parser when errors are encountered during argument parsing.
		/// </summary>
		public BehaviourOnError BehaviourOnError { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ParameterCollectionAttribute"/> class.
		/// </summary>
		/// <param name="helpTextGeneration">Specifies how help text is generated and displayed.</param>
		/// <param name="helpArgumentShortName">The short name for the help argument (e.g., 'h' for '-h').</param>
		/// <param name="helpArgumentLongName">The long name for the help argument (e.g., 'Help' for '--Help').</param>
		/// <param name="behaviourOnError">Specifies the behavior of the parser when errors are encountered during argument parsing.</param>
		public ParameterCollectionAttribute(
			HelpTextGeneration helpTextGeneration = HelpTextGeneration.GenerateAll,
			string helpArgumentShortName = "h",
			string helpArgumentLongName = "Help",
			BehaviourOnError behaviourOnError = BehaviourOnError.DisplayHelpAndExit
		)
		{
			HelpTextGeneration = helpTextGeneration;
			HelpArgumentShortName = helpArgumentShortName;
			HelpArgumentLongName = helpArgumentLongName;
			BehaviourOnError = behaviourOnError;
		}
	}

	/// <summary>
	/// This attribute is used to define an option parameter for the parser.
	/// Options are identified by a short name (e.g., -o) or a long name (e.g., --option).
	/// When an option is used, it is ALWAYS followed by a value (e.g., -o value).
	/// They can include a description and specify whether they are required.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, Inherited = false)]
	public sealed class OptionAttribute : Attribute
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="OptionAttribute"/> class.
		/// </summary>
		/// <param name="shortName"></param>
		/// <param name="longName"></param>
		/// <param name="description"></param>
		/// <param name="required"></param>
		/// <exception cref="ArgumentException"></exception>
		public OptionAttribute(string shortName = "", string longName = "", string description = "", bool required = false)
		{
			if (string.IsNullOrEmpty(shortName) && string.IsNullOrEmpty(longName))
				throw new ArgumentException("Either shortName or longName must be provided.");

			shortName ??= "";
			longName ??= "";
			if (shortName.Length > 1)
				throw new ArgumentException("ShortName must be a single character.");

			ShortName = shortName;
			LongName = longName;
			Description = description;
			Required = required;
		}
		/// <summary>
		/// Gets the short name of the option (e.g., 'o' for '-o').
		/// </summary>
		public string ShortName { get; }
		/// <summary>
		/// Gets the long name of the option (e.g., 'option' for '--option').
		/// </summary>
		public string LongName { get; }
		/// <summary>
		/// Gets the description of the option to be used in help text.
		/// </summary>
		public string Description { get; }
		/// <summary>
		/// Gets a value indicating whether this option is required when parsing arguments.
		/// </summary>
		public bool Required { get; }
	}

	/// <summary>
	/// This attribute is used to define a positional parameter for the parser.
	/// Positional parameters are identified by their position in the argument list
	/// and can include a description and specify whether they are required.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, Inherited = false)]
	public sealed class PositionalAttribute : Attribute
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="PositionalAttribute"/> class.
		/// </summary>
		/// <param name="position"></param>
		/// <param name="description"></param>
		/// <param name="required"></param>
		public PositionalAttribute(int position, string description, bool required = false)
		{
			Position = position;
			Description = description;
			Required = required;
		}
		/// <summary>
		/// Gets the position of the parameter in the argument list.
		/// </summary>
		public int Position { get; }
		/// <summary>
		/// Gets the description of the positional parameter to be used in help text.
		/// </summary>
		public string Description { get; }
		/// <summary>
		/// Gets a value indicating whether this positional parameter is required when parsing arguments.
		/// </summary>
		public bool Required { get; }
	}

	/// <summary>
	/// This attribute is used to define a flag parameter for the parser.
	/// Flags are identified by a short name (e.g., -f) or a long name (e.g., --flag).
	/// Flags are boolean parameters that indicate whether a specific condition is true.
	/// When used, they are NEVER followed by a value.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, Inherited = false)]
	public sealed class FlagAttribute : Attribute
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="FlagAttribute"/> class.
		/// </summary>
		/// <param name="shortName"></param>
		/// <param name="longName"></param>
		/// <param name="description"></param>
		public FlagAttribute(string shortName = "", string longName = "", string description = "")
		{
			if (string.IsNullOrEmpty(shortName) && string.IsNullOrEmpty(longName))
				throw new ArgumentException("Either shortName or longName must be provided.");
			shortName ??= "";
			longName ??= "";
			if (shortName.Length > 1)
				throw new ArgumentException("ShortName must be a single character.");

			ShortName = shortName;
			LongName = longName;
			Description = description;
		}
		/// <summary>
		/// Gets the short name of the flag (e.g., 'f' for '-f').
		/// </summary>
		public string ShortName { get; }
		/// <summary>
		/// Gets the long name of the flag (e.g., 'flag' for '--flag').
		/// </summary>
		public string LongName { get; }
		/// <summary>
		/// Gets the description of the flag to be used in help text.
		/// </summary>
		public string Description { get; }
	}

	/// <summary>
	/// This attribute indicates that the property type is parsed using a method with the specified name.
	/// The method signature should be bool methodName(string input, out T value), i.e. TryParse style.
	/// this method is expected to be a static method on the property's declaring type
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, Inherited = false)]
	public sealed class ParsedWithMethodAttribute : Attribute
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ParsedWithMethodAttribute"/> class.
		/// </summary>
		/// <param name="methodName">The name of the method used to parse the arguments.</param>
		public ParsedWithMethodAttribute(string methodName)
		{
			MethodName = methodName ?? throw new ArgumentNullException(nameof(methodName));
		}

		/// <summary>
		/// Gets the name of the method used to parse the arguments.
		/// </summary>
		public string MethodName { get; }
	}
}