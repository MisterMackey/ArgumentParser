using ExampleConsole;

Console.WriteLine("Hello, World!");

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