using System;

namespace ArgumentParser.Internal;

/// <summary>
/// ISourceTextGenerator implementations provide a GenerateSourceText method that generates the source text for the parse method.
/// </summary>
public interface ISourceTextGenerator
{
	/// <summary>
	/// Generates the source text for the parse method
	/// </summary>
	/// <returns></returns>
	string GenerateSourceText();
}
