using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ArgumentParser.Internal.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArgumentParser.Internal
{

	/// <summary>
	/// IIncrementalGenerator implementation that generates a parser for classes with the ParameterCollection attribute.
	/// </summary>
	[Generator(LanguageNames.CSharp)]
	public class ParserGenerator : IIncrementalGenerator
	{
		/// <summary>
		/// The name of the pipeline step that holds the models of the classes with the CommandApp attribute.
		/// Tests use it to see whether a model was reused.
		/// </summary>
		public const string RootModelsStepName = "RootModels";

		/// <summary>
		/// The name of the pipeline step that holds the models of the classes with the Command attribute.
		/// Tests use it to see whether a model was reused.
		/// </summary>
		public const string CommandModelsStepName = "CommandModels";

		/// <summary>
		/// The name of the pipeline step that holds the nodes of all models, without their locations.
		/// It stays equal when an edit only moves code around. Tests use it to see whether linking can be skipped.
		/// </summary>
		public const string NodeSourcesStepName = "NodeSources";

		/// <summary>
		/// The name of the pipeline step that holds the linked trees and the rules they break.
		/// Tests use it to see whether a tree was reused.
		/// </summary>
		public const string LinkedTreesStepName = "LinkedTrees";

		/// <summary>
		/// Initializes the generator. This method is called once per compilation.
		/// </summary>
		/// <param name="context"></param>
		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			// Everything that reads syntax or symbols happens in the transform, which returns plain data.
			// The pipeline compares that data, so output is only generated again when it changed.
			var classModels = context.SyntaxProvider
			.ForAttributeWithMetadataName(
			    AttributeReader.ParameterCollection,
			    (node, _) => node is ClassDeclarationSyntax,
			    ExtractModel
			    );

			// register output
			context.RegisterSourceOutput(classModels, GenerateSource);

			// The tree of commands has its own models: they say where a class sits in the tree, not what its arguments are.
			// A class with [CommandApp] is read by the first provider, so the second one skips it (it would be read twice otherwise).
			var rootModels = context.SyntaxProvider
			.ForAttributeWithMetadataName(
			    AttributeReader.CommandApp,
			    (node, _) => node is ClassDeclarationSyntax,
			    ExtractNodeModel
			    )
			.WithTrackingName(RootModelsStepName);
			var commandModels = context.SyntaxProvider
			.ForAttributeWithMetadataName(
			    AttributeReader.Command,
			    (node, _) => node is ClassDeclarationSyntax,
			    ExtractCommandModel
			    )
			.Where(static model => model is not null)
			.Select(static (model, _) => model!)
			.WithTrackingName(CommandModelsStepName);

			// the models report what is wrong with a node by itself
			context.RegisterSourceOutput(rootModels, ReportNodeDiagnostics);
			context.RegisterSourceOutput(commandModels, ReportNodeDiagnostics);

			// All nodes together are linked into trees. A model holds the locations of the class and its properties and the
			// diagnostics (which have locations too), so an edit that only moves code (a line added above the class) changes it.
			// A source is the same model without them. Linking only sees the sources, so after such an edit the sources compare
			// equal, linking and generating the trees are skipped, and only the diagnostics are reported again from the new models.
			var nodeModels = rootModels.Collect()
			.Combine(commandModels.Collect())
			.Select(static (pair, _) => new EquatableArray<CommandNodeModel>(pair.Left.Concat(pair.Right)));
			var nodeSources = nodeModels
			.Select(static (models, _) => new EquatableArray<CommandNodeSource>(models.AsReadOnly().Select(item => item.Source)))
			.WithTrackingName(NodeSourcesStepName);
			var linked = nodeSources
			.Select(static (sources, _) => CommandTreeLinker.Link(sources.AsReadOnly()))
			.WithTrackingName(LinkedTreesStepName);

			// The parser of a leaf comes from the ParameterCollection pipeline. A leaf whose class or arguments have errors has none,
			// and the code of its tree, which calls it, would only add compiler errors to the ones already reported.
			var leavesWithErrors = classModels
			.Where(static model => model.IsCommand && model.HasErrors)
			.Select(static (model, _) => model.TypeName)
			.Collect()
			.Select(static (names, _) => new EquatableArray<string>(names.OrderBy(name => name, StringComparer.Ordinal)));

			// the rules the trees break are reported, and the trees without errors are generated
			context.RegisterSourceOutput(linked.Combine(nodeModels), ReportLinkDiagnostics);
			context.RegisterSourceOutput(linked.Combine(leavesWithErrors), GenerateTrees);
		}

		private static CommandNodeModel ExtractNodeModel(GeneratorAttributeSyntaxContext generatorContext, CancellationToken cancellationToken)
		{
			return CommandNodeExtractor.Extract((ClassDeclarationSyntax)generatorContext.TargetNode, (INamedTypeSymbol)generatorContext.TargetSymbol, generatorContext.SemanticModel);
		}

		private static CommandNodeModel? ExtractCommandModel(GeneratorAttributeSyntaxContext generatorContext, CancellationToken cancellationToken)
		{
			var symbol = (INamedTypeSymbol)generatorContext.TargetSymbol;
			return CommandNodeExtractor.HasCommandAppAttribute(symbol)
				? null
				: CommandNodeExtractor.Extract((ClassDeclarationSyntax)generatorContext.TargetNode, symbol, generatorContext.SemanticModel);
		}

		private static void ReportNodeDiagnostics(SourceProductionContext context, CommandNodeModel model)
		{
			Validation.ReportDiagnostics(context, model.Diagnostics.AsReadOnly());
		}

		private static void ReportLinkDiagnostics(SourceProductionContext context, (LinkResult Result, EquatableArray<CommandNodeModel> Models) input)
		{
			Validation.ReportDiagnostics(context, CommandTreeLinker.ToDiagnostics(input.Result, input.Models.AsReadOnly()).AsReadOnly());
		}

		private static void GenerateTrees(SourceProductionContext context, (LinkResult Result, EquatableArray<string> LeavesWithErrors) input)
		{
			var result = input.Result;
			var leavesWithErrors = input.LeavesWithErrors.AsReadOnly();
			for (int i = 0; i < result.Trees.Count; i++)
			{
				var tree = result.Trees[i];
				if (tree.HasErrors || tree.Nodes.AsReadOnly().Any(n => leavesWithErrors.Contains(n.Source.Node.TypeName)))
				{
					continue;
				}
				for (int j = 0; j < tree.Nodes.Count; j++)
				{
					var node = tree.Nodes[j];
					context.AddSource(CommandTreeSourceGenerator.HintName(node), CommandTreeSourceGenerator.Generate(tree, node));
				}
			}
		}

		private static ClassModel ExtractModel(GeneratorAttributeSyntaxContext generatorContext, CancellationToken cancellationToken)
		{
			var classDeclaration = (ClassDeclarationSyntax)generatorContext.TargetNode;
			var semanticModel = generatorContext.SemanticModel;
			var className = classDeclaration.Identifier.Text;
			var containingNamespace = generatorContext.TargetSymbol.ContainingNamespace;
			var namespaceName = containingNamespace is { IsGlobalNamespace: false } ? containingNamespace.ToDisplayString() : null;
			var symbol = (INamedTypeSymbol)generatorContext.TargetSymbol;
			var containingTypes = ContainingType.ReadAll(symbol);
			var programName = semanticModel.Compilation.Assembly.Name ?? "";
			var isCommand = AttributeReader.Has(symbol, AttributeReader.Command);
			var typeName = symbol.ToDisplayString();

			ClassModel Rejected(IEnumerable<DiagnosticInfo> diagnostics)
			{
				return new ClassModel(
					className,
					namespaceName,
					programName,
					null,
					default,
					default,
					default,
					new EquatableArray<DiagnosticInfo>(diagnostics),
					containingTypes,
					isCommand,
					typeName);
			}

			// the class may be declared in several parts, in several files: the members of every part count
			var parts = DeclarationParts.Read(symbol, classDeclaration, semanticModel);
			var declarations = parts.Select(p => p.Declaration).ToList();

			// check class level diagnostics and stop processing if class is invalid
			var classDiagnostics = Validation.ValidateClassDeclaration(classDeclaration, parts: declarations);
			if (classDiagnostics.Count > 0)
			{
				return Rejected(classDiagnostics);
			}

			var config = new GeneratorConfiguration(classDeclaration, semanticModel);

			// instantiate all attribute properties with their respective arguments
			var factories = DeclarationParts.AttributeFactories(parts).ToList();
			var options = factories.SelectMany(f => f.GetOptionAttributes()).ToList();
			var flags = factories.SelectMany(f => f.GetFlagAttributes()).ToList();
			var positionals = factories.SelectMany(f => f.GetPositionalAttributes()).ToList();
			var argumentProvider = new ArgumentProvider(config, options, flags, positionals);

			// check for errors
			var attributeDiagnostics = Validation.ValidateAttributes(
				argumentProvider.GetOptionArguments(),
				argumentProvider.GetFlagArguments(),
				argumentProvider.GetPositionalArguments(),
				declarations);
			if (attributeDiagnostics.Count > 0)
			{
				return Rejected(attributeDiagnostics);
			}
			// not all helptext diags are errors, some are warnings
			var helptextDiagnostics = Validation.ValidateGenerationOptions(config, argumentProvider, classDeclaration, declarations);

			return new ClassModel(
				className,
				namespaceName,
				programName,
				config,
				new EquatableArray<PropertyAndAttributeInfo>(options),
				new EquatableArray<PropertyAndAttributeInfo>(flags),
				new EquatableArray<PropertyAndAttributeInfo>(positionals),
				new EquatableArray<DiagnosticInfo>(helptextDiagnostics),
				containingTypes,
				isCommand,
				typeName);
		}

		private static void GenerateSource(SourceProductionContext context, ClassModel model)
		{
			Validation.ReportDiagnostics(context, model.Diagnostics.AsReadOnly());
			if (!model.CanGenerate)
			{
				return;
			}

			var config = model.Configuration!;
			var argumentProvider = new ArgumentProvider(
				config,
				model.Options.AsReadOnly(),
				model.Flags.AsReadOnly(),
				model.Positionals.AsReadOnly());
			var helptextProvider = new HelptextProvider(config, argumentProvider, model.ProgramName);
			var textGenerator = new SourceTextGenerator(
				model.ClassName,
				model.NamespaceName,
				argumentProvider,
				helptextProvider,
				config,
				model.ContainingTypes,
				model.IsCommand
			);
			var sourceText = textGenerator.GenerateSourceText();
			// the namespace and the types the class is nested in are part of the name: command classes with the same name (an Add below each of two groups) are common
			var typeName = ContainingType.QualifiedName(model.NamespaceName, model.ContainingTypes, model.ClassName);
			context.AddSource($"{typeName}_Parser.g.cs", sourceText);
		}
	}
}
