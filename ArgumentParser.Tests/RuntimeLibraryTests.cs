namespace ArgumentParser.Tests;

/// <summary>
/// Tests for the types in the runtime library that the generated code and user code reference directly:
/// the attributes (constructor validation and stored values), the exception hierarchy and the tokens.
/// </summary>
public class RuntimeLibraryTests
{
	#region ParameterCollectionAttribute

	[Fact]
	public void ParameterCollection_Defaults_AreGenerateAllHelpAndDisplayHelpAndExit()
	{
		var attribute = new ParameterCollectionAttribute();

		Assert.Equal(HelpTextGeneration.GenerateAll, attribute.HelpTextGeneration);
		Assert.Equal("h", attribute.HelpArgumentShortName);
		Assert.Equal("Help", attribute.HelpArgumentLongName);
		Assert.Equal(BehaviourOnError.DisplayHelpAndExit, attribute.BehaviourOnError);
	}

	[Fact]
	public void ParameterCollection_StoresEveryArgument()
	{
		var attribute = new ParameterCollectionAttribute(HelpTextGeneration.None, "?", "Assist", BehaviourOnError.ThrowNever);

		Assert.Equal(HelpTextGeneration.None, attribute.HelpTextGeneration);
		Assert.Equal("?", attribute.HelpArgumentShortName);
		Assert.Equal("Assist", attribute.HelpArgumentLongName);
		Assert.Equal(BehaviourOnError.ThrowNever, attribute.BehaviourOnError);
	}

	[Fact]
	public void ParameterCollection_IsRestrictedToClasses_AndNotInherited()
	{
		var usage = GetUsage(typeof(ParameterCollectionAttribute));

		Assert.Equal(AttributeTargets.Class, usage.ValidOn);
		Assert.False(usage.Inherited);
	}

	#endregion

	#region OptionAttribute and FlagAttribute

	[Fact]
	public void Option_StoresEveryArgument()
	{
		var attribute = new OptionAttribute("o", "output", "Where to write", true);

		Assert.Equal("o", attribute.ShortName);
		Assert.Equal("output", attribute.LongName);
		Assert.Equal("Where to write", attribute.Description);
		Assert.True(attribute.Required);
	}

	[Fact]
	public void Option_OnlyLongName_LeavesShortNameEmpty_AndIsOptionalByDefault()
	{
		var attribute = new OptionAttribute(longName: "output");

		Assert.Equal("", attribute.ShortName);
		Assert.Equal("output", attribute.LongName);
		Assert.Equal("", attribute.Description);
		Assert.False(attribute.Required);
	}

	[Fact]
	public void Option_OnlyShortName_LeavesLongNameEmpty()
	{
		var attribute = new OptionAttribute("o");

		Assert.Equal("o", attribute.ShortName);
		Assert.Equal("", attribute.LongName);
	}

	[Theory]
	[InlineData("", "")]
	[InlineData(null, null)]
	[InlineData("", null)]
	[InlineData(null, "")]
	public void Option_WithoutAnyName_Throws(string? shortName, string? longName)
	{
		var exception = Assert.Throws<ArgumentException>(() => new OptionAttribute(shortName!, longName!));

		Assert.Contains("shortName or longName", exception.Message);
	}

	[Theory]
	[InlineData("ab")]
	[InlineData("verbose")]
	public void Option_ShortNameLongerThanOneCharacter_Throws(string shortName)
	{
		var exception = Assert.Throws<ArgumentException>(() => new OptionAttribute(shortName, "long"));

		Assert.Contains("single character", exception.Message);
	}

	[Fact]
	public void Option_NullLongNameWithShortName_IsStoredAsEmpty()
	{
		var attribute = new OptionAttribute("o", null!);

		Assert.Equal("", attribute.LongName);
	}

	[Fact]
	public void Flag_StoresEveryArgument()
	{
		var attribute = new FlagAttribute("v", "verbose", "Talk more");

		Assert.Equal("v", attribute.ShortName);
		Assert.Equal("verbose", attribute.LongName);
		Assert.Equal("Talk more", attribute.Description);
	}

	[Fact]
	public void Flag_OnlyOneName_LeavesTheOtherEmpty()
	{
		Assert.Equal("", new FlagAttribute("v").LongName);
		Assert.Equal("", new FlagAttribute(longName: "verbose").ShortName);
	}

	[Theory]
	[InlineData("", "")]
	[InlineData(null, null)]
	[InlineData("", null)]
	[InlineData(null, "")]
	public void Flag_WithoutAnyName_Throws(string? shortName, string? longName)
	{
		var exception = Assert.Throws<ArgumentException>(() => new FlagAttribute(shortName!, longName!));

		Assert.Contains("shortName or longName", exception.Message);
	}

	[Theory]
	[InlineData("ab")]
	[InlineData("verbose")]
	public void Flag_ShortNameLongerThanOneCharacter_Throws(string shortName)
	{
		var exception = Assert.Throws<ArgumentException>(() => new FlagAttribute(shortName, "long"));

		Assert.Contains("single character", exception.Message);
	}

	[Fact]
	public void Flag_NullLongNameWithShortName_IsStoredAsEmpty()
	{
		var attribute = new FlagAttribute("v", null!);

		Assert.Equal("", attribute.LongName);
	}

	#endregion

	#region PositionalAttribute and ParsedWithMethodAttribute

	[Fact]
	public void Positional_StoresEveryArgument()
	{
		var attribute = new PositionalAttribute(2, "Third thing", true);

		Assert.Equal(2, attribute.Position);
		Assert.Equal("Third thing", attribute.Description);
		Assert.True(attribute.Required);
	}

	[Fact]
	public void Positional_IsOptionalByDefault()
	{
		Assert.False(new PositionalAttribute(0, "First").Required);
	}

	[Fact]
	public void ParsedWithMethod_StoresTheMethodName()
	{
		Assert.Equal("TryParse", new ParsedWithMethodAttribute("TryParse").MethodName);
	}

	[Fact]
	public void ParsedWithMethod_NullMethodName_Throws()
	{
		var exception = Assert.Throws<ArgumentNullException>(() => new ParsedWithMethodAttribute(null!));

		Assert.Equal("methodName", exception.ParamName);
	}

	[Theory]
	[InlineData(typeof(OptionAttribute))]
	[InlineData(typeof(PositionalAttribute))]
	[InlineData(typeof(FlagAttribute))]
	[InlineData(typeof(ParsedWithMethodAttribute))]
	public void PropertyAttributes_AreRestrictedToProperties_AndNotInherited(Type attributeType)
	{
		var usage = GetUsage(attributeType);

		Assert.Equal(AttributeTargets.Property, usage.ValidOn);
		Assert.False(usage.Inherited);
		Assert.False(usage.AllowMultiple);
	}

	private static AttributeUsageAttribute GetUsage(Type attributeType)
	{
		return attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
			.Cast<AttributeUsageAttribute>()
			.Single();
	}

	#endregion

	#region Exceptions

	public static TheoryData<Type> ConcreteExceptionTypes =>
	[
		typeof(UnexpectedArgumentException),
		typeof(MissingRequiredArgumentException),
		typeof(InvalidArgumentValueException),
		typeof(InvalidTokenTypeException),
	];

	[Theory]
	[MemberData(nameof(ConcreteExceptionTypes))]
	public void Exception_DerivesFromArgumentParserException(Type type)
	{
		Assert.True(typeof(ArgumentParserException).IsAssignableFrom(type));
		Assert.True(typeof(Exception).IsAssignableFrom(type));
	}

	[Fact]
	public void ArgumentParserException_IsAbstract_WithOnlyProtectedConstructors()
	{
		var type = typeof(ArgumentParserException);

		Assert.True(type.IsAbstract);
		Assert.Empty(type.GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance));
	}

	[Theory]
	[MemberData(nameof(ConcreteExceptionTypes))]
	public void Exception_ParameterlessConstructor_HasADefaultMessageAndNoInnerException(Type type)
	{
		var exception = (Exception)Activator.CreateInstance(type)!;

		Assert.False(string.IsNullOrEmpty(exception.Message));
		Assert.Null(exception.InnerException);
	}

	[Theory]
	[MemberData(nameof(ConcreteExceptionTypes))]
	public void Exception_MessageConstructor_KeepsTheMessage(Type type)
	{
		var exception = (Exception)Activator.CreateInstance(type, "something went wrong")!;

		Assert.Equal("something went wrong", exception.Message);
		Assert.Null(exception.InnerException);
	}

	[Theory]
	[MemberData(nameof(ConcreteExceptionTypes))]
	public void Exception_MessageAndInnerConstructor_KeepsBoth(Type type)
	{
		var inner = new InvalidOperationException("the cause");

		var exception = (Exception)Activator.CreateInstance(type, "something went wrong", inner)!;

		Assert.Equal("something went wrong", exception.Message);
		Assert.Same(inner, exception.InnerException);
	}

	[Fact]
	public void Exceptions_CanBeCaughtAsTheBaseType()
	{
		Assert.Throws<UnexpectedArgumentException>(() => Throw(new UnexpectedArgumentException("x")));

		var caught = Record.Exception(() => Throw(new MissingRequiredArgumentException("y")));
		Assert.IsAssignableFrom<ArgumentParserException>(caught);
	}

	private static void Throw(ArgumentParserException exception) => throw exception;

	#endregion

	#region Tokens

	[Fact]
	public void OptionToken_StoresNameValueAndShortness()
	{
		var token = new OptionToken("o", "out.txt", isShort: true);

		Assert.Equal("o", token.Name);
		Assert.Equal("out.txt", token.Value);
		Assert.True(token.IsShort);
		Assert.False(new OptionToken("output", "out.txt").IsShort);
	}

	[Fact]
	public void FlagToken_StoresNameLevelAndShortness()
	{
		var token = new FlagToken("v", 3, isShort: true);

		Assert.Equal("v", token.Name);
		Assert.Equal(3, token.Level);
		Assert.True(token.IsShort);
		Assert.False(new FlagToken("verbose", 1).IsShort);
	}

	[Fact]
	public void PositionalToken_StoresPositionAndValue_AndIsNeverShort()
	{
		var token = new PositionalToken(4, "file.txt");

		Assert.Equal(4, token.Position);
		Assert.Equal("file.txt", token.Value);
		Assert.False(token.IsShort);
	}

	[Fact]
	public void UnknownToken_StoresNamePositionAndValue_AndIsNeverShort()
	{
		var token = new UnknownToken("mystery", 7, "value");

		Assert.Equal("mystery", token.Name);
		Assert.Equal(7, token.Position);
		Assert.Equal("value", token.Value);
		Assert.False(token.IsShort);
	}

	[Fact]
	public void AllTokens_DeriveFromToken()
	{
		Assert.IsAssignableFrom<Token>(new OptionToken("o", "v"));
		Assert.IsAssignableFrom<Token>(new FlagToken("f", 1));
		Assert.IsAssignableFrom<Token>(new PositionalToken(0, "v"));
		Assert.IsAssignableFrom<Token>(new UnknownToken("u", 0, "v"));
	}

	#endregion
}
