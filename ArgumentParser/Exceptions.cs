using System;

namespace ArgumentParser
{

	/// <summary>
	/// Base exception class for all argument parser exceptions.
	/// </summary>
	public abstract class ArgumentParserException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ArgumentParserException"/> class.
		/// </summary>
		protected ArgumentParserException() : base()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ArgumentParserException"/> class.
		/// </summary>
		/// <param name="message">The error message associated with the exception.</param>
		protected ArgumentParserException(string message) : base(message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ArgumentParserException"/> class with a specified error message and a reference to the inner exception.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception.</param>
		protected ArgumentParserException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}

	/// <summary>
	/// Exception thrown when an unexpected argument is encountered.
	/// </summary>
	public class UnexpectedArgumentException : ArgumentParserException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="UnexpectedArgumentException"/> class.
		/// </summary>
		public UnexpectedArgumentException() : base()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="UnexpectedArgumentException"/> class.
		/// </summary>
		/// <param name="message">The error message associated with the exception.</param>
		public UnexpectedArgumentException(string message) : base(message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="UnexpectedArgumentException"/> class with a specified error message and a reference to the inner exception.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception.</param>
		public UnexpectedArgumentException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}

	/// <summary>
	/// Exception thrown when a required argument is missing.
	/// </summary>
	public class MissingRequiredArgumentException : ArgumentParserException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MissingRequiredArgumentException"/> class.
		/// </summary>
		public MissingRequiredArgumentException() : base()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MissingRequiredArgumentException"/> class.
		/// </summary>
		/// <param name="message">The error message associated with the exception.</param>
		public MissingRequiredArgumentException(string message) : base(message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MissingRequiredArgumentException"/> class with a specified error message and a reference to the inner exception.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception.</param>
		public MissingRequiredArgumentException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}

	/// <summary>
	/// Exception thrown when an argument has an invalid value.
	/// </summary>
	public class InvalidArgumentValueException : ArgumentParserException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="InvalidArgumentValueException"/> class.
		/// </summary>
		public InvalidArgumentValueException() : base()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="InvalidArgumentValueException"/> class.
		/// </summary>
		/// <param name="message">The error message associated with the exception.</param>
		public InvalidArgumentValueException(string message) : base(message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="InvalidArgumentValueException"/> class with a specified error message and a reference to the inner exception.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception.</param>
		public InvalidArgumentValueException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}

	/// <summary>
	/// Exception thrown when a token has an invalid type.
	/// </summary>
	public class InvalidTokenTypeException : ArgumentParserException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="InvalidTokenTypeException"/> class.
		/// </summary>
		public InvalidTokenTypeException() : base()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="InvalidTokenTypeException"/> class.
		/// </summary>
		/// <param name="message">The error message associated with the exception.</param>
		public InvalidTokenTypeException(string message) : base(message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="InvalidTokenTypeException"/> class with a specified error message and a reference to the inner exception.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception.</param>
		public InvalidTokenTypeException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}

}