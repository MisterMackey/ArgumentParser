using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ArgumentParser.Internal;

namespace ArgumentParser.Tests;

/// <summary>
/// Runs the generator over a source string, compiles the result together with the input and loads it,
/// so tests can call the generated Parse method and inspect what it produces.
/// </summary>
public sealed class GeneratedParser
{
	private const string DefaultNamespace = "ArgumentParser.Tests";
	private const string ImplicitUsingsSource = "global using System; global using System.Collections.Generic; global using System.IO; global using System.Linq;";
	private readonly Assembly assembly;
	private readonly string className;

	private GeneratedParser(Assembly assembly, string className, string generatedSource)
	{
		this.assembly = assembly;
		this.className = className;
		GeneratedSource = generatedSource;
	}

	/// <summary>
	/// The text of the first file the generator produced.
	/// </summary>
	public string GeneratedSource { get; }

	/// <summary>
	/// Compiles the source plus the generated parser and loads the result.
	/// Fails the test with the compiler errors if the combination does not compile.
	/// The generated file contains no using directives, so by default the compilation gets the global usings
	/// that ImplicitUsings provides in an SDK style project.
	/// </summary>
	public static GeneratedParser Compile(string source, string className = "TestClass", bool implicitUsings = true)
	{
		var syntaxTrees = new List<SyntaxTree> { CSharpSyntaxTree.ParseText(source) };
		if (implicitUsings)
		{
			syntaxTrees.Add(CSharpSyntaxTree.ParseText(ImplicitUsingsSource));
		}
		var compilation = CSharpCompilation.Create(
			"GeneratedParserTestAssembly_" + Guid.NewGuid().ToString("N"),
			syntaxTrees,
			GetReferences(),
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

		var driver = CSharpGeneratorDriver
			.Create(new ParserGenerator())
			.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);

		var errors = generatorDiagnostics
			.Concat(outputCompilation.GetDiagnostics())
			.Where(d => d.Severity == DiagnosticSeverity.Error)
			.ToList();
		var generatedSource = driver.GetRunResult().GeneratedTrees.FirstOrDefault()?.ToString() ?? string.Empty;
		if (errors.Count > 0)
		{
			throw new InvalidOperationException(
				"Source plus generated code did not compile:" + Environment.NewLine
				+ string.Join(Environment.NewLine, errors.Select(e => e.ToString()))
				+ Environment.NewLine + "--- generated source ---" + Environment.NewLine + generatedSource);
		}

		using var stream = new MemoryStream();
		var emitResult = outputCompilation.Emit(stream);
		if (!emitResult.Success)
		{
			throw new InvalidOperationException("Emit failed: " + string.Join(Environment.NewLine, emitResult.Diagnostics));
		}
		stream.Position = 0;
		var context = new AssemblyLoadContext(null, isCollectible: true);
		var assembly = context.LoadFromStream(stream);
		return new GeneratedParser(assembly, className, generatedSource);
	}

	/// <summary>
	/// Calls the generated static Parse method.
	/// An exception thrown by the generated code is rethrown as is.
	/// </summary>
	public ParseOutcome Parse(params string[] args)
	{
		var type = GetParameterCollectionType();
		var method = type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, [typeof(string[])])
			?? throw new InvalidOperationException($"{type.FullName} has no public static Parse(string[]) method.");
		object? tuple;
		try
		{
			tuple = method.Invoke(null, [args]);
		}
		catch (TargetInvocationException ex) when (ex.InnerException is not null)
		{
			System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
			throw;
		}
		var items = (ITuple)tuple!;
		var errors = ((IEnumerable<ArgumentParserException>)items[1]!).ToList();
		return new ParseOutcome(items[0]!, errors);
	}

	/// <summary>
	/// Reads a public constant or static field of the generated class (for example HelpText).
	/// </summary>
	public T? GetStaticMember<T>(string name)
	{
		var field = GetParameterCollectionType().GetField(name, BindingFlags.Public | BindingFlags.Static);
		return (T?)field?.GetValue(null);
	}

	private Type GetParameterCollectionType()
	{
		return assembly.GetType($"{DefaultNamespace}.{className}")
			?? throw new InvalidOperationException($"Type {DefaultNamespace}.{className} not found in compiled assembly.");
	}

	private static ImmutableArray<MetadataReference> GetReferences()
	{
		var references = new List<MetadataReference>
		{
			MetadataReference.CreateFromFile(typeof(ParameterCollectionAttribute).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(GeneratedParser).Assembly.Location),
		};
		var trustedAssemblies = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty)
			.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
		foreach (var path in trustedAssemblies)
		{
			var name = Path.GetFileNameWithoutExtension(path);
			if (name == "System" || name == "netstandard" || name == "mscorlib" || name.StartsWith("System.", StringComparison.Ordinal))
			{
				references.Add(MetadataReference.CreateFromFile(path));
			}
		}
		return [.. references];
	}
}

/// <summary>
/// The tuple returned by a generated Parse method, with helpers to read properties of the parsed instance.
/// </summary>
public sealed class ParseOutcome(object instance, IReadOnlyList<ArgumentParserException> errors)
{
	/// <summary>
	/// The instance of the parameter collection class.
	/// </summary>
	public object Instance { get; } = instance;

	/// <summary>
	/// The errors collected while parsing.
	/// </summary>
	public IReadOnlyList<ArgumentParserException> Errors { get; } = errors;

	/// <summary>
	/// Reads a public instance property from the parsed instance.
	/// </summary>
	public T? Get<T>(string propertyName)
	{
		var property = Instance.GetType().GetProperty(propertyName)
			?? throw new InvalidOperationException($"{Instance.GetType().FullName} has no property {propertyName}.");
		return (T?)property.GetValue(Instance);
	}
}
