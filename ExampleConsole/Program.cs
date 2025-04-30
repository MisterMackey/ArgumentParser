// See https://aka.ms/new-console-template for more information
using ExampleConsole;

Console.WriteLine("Hello, World!");

var opts = Options.Parse(args);
Console.WriteLine($"Input: {opts.result.Input}");
Console.WriteLine($"Output: {opts.result.Output}");
Console.WriteLine($"Verbose: {opts.result.Verbose}");
foreach (var error in opts.errors)
{
	Console.WriteLine($"Error: {error.Message}");
}