# AGENTS.md

C# incremental source generator (published as the NuGet package `Aot.ArgumentParser`). Users annotate a `partial` class with `[ParameterCollection]` and properties with `[Flag]`/`[Option]`/`[Positional]`. The generator adds a reflection-free, AOT-friendly `static Parse(string[] args)` returning `(instance, List<ArgumentParserException>)`. User-facing docs are in `README.md`; release history in `CHANGELOG.md`.

## Code style
- Indent with **tabs** (see `.editorconfig`), LF line endings. Use tabs in any code snippet you write, including snippets inside generated-code string templates and markdown.
- Don't add comments to the code that the generator emits unless essential.
- XML doc comments are expected on public members: both library projects build with `AnalysisMode=All` and `GenerateDocumentationFile`.

## Commands
Everything goes through the `Makefile` (stamp files in `.build-stamps/` skip up-to-date steps; `make clean` resets them).

```bash
make build-analyzer        # dotnet build ArgumentParser -c Release (also builds the Analyzer and packs the nupkg into ArgumentParser/bin/nupkg)
make test                  # builds tests in Debug, then runs them with coverage
make smoke-test            # builds ExampleConsole against the locally packed nupkg and runs it 3 ways
make all                   # analyzer + example + tests
make help                  # lists every target
```

Run a subset of tests directly (the xunit project targets net9.0, Debug only; never Release, stack traces are wanted):

```bash
dotnet test ArgumentParser.Tests --filter "FullyQualifiedName~HelpTextTests"
dotnet test ArgumentParser.Tests --filter "FullyQualifiedName=ArgumentParser.Tests.BasicSourceTextGeneratorTests.Generator_DeclaresParseMethod"
```

Gotchas with the Makefile:
- `make build-example` / `restore-example` runs `dotnet nuget locals all --clear` (wipes the global NuGet cache) and `dotnet add ExampleConsole package ... --prerelease`, which **rewrites `ExampleConsole/ExampleConsole.csproj`**. Check `git diff` afterwards and don't commit unintended version changes there.
- `smoke-test` executes `ExampleConsole/bin/Debug/net9.0/ExampleConsole`, but `ExampleConsole` now targets `net10.0` (and CI installs .NET 9 only), so that path can be stale. Verify the path/TFM before relying on the target.
- `.github/workflows/main-build-and-tag.yaml` calls `make release`, which is not a Makefile target (only `pre-release`/`release-stable` exist).

## Architecture
Three projects matter; keep the split in mind because it is not obvious from the names:

- **`ArgumentParser/`** (assembly `Aot.ArgumentParser`, multi-targets netstandard2.0 to net10.0): the *runtime* library the user's program references. Contains the attributes (`Attributes.cs`), config enums, `ArgumentTokenizer`, `Token` types, and the `ArgumentParserException` hierarchy. Generated code calls into this (`ArgumentParser.ArgumentTokenizer`, `OptionToken`, ...). It is also the NuGet packaging project: it embeds the Analyzer DLL from `ArgumentParser.Analyzer/bin/Release/netstandard2.0/` under `analyzers/dotnet/cs/`, so the Analyzer must be built in **Release** before packing (the netstandard2.0 `ProjectReference` with `ReferenceOutputAssembly=false` enforces build order). `ArgumentParser/Internal/*.cs` are empty, tracked leftovers; the real code lives in the Analyzer project.
- **`ArgumentParser.Analyzer/`** (netstandard2.0, Roslyn 4.12): the generator itself. Pipeline in `ParserGenerator.cs`:
  1. `ForAttributeWithMetadataName("ArgumentParser.ParameterCollectionAttribute")` finds classes.
  2. `Validation.ValidateClassDeclaration` (ARG001/002/006 etc.), then `GeneratorConfiguration` (`Utilities/Configuration.cs`) reads the `[ParameterCollection]` constructor args.
  3. `AttributeFactory` (`Utilities/`) turns property attributes into `PropertyAndAttributeInfo`; `ArgumentProvider`/`HelptextProvider` (`CodeProviders/`) group them and build help text.
  4. `Validation.ValidateAttributes` / `ValidateGenerationOptions` report diagnostics (errors stop generation, warnings don't).
  5. `SourceTextGenerator` emits `<ClassName>_Parser.g.cs` as a string, with `AssemblyVersionProvider.FullSemVer` stamped into `[GeneratedCode]`.
  The generator must stay netstandard2.0 and satisfy `EnforceExtendedAnalyzerRules`. Generated code deliberately uses no `using` directives and only fully qualified names, so it works in any user project.
- **`ArgumentParser.Tests/`** (xunit, net9.0): `Helpers/TestHelper.GetGeneratedOutput(source)` compiles a source string and runs `ParserGenerator` through `CSharpGeneratorDriver`, returning diagnostics plus the first generated file. `Helpers/SourceCodeBuilder` assembles test class source from `PropertyAndAttributeInfo`s. Tests assert on substrings of generated text, so changing emitted code formatting can break many tests. `ArgumentTokenizerTests` tests the runtime tokenizer directly.
- `ExampleConsole/` is the demo/smoke-test app (`PublishAot`, consumes the locally built nupkg).
- `ArgumentParser.CodeFixes/` is gitignored build leftovers; code fixes are unimplemented (see `TODO`).

### Where changes usually land
- **New or changed diagnostic (ARGnnn)**: add the descriptor in `GeneratorDiagnostics.cs`, the check in `Validation.cs`, a rule entry in `ArgumentParser.Analyzer/AnalyzerReleases.UnShipped.md` (note the capital S), a doc page `ArgumentParser.Analyzer/docs/rules/ARGnnn.md`, and a case in `DiagnosticTests.cs`.
- **New attribute option or supported type**: touch `ArgumentParser/Attributes.cs` (runtime), `Utilities/AttributeFactory.cs` (extraction), `Validation.cs`, `SourceTextGenerator.cs` (emission), and the README type list. The tokenizer only changes if token semantics change.
- **Tokenizer behaviour** (flag repeat counts, short-name clusters like `-llvt`) lives only in the runtime `ArgumentTokenizer`; the generated `Parse` consumes the resulting tokens.

## Versioning and releases
- Version bumps come from GitVersion (`GitVersion.yml`): commit-message tags `+semver: fix|minor|major` or branch names `fix/`, `feature/`, `breaking/`.
- The version string exists in `ArgumentParser.Analyzer/CodeProviders/AssemblyVersionProvider.cs`, `ArgumentParser/ArgumentParser.csproj` (`<Version>`), and `ExampleConsole.csproj`; `make set-version-stable` / `set-pre-release-version` rewrite the first two with `sed` and **`git commit --amend`** the latest commit. Don't run those targets casually. CI (`main-build-and-tag.yaml`) does this on push to `main`.
- Add user-visible changes to `CHANGELOG.md` under "UnReleased".
