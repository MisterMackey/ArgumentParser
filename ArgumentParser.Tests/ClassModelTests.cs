using System.Collections.ObjectModel;
using ArgumentParser.Internal;
using ArgumentParser.Internal.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace ArgumentParser.Tests;

/// <summary>
/// Tests for the plain data types the incremental pipeline compares: <see cref="EquatableArray{T}"/>,
/// <see cref="DiagnosticInfo"/>, <see cref="LocationInfo"/>, <see cref="ClassModel"/> and <see cref="GeneratorConfiguration"/>.
/// </summary>
public class ClassModelTests
{
	#region EquatableArray

	[Fact]
	public void EquatableArray_ComparesByContent_NotByReference()
	{
		var left = new EquatableArray<string>(["a", "b"]);
		var right = new EquatableArray<string>(["a", "b"]);

		Assert.True(left.Equals(right));
		Assert.True(left == right);
		Assert.False(left != right);
		Assert.True(left.Equals((object)right));
		Assert.Equal(left.GetHashCode(), right.GetHashCode());
	}

	[Fact]
	public void EquatableArray_DiffersByOrderLengthAndContent()
	{
		var baseline = new EquatableArray<string>(["a", "b"]);

		Assert.NotEqual(baseline, new EquatableArray<string>(["b", "a"]));
		Assert.NotEqual(baseline, new EquatableArray<string>(["a"]));
		Assert.NotEqual(baseline, new EquatableArray<string>(["a", "c"]));
		Assert.True(baseline != new EquatableArray<string>(["a"]));
		Assert.False(baseline.Equals("a"));
	}

	[Fact]
	public void EquatableArray_DefaultIsEmpty_AndEqualToAnEmptyArray()
	{
		var empty = new EquatableArray<string>([]);

		Assert.Equal(0, default(EquatableArray<string>).Count);
		Assert.Equal(empty, default);
		Assert.Equal(empty.GetHashCode(), default(EquatableArray<string>).GetHashCode());
		Assert.Empty(default(EquatableArray<string>).AsReadOnly());
		Assert.Throws<IndexOutOfRangeException>(() => default(EquatableArray<string>)[0]);
	}

	[Fact]
	public void EquatableArray_CopiesItsInput_AndExposesItems()
	{
		var source = new List<string> { "a", "b" };
		var array = new EquatableArray<string>(source);
		source.Add("c");

		Assert.Equal(2, array.Count);
		Assert.Equal("b", array[1]);
		Assert.Equal(["a", "b"], array.AsReadOnly());
		Assert.IsType<ReadOnlyCollection<string>>(array.AsReadOnly());
	}

	[Fact]
	public void EquatableArray_NullItems_Throws()
	{
		Assert.Throws<ArgumentNullException>(() => new EquatableArray<string>(null!));
	}

	#endregion

	#region DiagnosticInfo and LocationInfo

	private static Location LocationIn(string source, string path, string text)
	{
		var tree = CSharpSyntaxTree.ParseText(source, path: path);
		return Location.Create(tree, new TextSpan(source.IndexOf(text, StringComparison.Ordinal), text.Length));
	}

	[Fact]
	public void DiagnosticInfo_KeepsDescriptorLocationAndArguments()
	{
		var location = LocationIn("class A\n{\n\tint B;\n}", "A.cs", "int B;");

		var info = DiagnosticInfo.Create(GeneratorDiagnostics.ARG003, location, "short", "v");

		Assert.Equal("ARG003", info.Id);
		Assert.Equal(DiagnosticSeverity.Error, info.Severity);
		Assert.Same(GeneratorDiagnostics.ARG003, info.Descriptor);
		Assert.Equal(["short", "v"], info.Arguments.AsReadOnly());
		Assert.NotNull(info.Location);
	}

	[Fact]
	public void DiagnosticInfo_ArgumentsAreFormattedInvariantly()
	{
		var info = DiagnosticInfo.Create(GeneratorDiagnostics.ARG004, null, -1, null, 2.5);

		Assert.Equal(["-1", "", "2.5"], info.Arguments.AsReadOnly());
	}

	[Fact]
	public void DiagnosticInfo_WarningDescriptor_HasWarningSeverity()
	{
		Assert.Equal(DiagnosticSeverity.Warning, DiagnosticInfo.Create(GeneratorDiagnostics.ARG008, null).Severity);
	}

	[Fact]
	public void DiagnosticInfo_ToDiagnostic_ReportsTheSameAsDiagnosticCreate()
	{
		var location = LocationIn("class A\n{\n\tint B;\n}", "A.cs", "int B;");
		var expected = Diagnostic.Create(GeneratorDiagnostics.ARG003, location, "long", "verbose");

		var actual = DiagnosticInfo.Create(GeneratorDiagnostics.ARG003, location, "long", "verbose").ToDiagnostic();

		Assert.Equal(expected.Id, actual.Id);
		Assert.Equal(expected.Severity, actual.Severity);
		Assert.Equal(expected.GetMessage(), actual.GetMessage());
		Assert.Equal(expected.Location.SourceSpan, actual.Location.SourceSpan);
		Assert.Equal(expected.Location.GetLineSpan().Span, actual.Location.GetLineSpan().Span);
		Assert.Equal("A.cs", actual.Location.GetLineSpan().Path);
	}

	[Fact]
	public void DiagnosticInfo_WithoutALocation_ReportsWithoutOne()
	{
		var info = DiagnosticInfo.Create(GeneratorDiagnostics.ARG008, Location.None);

		Assert.Null(info.Location);
		Assert.Equal(Location.None, info.ToDiagnostic().Location);
		Assert.Null(DiagnosticInfo.Create(GeneratorDiagnostics.ARG008, null).Location);
	}

	[Fact]
	public void DiagnosticInfo_ComparesById_Location_AndArguments()
	{
		var source = "class A\n{\n\tint B;\n}";
		var location = LocationIn(source, "A.cs", "int B;");
		var baseline = DiagnosticInfo.Create(GeneratorDiagnostics.ARG003, location, "short", "v");

		Assert.Equal(baseline, DiagnosticInfo.Create(GeneratorDiagnostics.ARG003, LocationIn(source, "A.cs", "int B;"), "short", "v"));
		Assert.Equal(baseline.GetHashCode(), DiagnosticInfo.Create(GeneratorDiagnostics.ARG003, LocationIn(source, "A.cs", "int B;"), "short", "v").GetHashCode());
		Assert.NotEqual(baseline, DiagnosticInfo.Create(GeneratorDiagnostics.ARG004, location, "short", "v"));
		Assert.NotEqual(baseline, DiagnosticInfo.Create(GeneratorDiagnostics.ARG003, location, "short", "x"));
		Assert.NotEqual(baseline, DiagnosticInfo.Create(GeneratorDiagnostics.ARG003, LocationIn(source, "A.cs", "class A"), "short", "v"));
		Assert.NotEqual(baseline, DiagnosticInfo.Create(GeneratorDiagnostics.ARG003, LocationIn(source, "Other.cs", "int B;"), "short", "v"));
		Assert.NotEqual(baseline, DiagnosticInfo.Create(GeneratorDiagnostics.ARG003, null, "short", "v"));
		Assert.False(baseline.Equals("ARG003"));
		Assert.False(baseline.Equals(null));
	}

	[Fact]
	public void DiagnosticInfo_NullDescriptor_Throws()
	{
		Assert.Throws<ArgumentNullException>(() => DiagnosticInfo.Create(null!, null));
	}

	[Fact]
	public void LocationInfo_FromNullOrTreelessLocation_IsNull()
	{
		Assert.Null(LocationInfo.From(null));
		Assert.Null(LocationInfo.From(Location.None));
	}

	[Fact]
	public void LocationInfo_RoundTripsPathAndSpans()
	{
		var original = LocationIn("class A\n{\n\tint B;\n}", "dir/A.cs", "int B;");

		var info = LocationInfo.From(original)!;
		var rebuilt = info.ToLocation();

		Assert.Equal("dir/A.cs", info.FilePath);
		Assert.Equal(original.SourceSpan, info.TextSpan);
		Assert.Equal(original.GetLineSpan().Span, info.LineSpan);
		Assert.Equal(original.SourceSpan, rebuilt.SourceSpan);
		Assert.Equal(2, rebuilt.GetLineSpan().StartLinePosition.Line);
		Assert.Equal("dir/A.cs", rebuilt.GetLineSpan().Path);
	}

	#endregion

	#region GeneratorConfiguration equality

	private static GeneratorConfiguration Configuration(string attribute)
	{
		var compilation = TestHelper.CreateCompilation($"using ArgumentParser;\n{attribute}\npublic partial class Target {{ }}");
		var tree = compilation.SyntaxTrees.Single();
		var declaration = tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().Single();
		return new GeneratorConfiguration(declaration, compilation.GetSemanticModel(tree));
	}

	[Fact]
	public void GeneratorConfiguration_ComparesEverySetting()
	{
		var baseline = Configuration("[ParameterCollection]");

		Assert.Equal(baseline, Configuration("[ParameterCollection]"));
		Assert.Equal(baseline, Configuration("[ParameterCollection(HelpTextGeneration.GenerateAll, \"h\", \"Help\", BehaviourOnError.DisplayHelpAndExit)]"));
		Assert.Equal(baseline.GetHashCode(), Configuration("[ParameterCollection]").GetHashCode());
		Assert.NotEqual(baseline, Configuration("[ParameterCollection(HelpTextGeneration.None)]"));
		Assert.NotEqual(baseline, Configuration("[ParameterCollection(helpArgumentShortName: \"?\")]"));
		Assert.NotEqual(baseline, Configuration("[ParameterCollection(helpArgumentLongName: \"Assist\")]"));
		Assert.NotEqual(baseline, Configuration("[ParameterCollection(behaviourOnError: BehaviourOnError.ThrowNever)]"));
		Assert.NotEqual(baseline, Configuration("[ParameterCollection(UseCurrentCulture = true)]"));
		Assert.NotEqual(baseline.GetHashCode(), Configuration("[ParameterCollection(UseCurrentCulture = true)]").GetHashCode());
		Assert.Equal(baseline, Configuration("[ParameterCollection(UseCurrentCulture = false)]"));
		Assert.False(baseline.Equals(null));
		Assert.False(baseline.Equals("[ParameterCollection]"));
	}

	#endregion

	#region ClassModel

	private static PropertyAndAttributeInfo Property(string name) => new()
	{
		PropertyName = name,
		PropertyType = "string",
		Attribute = new AttributeInfo("", name, "", false, -1),
		ParseMethodName = "",
	};

	private static ClassModel Model(
		string className = "Target",
		string? namespaceName = "Ns",
		string programName = "Program",
		bool withConfiguration = true,
		string option = "Output",
		string flag = "Verbose",
		string positional = "File",
		DiagnosticInfo[]? diagnostics = null,
		bool isCommand = false)
	{
		return new ClassModel(
			className,
			namespaceName,
			programName,
			withConfiguration ? Configuration("[ParameterCollection]") : null,
			new EquatableArray<PropertyAndAttributeInfo>([Property(option)]),
			new EquatableArray<PropertyAndAttributeInfo>([Property(flag)]),
			new EquatableArray<PropertyAndAttributeInfo>([Property(positional)]),
			new EquatableArray<DiagnosticInfo>(diagnostics ?? []),
			default,
			isCommand);
	}

	[Fact]
	public void ClassModel_EqualWhenEverythingMatches()
	{
		var baseline = Model();

		Assert.Equal(baseline, Model());
		Assert.Equal(baseline.GetHashCode(), Model().GetHashCode());
		Assert.True(baseline.Equals((object)Model()));
	}

	[Fact]
	public void ClassModel_DiffersByEveryField()
	{
		var baseline = Model();
		var warning = DiagnosticInfo.Create(GeneratorDiagnostics.ARG008, null);

		Assert.NotEqual(baseline, Model(className: "Other"));
		Assert.NotEqual(baseline, Model(namespaceName: "Other"));
		Assert.NotEqual(baseline, Model(namespaceName: null));
		Assert.NotEqual(baseline, Model(programName: "Other"));
		Assert.NotEqual(baseline, Model(withConfiguration: false));
		Assert.NotEqual(baseline, Model(option: "Other"));
		Assert.NotEqual(baseline, Model(flag: "Other"));
		Assert.NotEqual(baseline, Model(positional: "Other"));
		Assert.NotEqual(baseline, Model(diagnostics: [warning]));
		Assert.NotEqual(baseline, Model(isCommand: true));
		Assert.False(baseline.Equals(null));
		Assert.False(baseline.Equals("Target"));
	}

	[Fact]
	public void ClassModel_NullNamespaceAndConfiguration_HaveAStableHashCode()
	{
		Assert.Equal(Model(namespaceName: null, withConfiguration: false).GetHashCode(), Model(namespaceName: null, withConfiguration: false).GetHashCode());
	}

	[Fact]
	public void ClassModel_CanGenerate_NeedsAConfigurationAndNoErrors()
	{
		var error = DiagnosticInfo.Create(GeneratorDiagnostics.ARG001, null, "Target");
		var warning = DiagnosticInfo.Create(GeneratorDiagnostics.ARG008, null);

		Assert.True(Model().CanGenerate);
		Assert.True(Model(diagnostics: [warning]).CanGenerate);
		Assert.False(Model(diagnostics: [warning]).HasErrors);
		Assert.False(Model(diagnostics: [error]).CanGenerate);
		Assert.True(Model(diagnostics: [warning, error]).HasErrors);
		Assert.False(Model(withConfiguration: false).CanGenerate);
	}

	[Fact]
	public void ClassModel_NullNames_Throw()
	{
		Assert.Throws<ArgumentNullException>(() => new ClassModel(null!, null, "p", null, default, default, default, default));
		Assert.Throws<ArgumentNullException>(() => new ClassModel("c", null, null!, null, default, default, default, default));
	}

	#endregion

	#region NodeSettings and NodeArguments

	[Fact]
	public void NodeSettings_ComparesEverySetting()
	{
		var baseline = new NodeSettings(0, "h", "Help", 0);

		Assert.Equal(NodeSettings.Default, baseline);
		Assert.Equal(NodeSettings.Default.GetHashCode(), baseline.GetHashCode());
		Assert.NotEqual(baseline, new NodeSettings(2, "h", "Help", 0));
		Assert.NotEqual(baseline, new NodeSettings(0, "?", "Help", 0));
		Assert.NotEqual(baseline, new NodeSettings(0, "h", "help", 0));
		Assert.NotEqual(baseline, new NodeSettings(0, "h", "Help", 2));
		Assert.NotEqual(baseline, new NodeSettings(0, "h", "Help", 0, useCurrentCulture: true));
		Assert.NotEqual(baseline.GetHashCode(), new NodeSettings(0, "h", "Help", 0, useCurrentCulture: true).GetHashCode());
		Assert.Equal(new NodeSettings(0, "", "", 0), new NodeSettings(0, null!, null!, 0));
	}

	[Theory]
	[InlineData(0, "h", "Help", 0, true, true, null)]
	[InlineData(0, "", "", 0, true, false, null)]
	[InlineData(2, "h", "Help", 0, true, false, null)]
	[InlineData(1, "h", "Help", 2, false, false, null)]
	[InlineData(1, "h", "Help", 0, false, false, "HelpTextGeneration.None together with BehaviourOnError.DisplayHelpAndExit")]
	[InlineData(3, "h", "Help", 2, false, false, "HelpTextGeneration.GenerateArgumentOnly")]
	[InlineData(4, "h", "Help", 2, false, false, "HelpTextGeneration.GenerateArgumentAndHandler")]
	[InlineData(9, "h", "Help", 2, false, false, "HelpTextGeneration.9")]
	public void NodeSettings_TellWhatATreeGenerates(int mode, string shortName, string longName, int behaviour, bool texts, bool argument, string? unsupported)
	{
		var settings = new NodeSettings(mode, shortName, longName, behaviour);

		Assert.Equal(texts, settings.GeneratesHelpText);
		Assert.Equal(argument, settings.HasHelpArgument);
		Assert.Equal(unsupported, settings.UnsupportedByATree);
	}

	[Fact]
	public void NodeSettings_BehaviourExpression_NamesTheValueOrCastsIt()
	{
		Assert.Equal("global::ArgumentParser.BehaviourOnError.ThrowIfMissingRequired", new NodeSettings(0, "h", "Help", 3).BehaviourExpression);
		Assert.Equal("(global::ArgumentParser.BehaviourOnError)(7)", new NodeSettings(0, "h", "Help", 7).BehaviourExpression);
	}

	[Fact]
	public void NodeArguments_ComparePositionalsToo()
	{
		var positional = new EquatableArray<PropertyAndAttributeInfo>([Property("File")]);

		Assert.NotEqual(NodeArguments.Empty, new NodeArguments(default, default, positional));
		Assert.Equal(new NodeArguments(default, default, positional), new NodeArguments(default, default, positional));
	}
	#endregion

	#region ArgumentProvider from extracted arguments

	[Fact]
	public void ArgumentProvider_FromLists_AddsTheHelpFlagFirst_ThenTheGivenArguments()
	{
		var provider = new ArgumentProvider(
			Configuration("[ParameterCollection]"),
			[Property("Output")],
			[Property("Verbose")],
			[Property("File")]);

		Assert.Equal(["DisplayHelp", "Verbose"], provider.GetFlagArguments().Select(f => f.PropertyName));
		Assert.Equal(["Output"], provider.GetOptionArguments().Select(f => f.PropertyName));
		Assert.Equal(["File"], provider.GetPositionalArguments().Select(f => f.PropertyName));
	}

	[Fact]
	public void ArgumentProvider_FromLists_WithoutGeneratedHelp_AddsNoHelpFlag()
	{
		var provider = new ArgumentProvider(Configuration("[ParameterCollection(HelpTextGeneration.None)]"), [], [], []);

		Assert.Empty(provider.GetFlagArguments());
	}

	[Fact]
	public void ArgumentProvider_FromLists_NullArguments_Throw()
	{
		var config = Configuration("[ParameterCollection]");

		Assert.Throws<ArgumentNullException>(() => new ArgumentProvider(null!, [], [], []));
		Assert.Throws<ArgumentNullException>(() => new ArgumentProvider(config, null!, [], []));
		Assert.Throws<ArgumentNullException>(() => new ArgumentProvider(config, [], null!, []));
		Assert.Throws<ArgumentNullException>(() => new ArgumentProvider(config, [], [], null!));
	}

	#endregion
}
