#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{
	/// <summary>
	/// Lets the compiler emit init accessors (ArgumentParserException.Key) for netstandard2.0, which does not have this type;
	/// the other target frameworks have it in the base library.
	/// </summary>
	internal static class IsExternalInit
	{
	}
}
#endif
