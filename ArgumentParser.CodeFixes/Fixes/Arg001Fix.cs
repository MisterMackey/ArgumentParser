using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArgumentParser.CodeFixes.Fixes
{
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Arg001Fix)), Shared]
	public class Arg001Fix : CodeFixProvider
	{
		public sealed override ImmutableArray<string> FixableDiagnosticIds =>
			["ARG001"];

		public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			var diagnostic = context.Diagnostics.First();
			var diagnosticSpan = diagnostic.Location.SourceSpan;

			// Find the class declaration identified by the diagnostic.
			var classDecl = root?.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();
			if (classDecl == null)
				return;

			context.RegisterCodeFix(
				CodeAction.Create(
					"Make class partial",
					c => MakeClassPartialAsync(context.Document, classDecl, c),
					nameof(Arg001Fix)),
				diagnostic);
		}

		private async Task<Document> MakeClassPartialAsync(Document document, ClassDeclarationSyntax classDecl, CancellationToken cancellationToken)
		{
			// Add the partial modifier if not present
			if (!classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
			{
				var newModifiers = classDecl.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
				var newClassDecl = classDecl.WithModifiers(newModifiers);
				var root = await document.GetSyntaxRootAsync(cancellationToken);
				var newRoot = root?.ReplaceNode(classDecl, newClassDecl);
				if (newRoot == null)
					return document;
				return document.WithSyntaxRoot(newRoot);
			}
			return document;
		}
	}
}
