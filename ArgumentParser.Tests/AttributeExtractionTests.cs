using System.Collections.ObjectModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ArgumentParser.Internal;
using ArgumentParser.Internal.Utilities;

namespace ArgumentParser.Tests;

/// <summary>
/// Tests for the step between the user's source and the generator: reading the attribute arguments off the
/// properties (AttributeFactory), the value semantics of the info types, and reading the [ParameterCollection]
/// settings off the class (GeneratorConfiguration).
/// </summary>
public class AttributeExtractionTests
{
	private const string Preamble = """
		using System;
		using ArgumentParser;

		namespace Extraction;

		public enum Colour { Red, Green }

		public class Custom
		{
			public static bool TryParse(string input, out Custom result) { result = new Custom(); return true; }
		}

		""";

	private static (SemanticModel Model, ClassDeclarationSyntax Class) Compile(string classAttribute, string members, string extraTypes = "")
	{
		var source = $$"""
			{{Preamble}}
			{{extraTypes}}

			{{classAttribute}}
			public partial class Target
			{
			{{members}}
			}
			""";
		var compilation = TestHelper.CreateCompilation(source);
		var tree = compilation.SyntaxTrees.Single();
		var declaration = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.Text == "Target");
		return (compilation.GetSemanticModel(tree), declaration);
	}

	private static AttributeFactory Factory(string members, string extraTypes = "")
	{
		var (model, declaration) = Compile("[ParameterCollection]", members, extraTypes);
		var properties = declaration.Members.OfType<PropertyDeclarationSyntax>().ToList();
		return new AttributeFactory(model, new ReadOnlyCollection<PropertyDeclarationSyntax>(properties));
	}

	private static GeneratorConfiguration Configuration(string classAttribute)
	{
		var (model, declaration) = Compile(classAttribute, "");
		return new GeneratorConfiguration(declaration, model);
	}

	#region AttributeFactory construction

	[Fact]
	public void Factory_NullSemanticModel_Throws()
	{
		Assert.Throws<ArgumentNullException>(() =>
			new AttributeFactory(null!, new ReadOnlyCollection<PropertyDeclarationSyntax>([])));
	}

	[Fact]
	public void Factory_NoProperties_ReturnsNothing()
	{
		var factory = Factory("");

		Assert.Empty(factory.GetOptionAttributes());
		Assert.Empty(factory.GetFlagAttributes());
		Assert.Empty(factory.GetPositionalAttributes());
	}

	[Fact]
	public void Factory_PropertiesWithoutParameterAttributes_AreIgnored()
	{
		var factory = Factory("""
			public string Plain { get; set; } = "";
			[Obsolete] public string Obsolete { get; set; } = "";
			""");

		Assert.Empty(factory.GetOptionAttributes());
		Assert.Empty(factory.GetFlagAttributes());
		Assert.Empty(factory.GetPositionalAttributes());
	}

	[Fact]
	public void Factory_SortsEachPropertyIntoItsOwnKind()
	{
		var factory = Factory("""
			[Option("o")] public string Output { get; set; } = "";
			[Flag("v")] public bool Verbose { get; set; }
			[Positional(0, "Source")] public string Source { get; set; } = "";
			""");

		Assert.Equal(["Output"], factory.GetOptionAttributes().Select(p => p.PropertyName));
		Assert.Equal(["Verbose"], factory.GetFlagAttributes().Select(p => p.PropertyName));
		Assert.Equal(["Source"], factory.GetPositionalAttributes().Select(p => p.PropertyName));
	}

	[Fact]
	public void Factory_KeepsDeclarationOrder()
	{
		var options = Factory("""
			[Option("c")] public string Third { get; set; } = "";
			[Option("a")] public string First { get; set; } = "";
			[Option("b")] public string Second { get; set; } = "";
			""").GetOptionAttributes();

		Assert.Equal(["Third", "First", "Second"], options.Select(p => p.PropertyName));
	}

	#endregion

	#region Options

	[Fact]
	public void Options_PositionalArguments_AreExtracted()
	{
		var option = Assert.Single(Factory("""[Option("o", "output", "Where to write", true)] public string Output { get; set; } = "";""").GetOptionAttributes());

		Assert.Equal("o", option.Attribute.ShortName);
		Assert.Equal("output", option.Attribute.LongName);
		Assert.Equal("Where to write", option.Attribute.Description);
		Assert.True(option.Attribute.Required);
		Assert.Equal(-1, option.Attribute.Position);
		Assert.Equal("Output", option.PropertyName);
		Assert.Equal("string", option.PropertyType);
		Assert.False(option.IsEnum);
		Assert.False(option.HasParseMethod);
	}

	[Fact]
	public void Options_OmittedArguments_TakeTheirDefaults()
	{
		var option = Assert.Single(Factory("""[Option("o")] public string Output { get; set; } = "";""").GetOptionAttributes());

		Assert.Equal("o", option.Attribute.ShortName);
		Assert.Equal("", option.Attribute.LongName);
		Assert.Equal("", option.Attribute.Description);
		Assert.False(option.Attribute.Required);
	}

	[Fact]
	public void Options_NamedArguments_InAnyOrder_AreExtracted()
	{
		var option = Assert.Single(Factory("""[Option(required: true, description: "Where", longName: "output")] public string Output { get; set; } = "";""").GetOptionAttributes());

		Assert.Equal("", option.Attribute.ShortName);
		Assert.Equal("output", option.Attribute.LongName);
		Assert.Equal("Where", option.Attribute.Description);
		Assert.True(option.Attribute.Required);
	}

	[Fact]
	public void Options_ConstantExpressions_AreEvaluated()
	{
		var option = Assert.Single(Factory("""
			private const string Short = "o";
			private const string Long = "out";
			[Option(Short, Long + "put", $"{Short} or {Long}", Short == "o")] public string Output { get; set; } = "";
			""").GetOptionAttributes());

		Assert.Equal("o", option.Attribute.ShortName);
		Assert.Equal("output", option.Attribute.LongName);
		Assert.Equal("o or out", option.Attribute.Description);
		Assert.True(option.Attribute.Required);
	}

	[Fact]
	public void Options_AttributeSuffixSpelledOut_IsStillFound()
	{
		var option = Assert.Single(Factory("""[OptionAttribute("o")] public string Output { get; set; } = "";""").GetOptionAttributes());

		Assert.Equal("o", option.Attribute.ShortName);
	}

	[Fact]
	public void Options_FullyQualifiedAttribute_IsFound()
	{
		var option = Assert.Single(Factory("""[global::ArgumentParser.Option("o")] public string Output { get; set; } = "";""").GetOptionAttributes());

		Assert.Equal("o", option.Attribute.ShortName);
	}

	[Fact]
	public void Options_AttributeOnTheSameLineAsOthers_IsFound()
	{
		var option = Assert.Single(Factory("""[Obsolete, Option("o")] public string Output { get; set; } = "";""").GetOptionAttributes());

		Assert.Equal("o", option.Attribute.ShortName);
	}

	[Fact]
	public void Options_AttributeInASeparateList_IsFound()
	{
		var option = Assert.Single(Factory("""
			[Obsolete]
			[Option("o")]
			public string Output { get; set; } = "";
			""").GetOptionAttributes());

		Assert.Equal("o", option.Attribute.ShortName);
	}

	#endregion

	#region Flags

	[Fact]
	public void Flags_PositionalArguments_AreExtracted()
	{
		var flag = Assert.Single(Factory("""[Flag("v", "verbose", "Talk more")] public bool Verbose { get; set; }""").GetFlagAttributes());

		Assert.Equal("v", flag.Attribute.ShortName);
		Assert.Equal("verbose", flag.Attribute.LongName);
		Assert.Equal("Talk more", flag.Attribute.Description);
		Assert.False(flag.Attribute.Required);
		Assert.Equal(-1, flag.Attribute.Position);
		Assert.Equal("Verbose", flag.PropertyName);
		Assert.Equal("bool", flag.PropertyType);
	}

	[Fact]
	public void Flags_NamedArguments_AreExtracted()
	{
		var flag = Assert.Single(Factory("""[Flag(description: "Talk more", longName: "verbose")] public bool Verbose { get; set; }""").GetFlagAttributes());

		Assert.Equal("", flag.Attribute.ShortName);
		Assert.Equal("verbose", flag.Attribute.LongName);
		Assert.Equal("Talk more", flag.Attribute.Description);
	}

	[Fact]
	public void Flags_EnumProperty_KeepsItsTypeName()
	{
		var flag = Assert.Single(Factory("""[Flag("c")] public Colour Level { get; set; }""").GetFlagAttributes());

		Assert.Equal("Extraction.Colour", flag.PropertyType);
		Assert.True(flag.IsEnum);
	}

	#endregion

	#region Positionals

	[Fact]
	public void Positionals_PositionalArguments_AreExtracted()
	{
		var positional = Assert.Single(Factory("""[Positional(3, "Fourth thing", true)] public string Fourth { get; set; } = "";""").GetPositionalAttributes());

		Assert.Equal(3, positional.Attribute.Position);
		Assert.Equal("Fourth thing", positional.Attribute.Description);
		Assert.True(positional.Attribute.Required);
		Assert.Equal("", positional.Attribute.ShortName);
		Assert.Equal("", positional.Attribute.LongName);
	}

	[Fact]
	public void Positionals_NamedArgumentsInAnyOrder_AreExtracted()
	{
		var positional = Assert.Single(Factory("""[Positional(required: true, description: "Second", position: 1)] public string Second { get; set; } = "";""").GetPositionalAttributes());

		Assert.Equal(1, positional.Attribute.Position);
		Assert.Equal("Second", positional.Attribute.Description);
		Assert.True(positional.Attribute.Required);
	}

	[Fact]
	public void Positionals_AreOptionalByDefault()
	{
		var positional = Assert.Single(Factory("""[Positional(0, "First")] public string First { get; set; } = "";""").GetPositionalAttributes());

		Assert.False(positional.Attribute.Required);
	}

	[Fact]
	public void Positionals_NegativePosition_IsKeptAsWritten()
	{
		var positional = Assert.Single(Factory("""[Positional(-2, "Odd")] public string Odd { get; set; } = "";""").GetPositionalAttributes());

		Assert.Equal(-2, positional.Attribute.Position);
	}

	[Fact]
	public void Positionals_ConstantPosition_IsEvaluated()
	{
		var positional = Assert.Single(Factory("""
			private const int Second = 1 + 1;
			[Positional(Second, "Third")] public string Third { get; set; } = "";
			""").GetPositionalAttributes());

		Assert.Equal(2, positional.Attribute.Position);
	}

	#endregion

	#region Property types and parse methods

	[Theory]
	[InlineData("int", "int")]
	[InlineData("Int32", "int")]
	[InlineData("System.Int32", "int")]
	[InlineData("String", "string")]
	[InlineData("System.String", "string")]
	[InlineData("Boolean", "bool")]
	[InlineData("Guid", "System.Guid")]
	[InlineData("DateTime", "System.DateTime")]
	[InlineData("Colour", "Extraction.Colour")]
	[InlineData("Custom", "Extraction.Custom")]
	[InlineData("string?", "string?")]
	[InlineData("int[]", "int[]")]
	public void PropertyType_IsReportedInCanonicalForm(string declared, string expected)
	{
		var option = Assert.Single(Factory($"[Option(\"o\")] public {declared} Value {{ get; set; }}").GetOptionAttributes());

		Assert.Equal(expected, option.PropertyType);
	}

	[Fact]
	public void ParsedWithMethod_PositionalArgument_IsExtracted_ForOptions()
	{
		var option = Assert.Single(Factory("""[Option("c"), ParsedWithMethod("TryParse")] public Custom Value { get; set; } = new();""").GetOptionAttributes());

		Assert.True(option.HasParseMethod);
		Assert.Equal("TryParse", option.ParseMethodName);
	}

	[Fact]
	public void ParsedWithMethod_NamedArgument_IsExtracted()
	{
		var option = Assert.Single(Factory("""[Option("c"), ParsedWithMethod(methodName: "TryParse")] public Custom Value { get; set; } = new();""").GetOptionAttributes());

		Assert.Equal("TryParse", option.ParseMethodName);
	}

	[Fact]
	public void ParsedWithMethod_IsExtracted_ForPositionals()
	{
		var positional = Assert.Single(Factory("""[Positional(0, "Thing"), ParsedWithMethod("TryParse")] public Custom Value { get; set; } = new();""").GetPositionalAttributes());

		Assert.Equal("TryParse", positional.ParseMethodName);
	}

	[Fact]
	public void ParsedWithMethod_IsExtracted_ForFlags()
	{
		var flag = Assert.Single(Factory("""[Flag("c"), ParsedWithMethod("TryParse")] public Custom Value { get; set; } = new();""").GetFlagAttributes());

		Assert.Equal("TryParse", flag.ParseMethodName);
	}

	[Fact]
	public void ParsedWithMethod_AttributeOrderDoesNotMatter()
	{
		var option = Assert.Single(Factory("""
			[ParsedWithMethod("TryParse")]
			[Option("c")]
			public Custom Value { get; set; } = new();
			""").GetOptionAttributes());

		Assert.Equal("TryParse", option.ParseMethodName);
	}

	[Fact]
	public void ParsedWithMethod_WithoutIt_NoParseMethodIsReported()
	{
		var option = Assert.Single(Factory("""[Option("c")] public int Value { get; set; }""").GetOptionAttributes());

		Assert.Equal("", option.ParseMethodName);
		Assert.False(option.HasParseMethod);
	}

	#endregion

	#region Look-alike attributes

	private const string LookAlikes = """
		[AttributeUsage(AttributeTargets.Property)] public sealed class MyOptionAttribute : Attribute { public MyOptionAttribute(string name = "") { } }
		[AttributeUsage(AttributeTargets.Property)] public sealed class MyFlagAttribute : Attribute { public MyFlagAttribute(string name = "") { } }
		[AttributeUsage(AttributeTargets.Property)] public sealed class MyPositionalAttribute : Attribute { public MyPositionalAttribute(int position = 0) { } }
		[AttributeUsage(AttributeTargets.Property)] public sealed class MyParsedWithMethodAttribute : Attribute { public MyParsedWithMethodAttribute(string name = "") { } }
		""";

	[Fact]
	public void UserAttributesWithSimilarNames_AreNotMistakenForParameterAttributes()
	{
		var factory = Factory("""
			[MyOption("x")] public string A { get; set; } = "";
			[MyFlag("x")] public bool B { get; set; }
			[MyPositional(1)] public string C { get; set; } = "";
			""", LookAlikes);

		Assert.Empty(factory.GetOptionAttributes());
		Assert.Empty(factory.GetFlagAttributes());
		Assert.Empty(factory.GetPositionalAttributes());
	}

	[Fact]
	public void UserAttributeWithSimilarName_DoesNotShadowTheRealOne()
	{
		var option = Assert.Single(Factory("""
			[MyOption("x"), Option("o", "output")] public string A { get; set; } = "";
			""", LookAlikes).GetOptionAttributes());

		Assert.Equal("o", option.Attribute.ShortName);
		Assert.Equal("output", option.Attribute.LongName);
	}

	[Fact]
	public void UserParseMethodAttributeWithSimilarName_IsNotMistakenForTheRealOne()
	{
		var option = Assert.Single(Factory("""
			[Option("c"), MyParsedWithMethod("Nope")] public Custom Value { get; set; } = new();
			""", LookAlikes).GetOptionAttributes());

		Assert.False(option.HasParseMethod);
	}

	[Fact]
	public void LookAlikeAttributes_ProduceNoDiagnosticsAndNoParsingCode()
	{
		var source = $$"""
			using System;
			using ArgumentParser;

			namespace Extraction;

			{{LookAlikes}}

			[ParameterCollection(HelpTextGeneration.GenerateTextOnly, "h", "Help", BehaviourOnError.ThrowNever)]
			public partial class Target
			{
				[MyOption("x")] public string A { get; set; } = "";
				[Flag("v")] public bool Verbose { get; set; }
			}
			""";

		var (diagnostics, output) = TestHelper.GetGeneratedOutput(source);

		Assert.Empty(diagnostics.Where(d => d.Id.StartsWith("ARG", StringComparison.Ordinal)));
		Assert.DoesNotContain("instance.A", output);
		Assert.Contains("instance.Verbose = true;", output);
	}

	#endregion

	#region Value semantics

	private static PropertyAndAttributeInfo Info(string name = "Name", string type = "string", int position = -1, bool required = false, string parseMethod = "", bool isEnum = false)
	{
		return new PropertyAndAttributeInfo
		{
			PropertyName = name,
			PropertyType = type,
			Attribute = new AttributeInfo("n", "name", "desc", required, position),
			ParseMethodName = parseMethod,
			IsEnum = isEnum,
		};
	}

	[Fact]
	public void AttributeInfo_EqualWhenEveryFieldMatches()
	{
		var left = new AttributeInfo("o", "output", "desc", true, 2);
		var right = new AttributeInfo("o", "output", "desc", true, 2);

		Assert.True(left.Equals(right));
		Assert.True(left == right);
		Assert.False(left != right);
		Assert.True(left.Equals((object)right));
		Assert.Equal(left.GetHashCode(), right.GetHashCode());
	}

	[Theory]
	[InlineData("x", "output", "desc", true, 2)]
	[InlineData("o", "x", "desc", true, 2)]
	[InlineData("o", "output", "x", true, 2)]
	[InlineData("o", "output", "desc", false, 2)]
	[InlineData("o", "output", "desc", true, 3)]
	public void AttributeInfo_DiffersWhenAnyFieldDiffers(string shortName, string longName, string description, bool required, int position)
	{
		var baseline = new AttributeInfo("o", "output", "desc", true, 2);
		var other = new AttributeInfo(shortName, longName, description, required, position);

		Assert.False(baseline.Equals(other));
		Assert.True(baseline != other);
		Assert.NotEqual(baseline.GetHashCode(), other.GetHashCode());
	}

	[Fact]
	public void AttributeInfo_IsNotEqualToOtherTypes()
	{
		var info = new AttributeInfo("o", "output", "desc", true, 2);

		Assert.False(info.Equals("o"));
		Assert.False(info.Equals((object?)null));
	}

	[Fact]
	public void AttributeInfo_DefaultInstance_HasStableHashCode()
	{
		var info = default(AttributeInfo);

		Assert.Equal(info.GetHashCode(), default(AttributeInfo).GetHashCode());
		Assert.True(info.Equals(default));
	}

	[Fact]
	public void PropertyAndAttributeInfo_EqualWhenEverythingMatches()
	{
		var left = Info();
		var right = Info();

		Assert.True(left.Equals(right));
		Assert.True(left == right);
		Assert.False(left != right);
		Assert.True(left.Equals((object)right));
		Assert.Equal(left.GetHashCode(), right.GetHashCode());
	}

	[Fact]
	public void PropertyAndAttributeInfo_DiffersByName_Type_Attribute_ParseMethod_AndEnumness()
	{
		var baseline = Info();

		Assert.NotEqual(baseline, Info(name: "Other"));
		Assert.NotEqual(baseline, Info(type: "int"));
		Assert.NotEqual(baseline, Info(position: 1));
		Assert.NotEqual(baseline, Info(required: true));
		Assert.NotEqual(baseline, Info(parseMethod: "TryParse"));
		Assert.NotEqual(baseline, Info(isEnum: true));
	}

	[Fact]
	public void PropertyAndAttributeInfo_IsNotEqualToOtherTypes()
	{
		Assert.False(Info().Equals("Name"));
		Assert.False(Info().Equals((object?)null));
	}

	[Fact]
	public void PropertyAndAttributeInfo_DefaultInstance_DoesNotThrow()
	{
		var info = default(PropertyAndAttributeInfo);

		Assert.Equal(info.GetHashCode(), default(PropertyAndAttributeInfo).GetHashCode());
		Assert.True(info.Equals(default));
		Assert.False(info.HasParseMethod);
	}

	#endregion

	#region GeneratorConfiguration

	[Fact]
	public void Configuration_PlainAttribute_UsesDefaults()
	{
		var config = Configuration("[ParameterCollection]");

		Assert.Equal("GenerateAll", config.HelpTextGenerationMode);
		Assert.Equal("h", config.HelpArgumentShortName);
		Assert.Equal("Help", config.HelpArgumentLongName);
		Assert.Equal("DisplayHelpAndExit", config.BehaviourOnError);
	}

	[Fact]
	public void Configuration_EmptyArgumentList_UsesDefaults()
	{
		var config = Configuration("[ParameterCollection()]");

		Assert.Equal("GenerateAll", config.HelpTextGenerationMode);
		Assert.Equal("DisplayHelpAndExit", config.BehaviourOnError);
	}

	[Fact]
	public void Configuration_PositionalArguments_AreReadInOrder()
	{
		var config = Configuration("""[ParameterCollection(HelpTextGeneration.GenerateTextOnly, "?", "Assist", BehaviourOnError.ThrowNever)]""");

		Assert.Equal("GenerateTextOnly", config.HelpTextGenerationMode);
		Assert.Equal("?", config.HelpArgumentShortName);
		Assert.Equal("Assist", config.HelpArgumentLongName);
		Assert.Equal("ThrowNever", config.BehaviourOnError);
	}

	[Fact]
	public void Configuration_NamedArguments_InAnyOrder_AreRead()
	{
		var config = Configuration("""[ParameterCollection(behaviourOnError: BehaviourOnError.ThrowIfAnyError, helpArgumentLongName: "Assist", helpTextGeneration: HelpTextGeneration.None, helpArgumentShortName: "?")]""");

		Assert.Equal("None", config.HelpTextGenerationMode);
		Assert.Equal("?", config.HelpArgumentShortName);
		Assert.Equal("Assist", config.HelpArgumentLongName);
		Assert.Equal("ThrowIfAnyError", config.BehaviourOnError);
	}

	[Fact]
	public void Configuration_PositionalThenNamed_AreCombined()
	{
		var config = Configuration("""[ParameterCollection(HelpTextGeneration.None, behaviourOnError: BehaviourOnError.ThrowNever)]""");

		Assert.Equal("None", config.HelpTextGenerationMode);
		Assert.Equal("h", config.HelpArgumentShortName);
		Assert.Equal("ThrowNever", config.BehaviourOnError);
	}

	[Fact]
	public void Configuration_FullyQualifiedEnumValues_UseTheMemberName()
	{
		var config = Configuration("""[global::ArgumentParser.ParameterCollection(global::ArgumentParser.HelpTextGeneration.GenerateTextOnly, behaviourOnError: global::ArgumentParser.BehaviourOnError.ThrowNever)]""");

		Assert.Equal("GenerateTextOnly", config.HelpTextGenerationMode);
		Assert.Equal("ThrowNever", config.BehaviourOnError);
	}

	[Fact]
	public void Configuration_NamedStringArguments_AreReadByValue_NotByName()
	{
		var config = Configuration("""[ParameterCollection(helpArgumentShortName: "?")]""");

		Assert.Equal("?", config.HelpArgumentShortName);
		Assert.Equal("Help", config.HelpArgumentLongName);
	}

	[Fact]
	public void Configuration_StringConstants_AreEvaluated()
	{
		var (model, declaration) = Compile(
			"[ParameterCollection(HelpTextGeneration.GenerateAll, Names.Short, helpArgumentLongName: Names.Long)]",
			"",
			"""public static class Names { public const string Short = "?"; public const string Long = "Assist"; }""");

		var config = new GeneratorConfiguration(declaration, model);

		Assert.Equal("?", config.HelpArgumentShortName);
		Assert.Equal("Assist", config.HelpArgumentLongName);
	}

	[Fact]
	public void Configuration_NullClassDeclaration_Throws()
	{
		var (model, _) = Compile("[ParameterCollection]", "");

		Assert.Throws<ArgumentNullException>(() => new GeneratorConfiguration(null!, model));
	}

	[Fact]
	public void Configuration_ClassWithoutTheAttribute_Throws()
	{
		var (model, declaration) = Compile("[Obsolete]", "");

		var exception = Assert.Throws<InvalidOperationException>(() => new GeneratorConfiguration(declaration, model));

		Assert.Contains("No ParameterCollection attribute", exception.Message);
	}

	[Theory]
	[InlineData("GenerateAll", true, true, true)]
	[InlineData("GenerateTextOnly", false, true, false)]
	[InlineData("GenerateArgumentOnly", true, false, false)]
	[InlineData("GenerateArgumentAndHandler", true, false, true)]
	[InlineData("None", false, false, false)]
	[InlineData("generateall", true, true, true)]
	[InlineData("NotAMode", false, false, false)]
	public void Configuration_HelpTextMode_DecidesWhatIsGenerated(string mode, bool argument, bool text, bool displayOnRequest)
	{
		var config = Configuration("[ParameterCollection]");
		config.HelpTextGenerationMode = mode;

		Assert.Equal(argument, config.HelpArgumentShouldBeGenerated());
		Assert.Equal(text, config.HelpTextShouldBeGenerated());
		Assert.Equal(displayOnRequest, config.HelpTextShouldDisplayOnRequest());
	}

	[Fact]
	public void Configuration_NullHelpTextMode_GeneratesNothing()
	{
		var config = Configuration("[ParameterCollection]");
		config.HelpTextGenerationMode = null;

		Assert.False(config.HelpArgumentShouldBeGenerated());
		Assert.False(config.HelpTextShouldBeGenerated());
		Assert.False(config.HelpTextShouldDisplayOnRequest());
	}

	[Theory]
	[InlineData("DisplayHelpAndExit", true, false, false)]
	[InlineData("ThrowNever", false, false, false)]
	[InlineData("ThrowIfMissingRequired", false, true, false)]
	[InlineData("ThrowIfAnyError", false, true, true)]
	[InlineData("throwifanyerror", false, true, true)]
	[InlineData("NotABehaviour", false, false, false)]
	public void Configuration_BehaviourOnError_DecidesWhatHappensOnErrors(string behaviour, bool displayHelp, bool throwIfMissing, bool throwIfAny)
	{
		var config = Configuration("[ParameterCollection]");
		config.BehaviourOnError = behaviour;

		Assert.Equal(displayHelp, config.HelpTextShouldDisplayOnError());
		Assert.Equal(throwIfMissing, config.ShouldThrowIfMissingRequired());
		Assert.Equal(throwIfAny, config.ShouldThrowIfAnyError());
	}

	[Fact]
	public void Configuration_NullBehaviour_DoesNotDisplayHelp_AndThrowsOnEverything()
	{
		var config = Configuration("[ParameterCollection]");
		config.BehaviourOnError = null;

		Assert.False(config.HelpTextShouldDisplayOnError());
		Assert.True(config.ShouldThrowIfMissingRequired());
		Assert.True(config.ShouldThrowIfAnyError());
	}

	#endregion

	#region Guard clauses of the pipeline components

	private sealed record Components(
		ClassDeclarationSyntax Class,
		ISymbol Symbol,
		GeneratorConfiguration Config,
		ArgumentProvider Provider,
		HelptextProvider HelpText);

	private static Components BuildComponents()
	{
		var (model, declaration) = Compile("[ParameterCollection]", """[Option("o", "out", "where")] public string Out { get; set; } = "";""");
		var config = new GeneratorConfiguration(declaration, model);
		var properties = declaration.Members.OfType<PropertyDeclarationSyntax>().ToList();
		var provider = new ArgumentProvider(new AttributeFactory(model, new ReadOnlyCollection<PropertyDeclarationSyntax>(properties)), config);
		return new Components(declaration, model.GetDeclaredSymbol(declaration)!, config, provider, new HelptextProvider(config, provider, "Target"));
	}

	[Fact]
	public void ArgumentProvider_NullDependencies_Throw()
	{
		var c = BuildComponents();
		var factory = Factory("");

		Assert.Throws<ArgumentNullException>(() => new ArgumentProvider(null!, c.Config));
		Assert.Throws<ArgumentNullException>(() => new ArgumentProvider(factory, null!));
	}

	[Fact]
	public void HelptextProvider_NullDependencies_Throw()
	{
		var c = BuildComponents();

		Assert.Throws<ArgumentNullException>(() => new HelptextProvider(null!, c.Provider, "Target"));
		Assert.Throws<ArgumentNullException>(() => new HelptextProvider(c.Config, null!, "Target"));
		Assert.Throws<ArgumentNullException>(() => new HelptextProvider(c.Config, c.Provider, null!));
	}

	[Fact]
	public void SourceTextGenerator_NullDependencies_Throw()
	{
		var c = BuildComponents();

		Assert.Throws<ArgumentNullException>(() => new SourceTextGenerator(c.Class, null!, c.HelpText, c.Symbol, c.Config));
		Assert.Throws<ArgumentNullException>(() => new SourceTextGenerator(c.Class, c.Provider, null!, c.Symbol, c.Config));
		Assert.Throws<ArgumentNullException>(() => new SourceTextGenerator(c.Class, c.Provider, c.HelpText, c.Symbol, null!));
	}

	[Fact]
	public void Validation_NullArguments_Throw()
	{
		var c = BuildComponents();

		Assert.Throws<ArgumentNullException>(() => Validation.ValidateClassDeclaration(null!));
		Assert.Throws<ArgumentNullException>(() => Validation.ValidateAttributes(null!, c.Class));
		Assert.Throws<ArgumentNullException>(() => Validation.ValidateAttributes(c.Provider, null!));
		Assert.Throws<ArgumentNullException>(() => Validation.ValidateGenerationOptions(null!, c.Provider, c.Class));
		Assert.Throws<ArgumentNullException>(() => Validation.ValidateGenerationOptions(c.Config, null!, c.Class));
		Assert.Throws<ArgumentNullException>(() => Validation.ValidateGenerationOptions(c.Config, c.Provider, null!));
	}

	[Fact]
	public void Validation_ValidComponents_ReportNothing()
	{
		var c = BuildComponents();

		Assert.Empty(Validation.ValidateClassDeclaration(c.Class));
		Assert.Empty(Validation.ValidateAttributes(c.Provider, c.Class));
		Assert.Empty(Validation.ValidateGenerationOptions(c.Config, c.Provider, c.Class));
	}

	[Fact]
	public void Validation_ReportDiagnostics_NullListIsIgnored()
	{
		// the context is a struct that cannot be created outside of Roslyn, so only the null path can be exercised here
		Assert.Null(Record.Exception(() => Validation.ReportDiagnostics(default, null!)));
	}

	#endregion
}
