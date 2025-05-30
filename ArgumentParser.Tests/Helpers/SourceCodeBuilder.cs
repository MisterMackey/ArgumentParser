using System;
using System.CodeDom.Compiler;
using ArgumentParser.Internal;

namespace ArgumentParser.Tests;

/// <summary>
/// This class will help in the tests to more easily build snippets of source code
/// to feed into the generator used in the tests.
/// The result will be (somewhat) formatted in case it needs to be inspected.
/// </summary>
public class SourceCodeBuilder
{
	private readonly IndentedTextWriter writer;
	private readonly StringWriter stringWriter;
	private List<string> imports = [];
	private string classDeclaration = "public partial class TestClass";
	private List<string> classMembers = [];

	/// <summary>
	/// Create a new instance of the <see cref="SourceCodeBuilder"/> class.
	/// </summary>
	public SourceCodeBuilder()
	{
		stringWriter = new StringWriter();
		writer = new IndentedTextWriter(stringWriter, "\t");
	}

	/// <summary>
	/// Adds imports to the class. If imports is null, it will add default imports.
	/// /// </summary>
	public void AddImports(string[]? imports)
	{
		if (imports == null)
		{
			//add default imports
			this.imports.Add("using System;");
			this.imports.Add("using ArgumentParser;");
			return;
		}
		foreach (var import in imports)
		{
			this.imports.Add(import);
		}
	}

	/// <summary>
	/// Adds the class declaration to the class. If modifiers is null or empty,
	/// it will default to "public partial".
	/// /// </summary>
	public void AddClassDeclaration(string className, string? modifiers)
	{
		if (string.IsNullOrEmpty(className))
		{
			throw new ArgumentException("Class name cannot be null or empty.", nameof(className));
		}
		if (modifiers == null)
		{
			modifiers = "public partial";
		}
		else
		{
			modifiers = modifiers.Trim();
			if (string.IsNullOrEmpty(modifiers))
			{
				modifiers = "public partial";
			}
		}
		classDeclaration = $"{modifiers} {className}";
	}

	public void AddOptionParameter(PropertyAndAttributeInfo info, string? modifiers)
	{

	}

	public void AddFlagParameter(PropertyAndAttributeInfo info, string? modifiers)
	{

	}
	public void AddPositionalParameter(PropertyAndAttributeInfo info, string? modifiers)
	{

	}

	/// <summary>
	/// use this to add whatever code you want to the class.
	/// </summary>
	/// <param name="memberCode"></param>
	public void AddClassMember(string memberCode)
	{
		if (string.IsNullOrEmpty(memberCode))
		{
			throw new ArgumentException("Member code cannot be null or empty.", nameof(memberCode));
		}
		classMembers.Add(memberCode);
		classMembers.Add(Environment.NewLine);
	}
}
