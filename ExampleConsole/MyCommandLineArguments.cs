using ArgumentParser;

namespace ExampleConsole;

[ParameterCollection]
public partial class MyCommandLineArguments
{
	// flag attributes are designed as true/false values
	// if -v or --Verbose is specified on the commandline
	// this value will be true after parsing.
	// Flag cannot be used on other types than bool
	// Flags do not have a 'required' property (a required flag is just a true value)
	[Flag(shortName: "v", longName: "Verbose", description: "Enable verbose output")]
	public bool Verbose { get; set; }

	// Option attributes are designed to be passed as a sort of kv pair
	// Options may be passed either via their longName or shortName
	// the argument immediately succeeding this name will be interpreted
	// as the value of the option.

	// You don't need to set both longname and shortname, one is sufficient
	// The C# propertyname also does not need to align with the longName
	// Options may be set to required, triggering an exception if not passed
	// Option properties may have any supported type
	[Option(longName: "Target", required: true)]
	public string? Output { get; set; }

	// Positional attributes are passed on the commandline without any
	// named identifier marking them. They are instead distinguished by the
	// ordinal position they take respective of eachother. They may appear at
	// any point in the commandline argument array, as long as they cannot
	// be mistaken for an option value it will parse fine. 

	// Example: '-v --Target myTarget 15' and '15 --Target myTarget -v'
	// will both produce the same output (15 is the 0th positional argument in both cases)
	// BUT
	// '--Target 15 myTarget -v' will give you an exception (myTarget will be parsed to int)

	// Positional arguments may have any supported type
	// Positional arguments may be set to required
	// Positional argument positions must form a sequence from 0..n-1
	[Positional(0, "the amount of times to repeat")]
	public int RepeatTimes { get; set; }

	// To pass a value here, use the default format that .net expects
	// example: 2025-05-05T12:00:00Z or "2025-05-05T12:00:00 +02:00"
	// make sure to quote the value if it contains spaces or special characters
	[Option(shortName: "t", longName: "TimeStamp", description: "The timestamp to use", required: false)]
	public DateTime TimeStamp { get; set; }

	// Enum types are also supported, and when combined with a Flag, can be used
	// to implement levels of verbosity or other similar features.
	// ensure the enum values follow the pattern 0,1,2,3..n
	[Flag("l", "", "A level")]
	public MyVeryOwnEnum Level { get; set; }

	// Combined with an option it lets you set a value based on a string
	[Option(shortName: "", longName: "Level", description: "A level option with a long name")]
	public MyVeryOwnEnum LevelOption { get; set; }

	// Specifying the ParsedWithMethod attribute lets you specify your own parsing method
	// This method should be a static (extension) method on the type you are using
	// This disables type checking diagnostics
	[ParsedWithMethod("ParseThis")]
	[Positional(1, "Custom type")]
	public MyVeryOwnType? MyVeryOwnType { get; set; }
}

public enum MyVeryOwnEnum
{
	Level0,
	Level1,
	Level2,
	Level3
}

public class MyVeryOwnType
{
	public static bool ParseThis(string input, out MyVeryOwnType result)
	{
		if (!string.IsNullOrEmpty(input))
		{
			result = new MyVeryOwnType();
			return true;
		}
		result = null!;
		return false;
		
	}
}