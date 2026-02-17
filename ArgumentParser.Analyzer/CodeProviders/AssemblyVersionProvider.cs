namespace ArgumentParser.Internal;

/// <summary>
/// This static class provides a SemVer version property to be used by the code generator.
/// The value will be updated by the build pipeline (in the make release target).
/// </summary>
public static class AssemblyVersionProvider
{
	/// <summary>
	/// The fullsemver version of the assembly.
	/// Updated automatically by the build pipeline.
	/// </summary>
	public const string FullSemVer = "1.4.0";
}
