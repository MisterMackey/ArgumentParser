using ExampleConsole;

Console.WriteLine("Hello, World!");

var (myArgs, err) = MyCommandLineArguments.Parse(args);
Console.WriteLine($"Repeat: {myArgs.RepeatTimes}");
Console.WriteLine($"Output: {myArgs.Output}");
Console.WriteLine($"Verbose: {myArgs.Verbose}");
Console.WriteLine($"Timestamp: {myArgs.TimeStamp}");
foreach (var error in err)
{
	Console.WriteLine($"Error: {error.Message}");
}