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

		/// <summary>
		/// Gets or sets how the command that this class parses behaves, which is carried into the generated data about it
		/// (<see cref="CommandInfo.Annotations"/> when the class is a leaf of a tree of commands). Each value that is set states that
		/// one of the hints of a tool of the Model Context Protocol (MCP) is true, and each value that is not set states that it is false.
		/// The default is that of MCP for a tool without annotations: <see cref="CommandAnnotations.Destructive"/> and
		/// <see cref="CommandAnnotations.OpenWorld"/>. A value that is given replaces the default as a whole, so
		/// <c>Annotations = CommandAnnotations.ReadOnly</c> also states that the command does not reach outside of the application;
		/// write <c>CommandAnnotations.ReadOnly | CommandAnnotations.OpenWorld</c> to keep that part of the default.
		/// The annotations do not change how arguments are parsed or how help text looks.
		/// </summary>
		public CommandAnnotations Annotations { get; set; } = CommandAnnotations.Destructive | CommandAnnotations.OpenWorld;

		/// <summary>
		/// Gets or sets a value indicating whether numbers, dates and times are read with the culture of the current thread.
		/// By default (false) they are read with the invariant culture and only in forms that mean the same everywhere:
		/// numbers without thousands separators and with <c>.</c> as the decimal point (<c>1,5</c> is rejected), and dates and times
		/// as ISO 8601 (<c>2026-10-26</c>, <c>2026-10-26T14:30:00Z</c>); see <see cref="ValueParser"/>.
		/// Set it to true to read them the way the <c>TryParse(string, out T)</c> method of the type does, with the culture of the
		/// machine the program runs on: <c>1,5</c> is then 1.5 with a Dutch culture and 15 with an English one.
		/// For a single argument, a method given with <see cref="ParsedWithMethodAttribute"/> can read the text in any other way.
		/// When the class is a leaf of a tree of commands, the setting of the <see cref="CommandAppAttribute"/> of the tree is used instead.
		/// </summary>
		public bool UseCurrentCulture { get; set; }
	}

	/// <summary>
	/// This attribute marks a class as the root of a tree of commands, such as <c>myFileManagingTool directories list</c>.
	/// The root declares the name of the application. Its child commands are the properties
	/// whose type is a class annotated with <see cref="CommandAttribute"/>, and its own options
	/// (properties with the Option or Flag attribute) are the options that come before the name of the command.
	/// The class should be a public partial class with a parameterless constructor and cannot have positional arguments.
	/// It cannot be generic, abstract or have required members. A child command must be declared by the class itself:
	/// properties inherited from a base class are not child commands. Classes nested in other types are supported
	/// as long as the types around them are partial too.
	/// The help and error settings apply to the whole tree, like those of <see cref="ParameterCollectionAttribute"/> do to one class:
	/// the help argument can be given on every level and shows the help of that level, and an error shows the help of the deepest
	/// level that was reached. So does <see cref="UseCurrentCulture"/>, for the arguments of every level.
	/// The settings of the ParameterCollection attribute of a leaf only apply to its own <c>Parse</c>.
	/// The help text of every level is generated, so only <see cref="HelpTextGeneration.GenerateAll"/>,
	/// <see cref="HelpTextGeneration.GenerateTextOnly"/> and <see cref="HelpTextGeneration.None"/> can be used.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
	public sealed class CommandAppAttribute : Attribute
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="CommandAppAttribute"/> class.
		/// </summary>
		/// <param name="name">The name of the application, as users type it.</param>
		/// <param name="description">The description of the application, used in help text.</param>
		/// <param name="helpTextGeneration">Specifies how help text is generated and displayed for the whole tree.</param>
		/// <param name="helpArgumentShortName">The short name for the help argument (e.g., 'h' for '-h').</param>
		/// <param name="helpArgumentLongName">The long name for the help argument (e.g., 'Help' for '--Help').</param>
		/// <param name="behaviourOnError">Specifies the behavior of the parser when errors are encountered anywhere in the tree.</param>
		public CommandAppAttribute(
			string name,
			string description = "",
			HelpTextGeneration helpTextGeneration = HelpTextGeneration.GenerateAll,
			string helpArgumentShortName = "h",
			string helpArgumentLongName = "Help",
			BehaviourOnError behaviourOnError = BehaviourOnError.DisplayHelpAndExit)
		{
			Name = name ?? throw new ArgumentNullException(nameof(name));
			Description = description ?? "";
			HelpTextGeneration = helpTextGeneration;
			HelpArgumentShortName = helpArgumentShortName ?? "";
			HelpArgumentLongName = helpArgumentLongName ?? "";
			BehaviourOnError = behaviourOnError;
		}

		/// <summary>
		/// Gets how help text is generated and displayed for the whole tree.
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
		/// Gets the behavior of the parser when errors are encountered anywhere in the tree.
		/// </summary>
		public BehaviourOnError BehaviourOnError { get; }

		/// <summary>
		/// Gets the name of the application.
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// Gets the description of the application to be used in help text.
		/// </summary>
		public string Description { get; }
		/// <summary>
		/// Gets or sets a value indicating whether numbers, dates and times are read with the culture of the current thread,
		/// for the arguments of every level of the tree, the leaves included. By default (false) they are read with the invariant culture
		/// and only in forms that mean the same everywhere; see <see cref="ParameterCollectionAttribute.UseCurrentCulture"/>.
		/// </summary>
		public bool UseCurrentCulture { get; set; }
	}

	/// <summary>
	/// This attribute marks a class as a node in a tree of commands, and names it.
	/// A class that also has the <see cref="ParameterCollectionAttribute"/> is a leaf: the command that does the work,
	/// parsing the arguments that remain after the command names. A class without it is a group of commands
	/// (such as <c>directories</c> in <c>myFileManagingTool directories list</c>); its child commands are the properties whose type is
	/// a class with this attribute, and its own options come before the name of the next command.
	/// The class cannot be generic or abstract, and a group cannot have required members. A child command must be declared
	/// by the class itself: properties inherited from a base class are not child commands. Classes nested in other types
	/// are supported as long as the types around them are partial too.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
	public sealed class CommandAttribute : Attribute
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="CommandAttribute"/> class.
		/// </summary>
		/// <param name="name">The name of the command, as users type it. It cannot be empty, contain whitespace or start with '-'.</param>
		/// <param name="description">The description of the command, used in help text.</param>
		public CommandAttribute(string name, string description = "")
		{
			Name = name ?? throw new ArgumentNullException(nameof(name));
			Description = description ?? "";
		}

		/// <summary>
		/// Gets the name of the command.
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// Gets the description of the command to be used in help text.
		/// </summary>
		public string Description { get; }

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
	/// The method is a static method, of the class that declares the property or, when that class has no such method,
	/// of the type of the property. The class that declares the property is the only place for a type that cannot be extended, such as <c>int?</c>.
	/// When the property is nullable, T can be nullable as well, so the method can turn some text (such as <c>NULL</c>) into <c>null</c>.
	/// A required argument is given as soon as it is on the command line, whatever value the method returns for it.
	/// The generator reports ARG030 when it finds no such method. The attribute applies to options and positional arguments; a flag has no text to parse.
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
