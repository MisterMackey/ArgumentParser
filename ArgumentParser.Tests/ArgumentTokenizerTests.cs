using System;
using System.Linq;
using Xunit;

// TODO: fix errors in this code, proofread carefully
namespace ArgumentParser.Tests
{
	public class ArgumentTokenizerTests
	{
		private readonly ArgumentTokenizer _tokenizer;
		private readonly OptionAttribute[] _options;
		private readonly PositionalAttribute[] _positionals;
		private readonly FlagAttribute[] _flags;

		public ArgumentTokenizerTests()
		{
			_tokenizer = new ArgumentTokenizer();

			// default set of attributes is a valid set containing no required attributes
			_options = new[]
			{
				new OptionAttribute("o", "output", "Output file"),
				new OptionAttribute("i", "input", "Input file"),
			};

			_positionals = new[]
			{
				new PositionalAttribute(0, "Source file"),
				new PositionalAttribute(1, "Destination file")
			};

			_flags = new[]
			{
				new FlagAttribute("h", "help", "Show help"),
				new FlagAttribute("v", "verbose", "Enable verbose output"),
				new FlagAttribute("f", "force", "Force overwrite")
			};
		}

		[Fact]
		public void TokenizeArguments_EmptyArgs_ReturnsEmptyTokensAndErrors()
		{
			// Arrange
			string[] args = [];

			// Act
			var (tokens, errors) = _tokenizer.TokenizeArguments(args, _options, _positionals, _flags);

			// Assert
			Assert.Empty(tokens);
			Assert.Empty(errors);
		}

		[Fact]
		public void TokenizeArguments_NullArguments_ThrowsArgumentNullException()
		{
			// Arrange
			string[] validArgs = { "--output", "file.txt" };

			// Act & Assert
			Assert.Throws<ArgumentNullException>(() => _tokenizer.TokenizeArguments(null!, _options, _positionals, _flags));
			Assert.Throws<ArgumentNullException>(() => _tokenizer.TokenizeArguments(validArgs, (OptionAttribute[])null!, _positionals, _flags));
			Assert.Throws<ArgumentNullException>(() => _tokenizer.TokenizeArguments(validArgs, _options, (PositionalAttribute[])null!, _flags));
			Assert.Throws<ArgumentNullException>(() => _tokenizer.TokenizeArguments(validArgs, _options, _positionals, (FlagAttribute[])null!));
		}

		[Fact]
		public void TokenizeArguments_LongNameOption_ReturnsOptionToken()
		{
			// Arrange
			string[] args = ["--output", "file.txt"];

			// Act
			var (tokens, errors) = _tokenizer.TokenizeArguments(args, _options, _positionals, _flags);

			// Assert
			Assert.Single(tokens);
			Assert.Empty(errors);

			var token = tokens[0] as OptionToken;
			Assert.NotNull(token);
			Assert.Equal("output", token.Name);
			Assert.Equal("file.txt", token.Value);
		}

		[Fact]
		public void TokenizeArguments_ShortNameOption_ReturnsOptionToken()
		{
			// Arrange
			string[] args = ["-o", "file.txt"];

			// Act
			var (tokens, errors) = _tokenizer.TokenizeArguments(args, _options, _positionals, _flags);

			// Assert
			Assert.Single(tokens);
			Assert.Empty(errors);

			var token = tokens[0] as OptionToken;
			Assert.NotNull(token);
			Assert.Equal("o", token.Name);
			Assert.Equal("file.txt", token.Value);
		}

		[Fact]
		public void TokenizeArguments_LongNameFlag_ReturnsFlagToken()
		{
			// Arrange
			string[] args = ["--help"];

			// Act
			var (tokens, errors) = _tokenizer.TokenizeArguments(args, _options, _positionals, _flags);

			// Assert
			Assert.Single(tokens);
			Assert.Empty(errors);

			var token = tokens[0] as FlagToken;
			Assert.NotNull(token);
			// when both short and long names are present, the short name is used
			Assert.Equal("h", token.Name);
		}

		[Fact]
		public void TokenizeArguments_ShortNameFlag_ReturnsFlagToken()
		{
			// Arrange
			string[] args = ["-h"];

			// Act
			var (tokens, errors) = _tokenizer.TokenizeArguments(args, _options, _positionals, _flags);

			// Assert
			Assert.Single(tokens);
			Assert.Empty(errors);

			var token = tokens[0] as FlagToken;
			Assert.NotNull(token);
			Assert.Equal("h", token.Name);
		}

		[Fact]
		public void TokenizeArguments_MultipleShortFlags_ReturnsMultipleFlagTokens()
		{
			// Arrange
			string[] args = ["-hf"];  // Combined short flags

			// Act
			var (tokens, errors) = _tokenizer.TokenizeArguments(args, _options, _positionals, _flags);

			// Assert
			Assert.Equal(2, tokens.Count);
			Assert.Empty(errors);

			var firstToken = tokens[0] as FlagToken;
			Assert.NotNull(firstToken);
			Assert.Equal("h", firstToken.Name);

			var secondToken = tokens[1] as FlagToken;
			Assert.NotNull(secondToken);
			Assert.Equal("f", secondToken.Name);
		}

		[Fact]
		public void TokenizeArguments_PositionalArguments_ReturnsPositionalTokens()
		{
			// Arrange
			string[] args = { "source.txt", "dest.txt" };

			// Act
			var (tokens, errors) = _tokenizer.TokenizeArguments(args, _options, _positionals, _flags);

			// Assert
			Assert.Equal(2, tokens.Count);
			Assert.Empty(errors);

			var firstToken = tokens[0] as PositionalToken;
			Assert.NotNull(firstToken);
			Assert.Equal(0, firstToken.Position);
			Assert.Equal("source.txt", firstToken.Value);

			var secondToken = tokens[1] as PositionalToken;
			Assert.NotNull(secondToken);
			Assert.Equal(1, secondToken.Position);
			Assert.Equal("dest.txt", secondToken.Value);
		}

		[Fact]
		public void TokenizeArguments_MixedArguments_ReturnsAllTokens()
		{
			// Arrange
			string[] args = { "-h", "source.txt", "--output", "out.txt", "dest.txt" };

			// Act
			var (tokens, errors) = _tokenizer.TokenizeArguments(args, _options, _positionals, _flags);

			// Assert
			Assert.Equal(4, tokens.Count);
			Assert.Empty(errors);

			Assert.IsType<PositionalToken>(tokens[0]);
			var firstPositionalToken = tokens[0] as PositionalToken;
			Assert.NotNull(firstPositionalToken);
			Assert.Equal("source.txt", firstPositionalToken.Value);
			Assert.IsType<OptionToken>(tokens[1]);
			var optionToken = tokens[1] as OptionToken;
			Assert.NotNull(optionToken);
			Assert.Equal("out.txt", optionToken.Value);
			Assert.IsType<PositionalToken>(tokens[2]);
			var secondPositionalToken = tokens[2] as PositionalToken;
			Assert.NotNull(secondPositionalToken);
			Assert.Equal("dest.txt", secondPositionalToken.Value);
			// flag tokens are always at the end
			Assert.IsType<FlagToken>(tokens[3]);
		}

		[Fact]
		public void TokenizeArguments_UnknownFlag_AddsError()
		{
			// Arrange
			string[] args = ["-x"];

			// Act
			var (tokens, errors) = _tokenizer.TokenizeArguments(args, _options, _positionals, _flags);

			// Assert
			Assert.Empty(tokens);
			Assert.Single(errors);
			Assert.IsType<UnexpectedArgumentException>(errors[0]);
			Assert.Contains("Unknown flag '-x'", errors[0].Message);
		}

		[Fact]
		public void TokenizeArguments_UnknownLongOption_AddsError()
		{
			// Arrange
			string[] args = ["--unknown"];

			// Act
			var (tokens, errors) = _tokenizer.TokenizeArguments(args, _options, _positionals, _flags);

			// Assert
			Assert.Empty(tokens);
			Assert.Single(errors);
			Assert.IsType<UnexpectedArgumentException>(errors[0]);
			Assert.Contains("Unknown argument '--unknown'", errors[0].Message);
		}

		[Fact]
		public void TokenizeArguments_MissingOptionValue_AddsError()
		{
			// Arrange
			string[] args = ["--output"]; // Missing required value

			// Act
			var (tokens, errors) = _tokenizer.TokenizeArguments(args, _options, _positionals, _flags);

			// Assert
			Assert.Empty(tokens);
			Assert.Single(errors);
			Assert.IsType<MissingRequiredArgumentException>(errors[0]);
			Assert.Contains("requires a value", errors[0].Message);
		}

		[Fact]
		public void TokenizeArguments_TooManyPositionals_AddsError()
		{
			// Arrange
			string[] args = ["first.txt", "second.txt", "third.txt"]; // One too many

			// Act
			var (tokens, errors) = _tokenizer.TokenizeArguments(args, _options, _positionals, _flags);

			// Assert
			Assert.Equal(2, tokens.Count); // Only 2 valid positional tokens
			Assert.Single(errors);
			Assert.IsType<UnexpectedArgumentException>(errors[0]);
			Assert.Contains("Unexpected positional argument", errors[0].Message);
		}

		[Fact]
		public void TokenizeArguments_MixedOptionsAndFlags_ReturnsCorrectTokens()
		{
			// Arrange
			string[] args = ["-hfo", "output.txt"];

			// Act
			var (tokens, errors) = _tokenizer.TokenizeArguments(args, _options, _positionals, _flags);

			// Assert
			Assert.Equal(3, tokens.Count);
			Assert.Empty(errors);

			Assert.IsType<OptionToken>(tokens[0]);
			var optionToken = tokens[0] as OptionToken;
			Assert.NotNull(optionToken);
			Assert.Equal("output.txt", optionToken.Value);
			Assert.IsType<FlagToken>(tokens[1]);
			Assert.IsType<FlagToken>(tokens[2]);
		}

		[Fact]
		public void TokenizeArguments_OptionInMiddleOfShortFlags_AddsError()
		{
			// Arrange
			string[] args = ["-hof"]; // Option 'o' in the middle of flags

			// Act
			var (tokens, errors) = _tokenizer.TokenizeArguments(args, _options, _positionals, _flags);

			// Assert
			Assert.Equal(2, tokens.Count);
			Assert.Single(errors);
			Assert.Contains("Unexpected flag '-o'", errors[0].Message);
		}

		[Theory]
		[InlineData("-v", 1)]
		[InlineData("-vv", 2)]
		[InlineData("-vvv", 3)]
		[InlineData("-vv --verbose", 3)]
		public void TokenizeArguments_FlagLevel_ParsesCorrectly(string arg, int expectedCount)
		{
			// Arrange
			string[] args = arg.Split(' ');
			var flags = new[]
			{
				new FlagAttribute("v", "verbose", "verbosity"),
			};
			var options = Array.Empty<OptionAttribute>();
			var positionals = Array.Empty<PositionalAttribute>();

			// Act
			var (tokens, errors) = _tokenizer.TokenizeArguments(args, options, positionals, flags);

			// Assert
			Assert.Single(tokens);
			Assert.Empty(errors);
			var flagToken = tokens[0] as FlagToken;
			Assert.NotNull(flagToken);
			Assert.Equal("v", flagToken.Name);
			Assert.Equal(expectedCount, flagToken.Level);
		}

		[Theory]
		[InlineData("--verbose", 1)]
		[InlineData("--verbose --verbose", 2)]
		public void TokenizeArguments_FlagWithOnlyLongName_KeepsName(string arg, int expectedCount)
		{
			// explanation:
			// the tokenizer attempts to use only short names for flags to make counting the levels easier,
			// but if a flag has no short name, it should still be recognized by its long name. (and levels should work)
			// Arrange
			string[] args = arg.Split(' ');
			var flags = new[]
			{
				new FlagAttribute("", "verbose", "verbosity"),
			};
			var options = Array.Empty<OptionAttribute>();
			var positionals = Array.Empty<PositionalAttribute>();

			// Act
			var (tokens, errors) = _tokenizer.TokenizeArguments(args, options, positionals, flags);

			// Assert
			Assert.Single(tokens);
			Assert.Empty(errors);
			var flagToken = tokens[0] as FlagToken;
			Assert.NotNull(flagToken);
			Assert.Equal("verbose", flagToken.Name);
			Assert.Equal(expectedCount, flagToken.Level);
		}
	}
}