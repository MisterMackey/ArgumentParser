using ArgumentParser;

namespace ExampleConsole;

// A tree of commands, in the form 'appname subcommand action': myFileManagingTool directories list --showhidden
// Selected with the "commands" scenario, so the smoke test runs it as 'ExampleConsole commands directories list --showhidden'.
// The root is the application, a group holds other commands, and a leaf (which also has ParameterCollection) does the work.
// The child commands are the properties whose type is a Command class; only the ones that were typed are filled in.
// The tree is also described as MCP tools (FileTool.McpManifest) and can bind a tool call (FileTool.Bind), see the "mcp" scenario.

/// <summary>
/// The root of the tree. Its options come before the first command word.
/// </summary>
[CommandApp("myFileManagingTool", "Manage files and directories", EmitMcpManifest = true, EmitJsonBinder = true)]
public partial class FileTool
{
	[Option("r", "root", "Directory to work in")]
	public string Root { get; set; } = ".";

	public DirectoriesCommand? Directories { get; private set; }
	public FilesCommand? Files { get; private set; }
}

/// <summary>
/// A group with two actions.
/// </summary>
[Command("directories", "Work with directories")]
public partial class DirectoriesCommand
{
	public ListDirectories? List { get; private set; }
	public CreateDirectory? Create { get; private set; }
}

/// <summary>
/// A group with an option of its own, given between 'files' and the action.
/// </summary>
[Command("files", "Work with files")]
public partial class FilesCommand
{
	[Flag("n", "dryrun", "Only show what would be done")]
	public bool DryRun { get; set; }

	public DeleteFile? Delete { get; private set; }
}

/// <summary>
/// A leaf: its arguments are declared like those of any other parameter collection.
/// </summary>
[ParameterCollection(Annotations = CommandAnnotations.ReadOnly)]
[Command("list", "List the directories")]
public partial class ListDirectories
{
	[Flag("", "showhidden", "Include hidden directories")]
	public bool ShowHidden { get; set; }

	[Option("d", "depth", "How many levels deep to list")]
	public int? Depth { get; set; }

	[Option("x", "exclude", "A pattern to leave out")]
	public List<string>? Exclude { get; set; }
}

/// <summary>
/// A leaf with a required positional.
/// </summary>
[ParameterCollection(Annotations = CommandAnnotations.Idempotent)]
[Command("create", "Create a directory")]
public partial class CreateDirectory
{
	[Positional(0, "Name of the new directory", true)]
	public string Name { get; set; } = "";
}

/// <summary>
/// A leaf without Annotations would get the MCP defaults, Destructive | OpenWorld.
/// </summary>
[ParameterCollection(Annotations = CommandAnnotations.Destructive | CommandAnnotations.Idempotent)]
[Command("delete", "Delete a file")]
public partial class DeleteFile
{
	[Positional(0, "Path of the file", true)]
	public string FilePath { get; set; } = "";

	[Flag("f", "force", "Also delete read-only files")]
	public bool Force { get; set; }
}
