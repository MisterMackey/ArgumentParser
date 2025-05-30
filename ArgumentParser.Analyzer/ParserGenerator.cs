﻿using System.Linq;
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
			var attributeSyntaxContext = context.SyntaxProvider
			.ForAttributeWithMetadataName(
			    "ArgumentParser.ParameterCollectionAttribute", // Fully qualified name of the attribute
			    (node, _) => node is ClassDeclarationSyntax,
			    (context, _) => context
			    );

			// register output
			context.RegisterSourceOutput(attributeSyntaxContext, (productionContext, attributeContext) =>
			{
				// check class level diagnostics and stop processing if class is invalid
				var err = Validation.ValidateClassDeclaration((ClassDeclarationSyntax)attributeContext.TargetNode);
				if (err.Count > 0)
				{
					Validation.ReportDiagnostics(productionContext, err);
					return;
				}
				RunCodeGeneration(productionContext, attributeContext);
			});
		}

		private static void RunCodeGeneration(SourceProductionContext context, GeneratorAttributeSyntaxContext generatorContext)
		{
			var classDeclaration = (ClassDeclarationSyntax)generatorContext.TargetNode;
			var semanticModel = generatorContext.SemanticModel;
			var config = new GeneratorConfiguration(classDeclaration, semanticModel);
			var programName = semanticModel.Compilation.Assembly.Name ?? "";

			// instantiate all attribute properties with their respective arguments
			var properties = classDeclaration.Members.OfType<PropertyDeclarationSyntax>().ToList().AsReadOnly();
			var attributeFactory = new AttributeFactory(semanticModel, properties);
			var argumentProvider = new ArgumentProvider(attributeFactory, config);
			var helptextProvider = new HelptextProvider(
				config,
				argumentProvider,
				programName
			);
			
			// check for errors
			var attributeDiagnostics = Validation.ValidateAttributes(argumentProvider, classDeclaration);
			if (attributeDiagnostics.Count > 0)
			{
				Validation.ReportDiagnostics(context, attributeDiagnostics);
				return;
			}
			var helptextDiagnostics = Validation.ValidateGenerationOptions(config, argumentProvider, classDeclaration);
			// not all helptext diags are errors, some are warnings
			if (helptextDiagnostics.Count > 0)
			{
				Validation.ReportDiagnostics(context, helptextDiagnostics);
				// if there are errors, we stop here
				if (helptextDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
				{
					return;
				}
			}

			var textGenerator = new SourceTextGenerator(
				classDeclaration,
				argumentProvider,
				helptextProvider,
				generatorContext.TargetSymbol,
				config
			);
			var sourceText = textGenerator.GenerateSourceText();
			context.AddSource($"{classDeclaration.Identifier.Text}_Parser.g.cs", sourceText);
		}
	}
}