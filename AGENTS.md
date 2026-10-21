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
make smoke-test            # publishes ExampleConsole as a native AOT executable against the locally packed nupkg and runs scripts/smoke-test.sh against it
make all                   # analyzer + example + tests
make help                  # lists every target
```

Run a subset of tests directly (the xunit project targets net10.0, Debug only; never Release, stack traces are wanted):

```bash
dotnet test ArgumentParser.Tests --filter "FullyQualifiedName~HelpTextTests"
dotnet test ArgumentParser.Tests --filter "FullyQualifiedName=ArgumentParser.Tests.BasicSourceTextGeneratorTests.Generator_DeclaresParseMethod"
```

Gotchas with the Makefile:
- `make build-example` / `restore-example` deliberately consumes the example as a real NuGet consumer, to prove the packed nupkg (analyzer DLL under `analyzers/dotnet/cs/`) works end to end. `dotnet nuget locals all --clear` is there because every local build reuses the same version, so a cached copy would be served stale. `dotnet add ... --source ArgumentParser/bin/nupkg --prerelease` is pinned with `--version` taken from `ArgumentParser/ArgumentParser.csproj`, because the (gitignored) nupkg folder accumulates old packages and without the pin the highest one wins (it used to rewrite `ExampleConsole/ExampleConsole.csproj` to e.g. 1.4.0 instead of 1.3.0). Bumping `<Version>` therefore also needs the `PackageReference` in `ExampleConsole.csproj` updated, which `make restore-example` does.
- `build-example` runs `dotnet publish -c Release`, so the smoke test exercises the real NativeAOT binary (needs clang and zlib; `TreatWarningsAsErrors` makes trim/AOT warnings fail the build). A plain `dotnet build` only gives a JIT-run apphost plus a managed dll, and `smoke-test.sh` refuses such a binary. `ExampleConsole` pins `RuntimeIdentifier` to `linux-x64` (AOT ILCompiler package workaround), so the binary is at `ExampleConsole/bin/Release/net10.0/linux-x64/publish/ExampleConsole`, which is what `smoke-test` runs.

## Architecture
Three projects matter; keep the split in mind because it is not obvious from the names:

- **`ArgumentParser/`** (assembly `Aot.ArgumentParser`, multi-targets netstandard2.0 to net10.0): the *runtime* library the user's program references. Contains the attributes (`Attributes.cs`), config enums, `ArgumentTokenizer`, `Token` types, and the `ArgumentParserException` hierarchy. Generated code calls into this (`ArgumentParser.ArgumentTokenizer`, `OptionToken`, ...). It is also the NuGet packaging project: it embeds the Analyzer DLL from `ArgumentParser.Analyzer/bin/Release/netstandard2.0/` under `analyzers/dotnet/cs/`, so the Analyzer must be built in **Release** before packing (the netstandard2.0 `ProjectReference` with `ReferenceOutputAssembly=false` enforces build order). `ArgumentParser/Internal/*.cs` are empty, tracked leftovers; the real code lives in the Analyzer project.
- **`ArgumentParser.Analyzer/`** (netstandard2.0, Roslyn 4.12): the generator itself. Pipeline in `ParserGenerator.cs`:
  1. `ForAttributeWithMetadataName("ArgumentParser.ParameterCollectionAttribute")` finds classes.
  2. `Validation.ValidateClassDeclaration` (ARG001/002/006 etc.), then `GeneratorConfiguration` (`Utilities/Configuration.cs`) reads the `[ParameterCollection]` constructor args.
  3. `AttributeFactory` (`Utilities/`) turns property attributes into `PropertyAndAttributeInfo`; `ArgumentProvider`/`HelptextProvider` (`CodeProviders/`) group them and build help text.
  4. `Validation.ValidateAttributes` / `ValidateGenerationOptions` report diagnostics (errors stop generation, warnings don't).
  5. `SourceTextGenerator` emits `<ClassName>_Parser.g.cs` as a string, with `AssemblyVersionProvider.FullSemVer` stamped into `[GeneratedCode]`.
  The generator must stay netstandard2.0 and satisfy `EnforceExtendedAnalyzerRules`. The generated file starts with its own `using System; System.Collections.Generic; System.Linq;` so it compiles in projects without `ImplicitUsings`. Names and descriptions go into the generated code through `Literal(...)` (Roslyn `SymbolDisplay.FormatLiteral`), and the help text raw string picks a delimiter longer than any quote run in its content; keep using those instead of interpolating raw strings into generated code. Per-call state such as `requiredProperties` must be a local of `Parse`, never static.
- **`ArgumentParser.Tests/`** (xunit, net10.0): `Helpers/TestHelper.GetGeneratedOutput(source)` compiles a source string and runs `ParserGenerator` through `CSharpGeneratorDriver`, returning diagnostics plus the first generated file. `Helpers/SourceCodeBuilder` assembles test class source from `PropertyAndAttributeInfo`s. Tests assert on substrings of generated text, so changing emitted code formatting can break many tests. `ArgumentTokenizerTests` tests the runtime tokenizer directly. `RuntimeLibraryTests` covers the attributes, exceptions and tokens; `AttributeExtractionTests` drives `AttributeFactory`, `GeneratorConfiguration` and the pipeline components directly from a `TestHelper.CreateCompilation` compilation; `DiagnosticRuleTests` covers every ARG rule. `IncrementalityTests` tracks the driver's pipeline steps to check that unrelated edits reuse the cached output (one test is skipped until the generator is made incremental). `GeneratedParserBehaviourTests` (via `Helpers/GeneratedParser` and `ParserSource`) compiles the generated parser with a test class, loads it, calls `Parse` and asserts on results; prefer it for behaviour changes. Use `ThrowNever`/`ThrowIfAnyError` plus `GenerateTextOnly`/`GenerateArgumentOnly` there, because `GenerateAll` or the default `DisplayHelpAndExit` call `Environment.Exit` and would kill the test host. If you find a generator bug, add a failing test here first (a `Skip` with the reason is fine until it is fixed).
- `ExampleConsole/` is the demo/smoke-test app (`PublishAot`, consumes the locally built nupkg). `Program.cs` takes a scenario as its first argument (`lenient`, `strict`, `required`, otherwise the default `MyCommandLineArguments`) so every `BehaviourOnError` mode and every supported property type can be reached; the scenario classes live in `ScenarioArguments.cs`. `scripts/smoke-test.sh` runs the binary with ~50 command lines and checks exit code and output lines; add a `check ...` call there when adding behaviour to the example.
- `ArgumentParser.CodeFixes/` is gitignored build leftovers; code fixes are unimplemented (see `TODO`).

### Where changes usually land
- **New or changed diagnostic (ARGnnn)**: add the descriptor in `GeneratorDiagnostics.cs`, the check in `Validation.cs`, a rule entry in `ArgumentParser.Analyzer/AnalyzerReleases.UnShipped.md` (note the capital S), a doc page `ArgumentParser.Analyzer/docs/rules/ARGnnn.md`, and a case in `DiagnosticTests.cs`.
- **New attribute option or supported type**: touch `ArgumentParser/Attributes.cs` (runtime), `Utilities/AttributeFactory.cs` (extraction), `Validation.cs`, `SourceTextGenerator.cs` (emission), and the README type list. The tokenizer only changes if token semantics change.
- **Tokenizer behaviour** (flag repeat counts, short-name clusters like `-llvt`) lives only in the runtime `ArgumentTokenizer`; the generated `Parse` consumes the resulting tokens.

## Versioning and releases
- Version bumps come from GitVersion (`GitVersion.yml`): commit-message tags `+semver: fix|minor|major` or branch names `fix/`, `feature/`, `breaking/`.
- The version string exists in `ArgumentParser.Analyzer/CodeProviders/AssemblyVersionProvider.cs`, `ArgumentParser/ArgumentParser.csproj` (`<Version>`), and `ExampleConsole.csproj`; `make set-version-stable` / `set-pre-release-version` rewrite the first two with `sed` and **`git commit --amend`** the latest commit. Don't run those targets casually. Releases are cut locally (`make pre-release` / `make release-stable`); no CI workflow does it.
- Add user-visible changes to `CHANGELOG.md` under "UnReleased".
