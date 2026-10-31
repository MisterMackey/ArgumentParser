namespace ArgumentParser.Tests;

/// <summary>
/// Builds complete source files for the behaviour tests that execute the generated parser.
/// </summary>
public static class ParserSource
{
	/// <summary>
	/// Wraps the members in a ParameterCollection class named TestClass.
	/// Defaults are chosen so that Parse never exits the test process:
	/// no help argument handler and no throwing on errors.
	/// With useCurrentCulture the attribute sets UseCurrentCulture = true.
	/// </summary>
	public static string Build(
		string members,
		string behaviourOnError = "ThrowNever",
		string helpTextGeneration = "GenerateTextOnly",
		string extraTypes = "",
		bool useCurrentCulture = false)
	{
		var culture = useCurrentCulture ? ", UseCurrentCulture = true" : "";
		return $$"""
			using System;
			using ArgumentParser;

			namespace ArgumentParser.Tests;

			{{extraTypes}}

			[ParameterCollection(HelpTextGeneration.{{helpTextGeneration}}, "h", "Help", BehaviourOnError.{{behaviourOnError}}{{culture}})]
			public partial class TestClass
			{
			{{members}}
			}
			""";
	}
}
