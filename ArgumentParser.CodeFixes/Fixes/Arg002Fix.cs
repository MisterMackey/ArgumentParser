using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArgumentParser.CodeFixes.Fixes
{
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Arg002Fix)), Shared]
	public class Arg002Fix : CodeFixProvider
	{
		public sealed override ImmutableArray<string> FixableDiagnosticIds =>
			["ARG002"];

		public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			var diagnostic = context.Diagnostics.First();
			var diagnosticSpan = diagnostic.Location.SourceSpan;

			var classDecl = root?.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();
			if (classDecl == null)
				return;

			context.RegisterCodeFix(
				CodeAction.Create(
					"Remove existing Parse method",
					c => RemoveParseMethodAsync(context.Document, classDecl, c),
					nameof(Arg002Fix)),
				diagnostic);
		}

		private async Task<Document> RemoveParseMethodAsync(Document document, ClassDeclarationSyntax classDecl, CancellationToken cancellationToken)
		{
			var parseMethod = classDecl.Members
				.OfType<MethodDeclarationSyntax>()
				.FirstOrDefault(m =>
					m.Identifier.Text == "Parse" &&
					m.ParameterList.Parameters.Count == 1 &&
					m.ParameterList.Parameters[0].Type is ArrayTypeSyntax arrayType &&
					arrayType.ElementType is PredefinedTypeSyntax pts &&
					pts.Keyword.IsKind(SyntaxKind.StringKeyword)
				);

			if (parseMethod == null)
				return document;

			var newClassDecl = classDecl.RemoveNode(parseMethod, SyntaxRemoveOptions.KeepNoTrivia);
			if (newClassDecl == null)
				return document;
			var root = await document.GetSyntaxRootAsync(cancellationToken);
			var newRoot = root?.ReplaceNode(classDecl, newClassDecl);
			if (newRoot == null)
				return document;
			return document.WithSyntaxRoot(newRoot);
		}
	}
}
