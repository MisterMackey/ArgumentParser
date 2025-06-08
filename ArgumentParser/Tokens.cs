namespace ArgumentParser
{

	/// <summary>
	/// Base class for all tokens.
	/// </summary>
	public abstract class Token
	{
	}

	/// <summary>
	/// Represents an option token with a name and value.
	/// </summary>
	/// <remarks>
	/// Initializes a new instance of the <see cref="OptionToken"/> class.
	/// </remarks>
	/// <param name="name">The name of the option.</param>
	/// <param name="value">The value of the option.</param>
	public class OptionToken(string name, string value) : Token
	{

		/// <summary>
		/// Gets the name of the option.
		/// </summary>
		public string Name { get; } = name;

		/// <summary>
		/// Gets the value of the option.
		/// </summary>
		public string Value { get; } = value;
	}

	/// <summary>
	/// Represents a positional token with a position and value.
	/// </summary>
	/// <remarks>
	/// Initializes a new instance of the <see cref="PositionalToken"/> class.
	/// </remarks>
	/// <param name="position">The position of the argument.</param>
	/// <param name="value">The value of the argument.</param>
	public class PositionalToken(int position, string value) : Token
	{

		/// <summary>
		/// Gets the position of the argument.
		/// </summary>
		public int Position { get; } = position;

		/// <summary>
		/// Gets the value of the argument.
		/// </summary>
		public string Value { get; } = value;
	}

	/// <summary>
	/// Represents a flag token with a name.
	/// </summary>
	/// <remarks>
	/// Initializes a new instance of the <see cref="FlagToken"/> class.
	/// </remarks>
	/// <param name="name">The name of the flag.</param>
	/// <param name="level">The level of the flag, or null if this flag has no levels.</param>
	public class FlagToken(string name, int? level = null) : Token
	{

		/// <summary>
		/// Gets the name of the flag.
		/// </summary>
		public string Name { get; } = name;

		/// <summary>
		/// Get the level of the flag, or null if this flag has no levels.
		/// /// </summary>
		public int? Level { get; } = level;
	}

	/// <summary>
	/// Represents an unknown token with a name, position, and value.
	/// </summary>
	/// <remarks>
	/// Initializes a new instance of the <see cref="UnknownToken"/> class.
	/// </remarks>
	/// <param name="name">The name of the token.</param>
	/// <param name="position">The position of the token.</param>
	/// <param name="value">The value of the token.</param>
	public class UnknownToken(string name, int position, string value) : Token
	{

		/// <summary>
		/// Gets the name of the token.
		/// </summary>
		public string Name { get; } = name;

		/// <summary>
		/// Gets the position of the token.
		/// </summary>
		public int Position { get; } = position;

		/// <summary>
		/// Gets the value of the token.
		/// </summary>
		public string Value { get; } = value;
	}

}