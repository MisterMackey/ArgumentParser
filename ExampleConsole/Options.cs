using ArgumentParser;

namespace ExampleConsole;

[ParameterCollection]
public partial class Options
{
	[Option("o", "output", "Output file", required: true)]
	public string Output { get; set; } = string.Empty;
	[Positional(0, "input", true)]
	public string Input { get; set; } = string.Empty;
	[Flag("v", "verbose", "Verbose output")]
	public bool Verbose { get; set; } = false;
}