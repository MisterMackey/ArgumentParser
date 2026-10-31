using System.Globalization;
using ArgumentParser;
using ExampleConsole;

// The first argument selects what to parse the rest with, so the smoke test can reach every error behaviour.
// Without a scenario the arguments go to MyCommandLineArguments, which displays help and exits on errors.
// This is for test and demo purposes, in a real application you would just pass argv to the generated top-level parse method.
return args.FirstOrDefault() switch
{
	"lenient" => RunLenient(args[1..]),
	"strict" => RunThrowing(() => { var (parsed, _) = StrictArguments.Parse(args[1..]); return $"Number={parsed.Number}\nLabel={parsed.Label}"; }),
	"required" => RunThrowing(() => { var (parsed, errors) = RequiredOnlyArguments.Parse(args[1..]); return $"Key={parsed.Key}\nNumber={parsed.Number}\nErrors={errors.Count}"; }),
	"lists" => RunLists(args[1..]),
	"commands" => RunCommands(args[1..]),
	"commandlist" => RunCommandList(args[1..]),
	"mcp" => RunMcp(args[1..]),
	_ => RunDefault(args),
};

static int RunDefault(string[] args)
{
	var (myArgs, err) = MyCommandLineArguments.Parse(args);
	Console.WriteLine($"Repeat: {myArgs.RepeatTimes}");
	Console.WriteLine($"Output: {myArgs.Output}");
	Console.WriteLine($"Verbose: {myArgs.Verbose}");
	Console.WriteLine($"Timestamp: {myArgs.TimeStamp}");
	Console.WriteLine($"Level: {myArgs.Level}");
	Console.WriteLine($"LevelOption: {myArgs.LevelOption}");
	foreach (var error in err)
	{
		Console.WriteLine($"Error: {error.Message}");
	}
	return 0;
}

static int RunLenient(string[] args)
{
	var (parsed, errors) = LenientArguments.Parse(args);
	Console.WriteLine($"Verbose={parsed.Verbose}");
	Console.WriteLine($"Quiet={parsed.Quiet}");
	Console.WriteLine($"Debug={parsed.Debug}");
	Console.WriteLine($"Name={parsed.Name}");
	Console.WriteLine($"Count={parsed.Count.ToString(CultureInfo.InvariantCulture)}");
	Console.WriteLine($"Big={parsed.Big.ToString(CultureInfo.InvariantCulture)}");
	Console.WriteLine($"Ratio={parsed.Ratio.ToString(CultureInfo.InvariantCulture)}");
	Console.WriteLine($"Price={parsed.Price.ToString(CultureInfo.InvariantCulture)}");
	Console.WriteLine($"Id={parsed.Id}");
	Console.WriteLine($"Site={parsed.Site}");
	Console.WriteLine($"Wait={parsed.Wait.ToString("c", CultureInfo.InvariantCulture)}");
	Console.WriteLine($"When={parsed.When.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)}");
	Console.WriteLine($"Letter={(parsed.Letter == '\0' ? "" : parsed.Letter.ToString())}");
	Console.WriteLine($"Enabled={parsed.Enabled}");
	Console.WriteLine($"Mode={parsed.Mode}");
	Console.WriteLine($"Point={(parsed.Point is null ? "" : $"{parsed.Point.X},{parsed.Point.Y}")}");
	Console.WriteLine($"Retries={parsed.Retries?.ToString(CultureInfo.InvariantCulture) ?? "null"}");
	Console.WriteLine($"Redirect={parsed.Redirect?.ToString(CultureInfo.InvariantCulture) ?? "null"}");
	Console.WriteLine($"Source={parsed.Source}");
	Console.WriteLine($"Copies={parsed.Copies.ToString(CultureInfo.InvariantCulture)}");
	foreach (var error in errors)
	{
		Console.WriteLine($"Error: {error.GetType().Name}: {error.Message}");
	}
	return errors.Count == 0 ? 0 : 1;
}

static int RunLists(string[] args)
{
	var (parsed, errors) = ListArguments.Parse(args);
	static string Join<T>(IEnumerable<T>? values) => values is null ? "null" : string.Join("|", values.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture)));
	Console.WriteLine($"Verbose={parsed.Verbose}");
	Console.WriteLine($"Include={Join(parsed.Include)}");
	Console.WriteLine($"Numbers={Join(parsed.Numbers)}");
	Console.WriteLine($"Modes={Join(parsed.Modes)}");
	Console.WriteLine($"Points={Join(parsed.Points?.Select(p => $"{p.X},{p.Y}"))}");
	Console.WriteLine($"Exclude={Join(parsed.Exclude)}");
	Console.WriteLine($"Target={parsed.Target}");
	Console.WriteLine($"Files={Join(parsed.Files)}");
	foreach (var error in errors)
	{
		Console.WriteLine($"Error: {error.GetType().Name}: {error.Message}");
	}
	return errors.Count == 0 ? 0 : 1;
}

// the tree of commands in FileToolCommands.cs; like MyCommandLineArguments it displays help and exits on errors
static int RunCommands(string[] args)
{
	var (tool, _) = FileTool.Parse(args);
	PrintTool(tool);
	return 0;
}

// the tree as MCP tools: without arguments the manifest, otherwise a tool call (the name of a tool and its arguments as JSON) bound to the tree
static int RunMcp(string[] args)
{
	if (args.Length == 0)
	{
		Console.WriteLine(FileTool.McpManifest);
		return 0;
	}
	using var arguments = System.Text.Json.JsonDocument.Parse(args.Length > 1 ? args[1] : "{}");
	var (tool, errors) = FileTool.Bind(args[0], arguments.RootElement);
	PrintTool(tool);
	foreach (var error in errors)
	{
		Console.WriteLine($"Error: {error.GetType().Name} ({error.Key ?? "no key"}): {error.Message}");
	}
	return errors.Count == 0 ? 0 : 1;
}

static void PrintTool(FileTool tool)
{
	Console.WriteLine($"Root={tool.Root}");
	Console.WriteLine($"Command={tool.SelectedCommand}");
	switch (tool.Selected)
	{
		case ListDirectories list:
			Console.WriteLine($"ShowHidden={list.ShowHidden}");
			Console.WriteLine($"Depth={list.Depth?.ToString(CultureInfo.InvariantCulture) ?? "null"}");
			Console.WriteLine($"Exclude={(list.Exclude is null ? "null" : string.Join("|", list.Exclude))}");
			break;
		case CreateDirectory create:
			Console.WriteLine($"Name={create.Name}");
			break;
		case DeleteFile delete:
			Console.WriteLine($"DryRun={tool.Files!.DryRun}");
			Console.WriteLine($"FilePath={delete.FilePath}");
			Console.WriteLine($"Force={delete.Force}");
			break;
	}
}

// the list of commands the root of the tree gets; with arguments, they are parsed by the command they start with, which never exits
static int RunCommandList(string[] args)
{
	if (args.Length == 0)
	{
		foreach (var command in FileTool.Commands)
		{
			Console.WriteLine($"{string.Join("_", command.Path)}: {command.Description} ({command.Annotations})");
		}
		return 0;
	}
	var match = FileTool.Commands.FirstOrDefault(c => c.Path.SequenceEqual(args.Take(c.Path.Count)));
	if (match is null)
	{
		Console.WriteLine("No such command");
		return 1;
	}
	var (result, errors) = match.Parse(args[match.Path.Count..]);
	Console.WriteLine($"Result={result.GetType().Name}");
	if (result is CreateDirectory create)
	{
		Console.WriteLine($"Name={create.Name}");
	}
	foreach (var error in errors)
	{
		Console.WriteLine($"Error: {error.GetType().Name}: {error.Message}");
	}
	return errors.Count == 0 ? 0 : 1;
}

static int RunThrowing(Func<string> parse)
{
	try
	{
		Console.WriteLine(parse());
		return 0;
	}
	catch (AggregateException ex)
	{
		Console.WriteLine($"Threw: {ex.GetType().Name}: {string.Join(", ", ex.InnerExceptions.Select(inner => inner.GetType().Name))}");
		return 3;
	}
}
