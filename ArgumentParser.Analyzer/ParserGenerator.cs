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
		/// Initializes the generator. This method is called once per compilation.
		/// </summary>
		/// <param name="context"></param>
		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
			// Everything that reads syntax or symbols happens in the transform, which returns plain data.
			// The pipeline compares that data, so output is only generated again when it changed.
			var classModels = context.SyntaxProvider
			.ForAttributeWithMetadataName(
			    "ArgumentParser.ParameterCollectionAttribute", // Fully qualified name of the attribute
			    (node, _) => node is ClassDeclarationSyntax,
			    ExtractModel
			    );

			// register output
			context.RegisterSourceOutput(classModels, GenerateSource);
		}

		private static ClassModel ExtractModel(GeneratorAttributeSyntaxContext generatorContext, CancellationToken cancellationToken)
		{
			var classDeclaration = (ClassDeclarationSyntax)generatorContext.TargetNode;
			var semanticModel = generatorContext.SemanticModel;
			var className = classDeclaration.Identifier.Text;
			var namespaceName = generatorContext.TargetSymbol.ContainingNamespace?.ToDisplayString();
			var programName = semanticModel.Compilation.Assembly.Name ?? "";

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
					new EquatableArray<DiagnosticInfo>(diagnostics));
			}

			// check class level diagnostics and stop processing if class is invalid
			var classDiagnostics = Validation.ValidateClassDeclaration(classDeclaration);
			if (classDiagnostics.Count > 0)
			{
				return Rejected(classDiagnostics);
			}

			var config = new GeneratorConfiguration(classDeclaration, semanticModel);

			// instantiate all attribute properties with their respective arguments
			var properties = classDeclaration.Members.OfType<PropertyDeclarationSyntax>().ToList().AsReadOnly();
			var attributeFactory = new AttributeFactory(semanticModel, properties);
			var options = attributeFactory.GetOptionAttributes();
			var flags = attributeFactory.GetFlagAttributes();
			var positionals = attributeFactory.GetPositionalAttributes();
			var argumentProvider = new ArgumentProvider(config, options, flags, positionals);

			// check for errors
			var attributeDiagnostics = Validation.ValidateAttributes(argumentProvider, classDeclaration);
			if (attributeDiagnostics.Count > 0)
			{
				return Rejected(attributeDiagnostics);
			}
			// not all helptext diags are errors, some are warnings
			var helptextDiagnostics = Validation.ValidateGenerationOptions(config, argumentProvider, classDeclaration);

			return new ClassModel(
				className,
				namespaceName,
				programName,
				config,
				new EquatableArray<PropertyAndAttributeInfo>(options),
				new EquatableArray<PropertyAndAttributeInfo>(flags),
				new EquatableArray<PropertyAndAttributeInfo>(positionals),
				new EquatableArray<DiagnosticInfo>(helptextDiagnostics));
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
				config
			);
			var sourceText = textGenerator.GenerateSourceText();
			context.AddSource($"{model.ClassName}_Parser.g.cs", sourceText);
		}
	}
}
