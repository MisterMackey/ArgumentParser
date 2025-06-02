using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ArgumentParser.Internal;

namespace ArgumentParser.Tests
{
	public static class TestHelper
	{
		public static (ImmutableArray<Diagnostic> Diagnostics, string Output) GetGeneratedOutput(string source)
		{
			// Create the references needed for compilation
			var references = new List<MetadataReference>
			{
				// non core references
				MetadataReference.CreateFromFile(typeof(ParameterCollectionAttribute).Assembly.Location),
			};
			// Add core references
			var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
			var trustedAssembliesPaths = trustedAssemblies?.Split(Path.PathSeparator)
				.Where(path => !string.IsNullOrWhiteSpace(path))
				.Select(path => Path.GetFullPath(path))
				.ToArray();
			var neededPlatformAssemblies = new[] {
				"System.Runtime",
				"System.Private.CoreLib",
				"System.Private.Uri"
			};
			foreach (var assemblyPath in trustedAssembliesPaths ?? Array.Empty<string>())
			{
				var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
				if (neededPlatformAssemblies.Contains(assemblyName))
				{
					references.Add(MetadataReference.CreateFromFile(assemblyPath));
				}
			}

			// Create compilation
			var compilation = CSharpCompilation.Create(
				"TestAssembly",
				new[] { CSharpSyntaxTree.ParseText(source) },
				references,
				new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
			);

			// Create an instance of the generator
			var generator = new ParserGenerator();

			// Create the driver that will run our generator
			var driver = CSharpGeneratorDriver
			    .Create(new[] { generator })
			    .RunGenerators(compilation);

			// Run the source generator
			var runResult = driver.GetRunResult();

			// Get the generated output
			var generatedOutput = runResult.GeneratedTrees.FirstOrDefault()?.ToString() ?? string.Empty;

			// Get diagnostic results from compiler itself
			var diagnostics = compilation.GetDiagnostics();
			// Get diagnostics from the generator
			var generatorDiagnostics = runResult.Diagnostics;
			// Combine both diagnostics
			diagnostics = [.. diagnostics, .. generatorDiagnostics];

			return (diagnostics, generatedOutput);
		}
	}
}