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
	public class OptionToken : Token
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="OptionToken"/> class.
		/// </summary>
		/// <param name="name">The name of the option.</param>
		/// <param name="value">The value of the option.</param>
		public OptionToken(string name, string value)
		{
			Name = name;
			Value = value;
		}

		/// <summary>
		/// Gets the name of the option.
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// Gets the value of the option.
		/// </summary>
		public string Value { get; }
	}

	/// <summary>
	/// Represents a positional token with a position and value.
	/// </summary>
	public class PositionalToken : Token
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="PositionalToken"/> class.
		/// </summary>
		/// <param name="position">The position of the argument.</param>
		/// <param name="value">The value of the argument.</param>
		public PositionalToken(int position, string value)
		{
			Position = position;
			Value = value;
		}

		/// <summary>
		/// Gets the position of the argument.
		/// </summary>
		public int Position { get; }

		/// <summary>
		/// Gets the value of the argument.
		/// </summary>
		public string Value { get; }
	}

	/// <summary>
	/// Represents a flag token with a name.
	/// </summary>
	public class FlagToken : Token
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="FlagToken"/> class.
		/// </summary>
		/// <param name="name">The name of the flag.</param>
		public FlagToken(string name)
		{
			Name = name;
		}

		/// <summary>
		/// Gets the name of the flag.
		/// </summary>
		public string Name { get; }
	}

	/// <summary>
	/// Represents an unknown token with a name, position, and value.
	/// </summary>
	public class UnknownToken : Token
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="UnknownToken"/> class.
		/// </summary>
		/// <param name="name">The name of the token.</param>
		/// <param name="position">The position of the token.</param>
		/// <param name="value">The value of the token.</param>
		public UnknownToken(string name, int position, string value)
		{
			Name = name;
			Position = position;
			Value = value;
		}

		/// <summary>
		/// Gets the name of the token.
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// Gets the position of the token.
		/// </summary>
		public int Position { get; }

		/// <summary>
		/// Gets the value of the token.
		/// </summary>
		public string Value { get; }
	}

}