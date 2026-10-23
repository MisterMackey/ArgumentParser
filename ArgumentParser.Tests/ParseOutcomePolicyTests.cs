namespace ArgumentParser.Tests;

/// <summary>
/// Tests for the policy the generated Parse methods apply to the outcome of a parse.
/// The exit is recorded instead of ending the process, so a regression is an assertion failure and not a crashed test host.
/// </summary>
public class ParseOutcomePolicyTests
{
	private const string HelpText = "the help text";

	private static readonly ArgumentParserException[] NoErrors = [];

	private static readonly ArgumentParserException Invalid = new InvalidArgumentValueException("Invalid value for Number: abc");

	private static readonly ArgumentParserException Missing = new MissingRequiredArgumentException("Missing required argument: Out");

	private sealed class Recorder
	{
		public StringWriter Output { get; } = new StringWriter();

		public List<int> ExitCodes { get; } = [];

		public void Exit(int code) => ExitCodes.Add(code);

		public void Apply(
			BehaviourOnError behaviour,
			bool helpRequested = false,
			string? helpText = HelpText,
			IReadOnlyList<ArgumentParserException>? errors = null,
			IReadOnlyList<ArgumentParserException>? missingRequired = null)
		{
			ParseOutcomePolicy.Apply(behaviour, helpRequested, helpText, errors ?? NoErrors, missingRequired ?? NoErrors, Output, Exit);
		}
	}

	private static string Lines(params string[] lines)
	{
		return string.Concat(lines.Select(line => line + Environment.NewLine));
	}

	[Theory]
	[InlineData(BehaviourOnError.DisplayHelpAndExit)]
	[InlineData(BehaviourOnError.ThrowIfAnyError)]
	[InlineData(BehaviourOnError.ThrowIfMissingRequired)]
	[InlineData(BehaviourOnError.ThrowNever)]
	public void HelpRequested_DisplaysTheHelpTextAndExitsWithZero_WhateverTheBehaviour(BehaviourOnError behaviour)
	{
		var recorder = new Recorder();

		// the errors are ignored: asking for help wins, and nothing is thrown
		recorder.Apply(behaviour, helpRequested: true, errors: [Invalid, Missing], missingRequired: [Missing]);

		Assert.Equal(Lines(HelpText), recorder.Output.ToString());
		Assert.Equal([ParseOutcomePolicy.HelpExitCode], recorder.ExitCodes);
		Assert.Equal(0, ParseOutcomePolicy.HelpExitCode);
	}

	[Fact]
	public void DisplayHelpAndExit_WithErrors_DisplaysTheErrorsThenTheHelpTextAndExitsWithTwo()
	{
		var recorder = new Recorder();

		recorder.Apply(BehaviourOnError.DisplayHelpAndExit, errors: [Invalid, Missing], missingRequired: [Missing]);

		Assert.Equal(Lines(Invalid.ToString(), Missing.ToString(), "", HelpText), recorder.Output.ToString());
		Assert.Equal([ParseOutcomePolicy.ErrorExitCode], recorder.ExitCodes);
		Assert.Equal(2, ParseOutcomePolicy.ErrorExitCode);
	}

	[Fact]
	public void DisplayHelpAndExit_WithoutErrors_DoesNothing()
	{
		var recorder = new Recorder();

		recorder.Apply(BehaviourOnError.DisplayHelpAndExit);

		Assert.Equal(string.Empty, recorder.Output.ToString());
		Assert.Empty(recorder.ExitCodes);
	}

	[Fact]
	public void ThrowIfAnyError_WithErrors_ThrowsAnAggregateOfAllOfThem()
	{
		var recorder = new Recorder();

		var ex = Assert.Throws<AggregateException>(() =>
			recorder.Apply(BehaviourOnError.ThrowIfAnyError, errors: [Invalid, Missing], missingRequired: [Missing]));

		Assert.Equal("One or more errors occurred during parsing", ex.Message.Split(" (")[0]);
		Assert.Equal([Invalid, Missing], ex.InnerExceptions);
		Assert.Equal(string.Empty, recorder.Output.ToString());
		Assert.Empty(recorder.ExitCodes);
	}

	[Fact]
	public void ThrowIfAnyError_WithoutErrors_DoesNothing()
	{
		var recorder = new Recorder();

		recorder.Apply(BehaviourOnError.ThrowIfAnyError);

		Assert.Empty(recorder.ExitCodes);
	}

	[Fact]
	public void ThrowIfMissingRequired_ThrowsAnAggregateOfTheMissingRequiredOnly()
	{
		var recorder = new Recorder();

		var ex = Assert.Throws<AggregateException>(() =>
			recorder.Apply(BehaviourOnError.ThrowIfMissingRequired, errors: [Invalid, Missing], missingRequired: [Missing]));

		Assert.Equal("One or more required arguments missing", ex.Message.Split(" (")[0]);
		Assert.Equal([Missing], ex.InnerExceptions);
	}

	[Fact]
	public void ThrowIfMissingRequired_DoesNotThrowForOtherErrors()
	{
		var recorder = new Recorder();

		recorder.Apply(BehaviourOnError.ThrowIfMissingRequired, errors: [Invalid]);

		Assert.Equal(string.Empty, recorder.Output.ToString());
		Assert.Empty(recorder.ExitCodes);
	}

	[Fact]
	public void ThrowNever_NeverThrowsOrExitsOrWrites()
	{
		var recorder = new Recorder();

		recorder.Apply(BehaviourOnError.ThrowNever, errors: [Invalid, Missing], missingRequired: [Missing]);

		Assert.Equal(string.Empty, recorder.Output.ToString());
		Assert.Empty(recorder.ExitCodes);
	}

	[Fact]
	public void ExitIsCalledOnceEvenIfItReturns()
	{
		var recorder = new Recorder();

		// a real exit never returns, an injected one does; the policy must not carry on and exit a second time
		recorder.Apply(BehaviourOnError.DisplayHelpAndExit, helpRequested: true, errors: [Invalid]);

		Assert.Single(recorder.ExitCodes);
		Assert.Equal(Lines(HelpText), recorder.Output.ToString());
	}

	[Fact]
	public void TheHelpTextIsOnlyRequiredWhenItIsDisplayed()
	{
		var recorder = new Recorder();

		recorder.Apply(BehaviourOnError.ThrowNever, helpText: null, errors: [Invalid]);
		Assert.Throws<ArgumentNullException>(() => recorder.Apply(BehaviourOnError.ThrowNever, helpRequested: true, helpText: null));
		Assert.Throws<ArgumentNullException>(() => recorder.Apply(BehaviourOnError.DisplayHelpAndExit, helpText: null, errors: [Invalid]));
	}

	[Fact]
	public void UnknownBehaviour_IsRejected()
	{
		var recorder = new Recorder();

		Assert.Throws<ArgumentOutOfRangeException>(() => recorder.Apply((BehaviourOnError)99));
	}

	[Fact]
	public void NullArguments_AreRejected()
	{
		var output = new StringWriter();

		Assert.Throws<ArgumentNullException>(() => ParseOutcomePolicy.Apply(BehaviourOnError.ThrowNever, false, null, null!, NoErrors, output, _ => { }));
		Assert.Throws<ArgumentNullException>(() => ParseOutcomePolicy.Apply(BehaviourOnError.ThrowNever, false, null, NoErrors, null!, output, _ => { }));
		Assert.Throws<ArgumentNullException>(() => ParseOutcomePolicy.Apply(BehaviourOnError.ThrowNever, false, null, NoErrors, NoErrors, null!, _ => { }));
		Assert.Throws<ArgumentNullException>(() => ParseOutcomePolicy.Apply(BehaviourOnError.ThrowNever, false, null, NoErrors, NoErrors, output, null!));
	}

	[Fact]
	public void TheShortOverload_AppliesThePolicyToTheConsole()
	{
		// only the paths that neither write to the console nor exit can be run in the test host
		ParseOutcomePolicy.Apply(BehaviourOnError.ThrowNever, false, null, [Invalid], [Missing]);
		ParseOutcomePolicy.Apply(BehaviourOnError.DisplayHelpAndExit, false, HelpText, NoErrors, NoErrors);

		var ex = Assert.Throws<AggregateException>(() =>
			ParseOutcomePolicy.Apply(BehaviourOnError.ThrowIfAnyError, false, null, [Invalid], NoErrors));
		Assert.Equal([Invalid], ex.InnerExceptions);
	}
}
