using System.Linq;
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

			// instantiate all attribute properties with their respective arguments
			var semanticModel = generatorContext.SemanticModel;
			var properties = classDeclaration.Members.OfType<PropertyDeclarationSyntax>().ToList().AsReadOnly();
			var attributeFactory = new AttributeFactory(semanticModel, properties);
			var argumentProvider = new UserSpecifiedArgumentProvider(attributeFactory);
			
			// check validity of attributes and stop processing if any are invalid
			var err = Validation.ValidateAttributes(argumentProvider, classDeclaration);
			if (err.Count > 0)
			{
				Validation.ReportDiagnostics(context, err);
				return;
			}

			var textGenerator = new SourceTextGenerator(
				classDeclaration,
				argumentProvider,
				generatorContext.TargetSymbol
			);
			var sourceText = textGenerator.GenerateSourceText();
			context.AddSource($"{classDeclaration.Identifier.Text}_Parser.g.cs", sourceText);
		}
	}
}