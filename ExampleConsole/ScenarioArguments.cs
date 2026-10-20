using ArgumentParser;

namespace ExampleConsole;

/// <summary>
/// Exercises every supported property type and attribute without ever exiting or throwing,
/// so the caller decides what to do with the collected errors. Selected with the "lenient" scenario.
/// </summary>
[ParameterCollection(behaviourOnError: BehaviourOnError.ThrowNever)]
public partial class LenientArguments
{
	[Flag("v", "verbose", "Verbose output")]
	public bool Verbose { get; set; }

	[Flag("q", "quiet", "Quiet output")]
	public bool Quiet { get; set; }

	// repeating the flag (-ddd) raises the level
	[Flag("d", "debug", "Debug level")]
	public MyVeryOwnEnum Debug { get; set; }

	[Option("n", "name", "Name to use", required: true)]
	public string Name { get; set; } = string.Empty;

	[Option("c", "count", "A number")]
	public int Count { get; set; }

	[Option(longName: "big", description: "A big number")]
	public long Big { get; set; }

	[Option("r", "ratio", "A fraction")]
	public double Ratio { get; set; }

	[Option(longName: "price", description: "An amount")]
	public decimal Price { get; set; }

	[Option(longName: "id", description: "An identifier")]
	public Guid Id { get; set; }

	[Option(longName: "site", description: "An address")]
	public Uri Site { get; set; } = new("about:blank");

	[Option("w", "wait", "A duration")]
	public TimeSpan Wait { get; set; }

	[Option("t", "when", "A moment in time")]
	public DateTime When { get; set; }

	[Option(longName: "letter", description: "A single character")]
	public char Letter { get; set; }

	[Option(longName: "enabled", description: "A boolean value")]
	public bool Enabled { get; set; }

	[Option("m", "mode", "An enum value")]
	public MyVeryOwnEnum Mode { get; set; }

	[ParsedWithMethod("TryParse")]
	[Option(longName: "point", description: "A custom type, given as x,y")]
	public Point? Point { get; set; }

	[Positional(0, "The source", required: true)]
	public string Source { get; set; } = string.Empty;

	[Positional(1, "How many copies")]
	public int Copies { get; set; }
}

/// <summary>
/// Throws on the first problem it finds. Selected with the "strict" scenario.
/// </summary>
[ParameterCollection(HelpTextGeneration.GenerateTextOnly, behaviourOnError: BehaviourOnError.ThrowIfAnyError)]
public partial class StrictArguments
{
	[Option("n", "number", "A number")]
	public int Number { get; set; }

	[Positional(0, "A label")]
	public string? Label { get; set; }
}

/// <summary>
/// Only throws when something required is missing, other problems are collected. Selected with the "required" scenario.
/// </summary>
[ParameterCollection(HelpTextGeneration.GenerateTextOnly, behaviourOnError: BehaviourOnError.ThrowIfMissingRequired)]
public partial class RequiredOnlyArguments
{
	[Option("k", "key", "A key", required: true)]
	public string Key { get; set; } = string.Empty;

	[Option("n", "number", "A number")]
	public int Number { get; set; }
}

public class Point
{
	public int X { get; private set; }
	public int Y { get; private set; }

	public static bool TryParse(string input, out Point result)
	{
		var parts = input.Split(',');
		if (parts.Length == 2 && int.TryParse(parts[0], out var x) && int.TryParse(parts[1], out var y))
		{
			result = new Point { X = x, Y = y };
			return true;
		}
		result = null!;
		return false;
	}
}
