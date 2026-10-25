using System.Collections.Immutable;
using ArgumentParser.Internal;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ArgumentParser.Tests
{
	public static class TestHelper
	{
		// further files are named Other1.cs, Other2.cs, ... so a test can see in which file a diagnostic is
		public static CSharpCompilation CreateCompilation(string source, params string[] otherFiles)
		{
			// Create the references needed for compilation
			var references = new List<MetadataReference>
			{
				// non core references
				// ArgumentParser ref
				MetadataReference.CreateFromFile(typeof(ParameterCollectionAttribute).Assembly.Location),
				// ArgumentParser.Tests ref
				MetadataReference.CreateFromFile(typeof(TestHelper).Assembly.Location),
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

			return CSharpCompilation.Create(
				"TestAssembly",
				new[] { CSharpSyntaxTree.ParseText(source) }.Concat(otherFiles.Select((text, index) => CSharpSyntaxTree.ParseText(text, path: $"Other{index + 1}.cs"))),
				references,
				new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
			);
		}

		public static (ImmutableArray<Diagnostic> Diagnostics, string Output) GetGeneratedOutput(string source, params string[] otherFiles)
		{
			var compilation = CreateCompilation(source, otherFiles);

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
