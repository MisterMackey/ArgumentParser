using System.Globalization;
using ArgumentParser;
using ExampleConsole;

// The first argument selects what to parse the rest with, so the smoke test can reach every error behaviour.
// Without a scenario the arguments go to MyCommandLineArguments, which displays help and exits on errors.
return args.FirstOrDefault() switch
{
	"lenient" => RunLenient(args[1..]),
	"strict" => RunThrowing(() => { var (parsed, _) = StrictArguments.Parse(args[1..]); return $"Number={parsed.Number}\nLabel={parsed.Label}"; }),
	"required" => RunThrowing(() => { var (parsed, errors) = RequiredOnlyArguments.Parse(args[1..]); return $"Key={parsed.Key}\nNumber={parsed.Number}\nErrors={errors.Count}"; }),
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
	Console.WriteLine($"Source={parsed.Source}");
	Console.WriteLine($"Copies={parsed.Copies.ToString(CultureInfo.InvariantCulture)}");
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
