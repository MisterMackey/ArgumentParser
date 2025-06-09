using System;
using System.CodeDom.Compiler;
using ArgumentParser.Internal;

namespace ArgumentParser.Tests;

/// <summary>
/// This class will help in the tests to more easily build snippets of source code
/// to feed into the generator used in the tests.
/// The result will be (somewhat) formatted in case it needs to be inspected.
/// </summary>
public class SourceCodeBuilder : IDisposable
{
	private readonly IndentedTextWriter writer;
	private readonly StringWriter stringWriter;
	private List<string> imports = [];
	private string parameterCollectionAttribute = "[ParameterCollection]";
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
	public SourceCodeBuilder AddImports(string[]? imports)
	{
		if (imports == null)
		{
			//add default imports
			this.imports.Add("using System;");
			this.imports.Add("using ArgumentParser;");
			return this;
		}
		foreach (var import in imports)
		{
			this.imports.Add(import);
		}
		return this;
	}

	public SourceCodeBuilder AddClassAttribute(string? attribute)
	{
		if (string.IsNullOrEmpty(attribute))
		{
			throw new ArgumentException("Attribute cannot be null or empty.", nameof(attribute));
		}
		parameterCollectionAttribute = attribute.Trim();
		return this;
	}

	/// <summary>
	/// Adds the class declaration to the class. If modifiers is null or empty,
	/// it will default to "public partial".
	/// /// </summary>
	public SourceCodeBuilder AddClassDeclaration(string className, string? modifiers)
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
		return this;
	}

	public SourceCodeBuilder AddOptionParameter(PropertyAndAttributeInfo info, string? modifiers, string? parseMethodName = null)
	{
		if (string.IsNullOrWhiteSpace(modifiers))
			modifiers = "public";
		if (!string.IsNullOrEmpty(parseMethodName))
		{
			classMembers.Add($"[ParsedWithMethod(\"{parseMethodName}\")]");
		}
		classMembers.Add($"[Option(\"{info.Attribute.ShortName}\", \"{info.Attribute.LongName}\", \"{info.Attribute.Description}\", {info.Attribute.Required.ToString().ToLower()})]");
		classMembers.Add($"{modifiers} {info.PropertyType} {info.PropertyName} {{ get; set; }}");
		classMembers.Add(Environment.NewLine);
		return this;
	}

	public SourceCodeBuilder AddFlagParameter(PropertyAndAttributeInfo info, string? modifiers, string? parseMethodName = null)
	{
		if (string.IsNullOrWhiteSpace(modifiers))
			modifiers = "public";
		if (!string.IsNullOrEmpty(parseMethodName))
		{
			classMembers.Add($"[ParsedWithMethod(\"{parseMethodName}\")]");
		}
		classMembers.Add($"[Flag(\"{info.Attribute.ShortName}\", \"{info.Attribute.LongName}\", \"{info.Attribute.Description}\")]");
		classMembers.Add($"{modifiers} {info.PropertyType} {info.PropertyName} {{ get; set; }}");
		classMembers.Add(Environment.NewLine);
		return this;
	}
	public SourceCodeBuilder AddPositionalParameter(PropertyAndAttributeInfo info, string? modifiers, string? parseMethodName = null)
	{
		if (string.IsNullOrWhiteSpace(modifiers))
			modifiers = "public";
		if (!string.IsNullOrEmpty(parseMethodName))
		{
			classMembers.Add($"[ParsedWithMethod(\"{parseMethodName}\")]");
		}
		classMembers.Add($"[Positional({info.Attribute.Position}, \"{info.Attribute.Description}\", {info.Attribute.Required.ToString().ToLower()})]");
		classMembers.Add($"{modifiers} {info.PropertyType} {info.PropertyName} {{ get; set; }}");
		classMembers.Add(Environment.NewLine);
		return this;
	}

	/// <summary>
	/// use this to add whatever code you want to the class.
	/// </summary>
	/// <param name="memberCode"></param>
	public SourceCodeBuilder AddClassMember(string memberCode)
	{
		if (string.IsNullOrEmpty(memberCode))
		{
			throw new ArgumentException("Member code cannot be null or empty.", nameof(memberCode));
		}
		classMembers.Add(memberCode);
		classMembers.Add(Environment.NewLine);
		return this;
	}

	public override string ToString()
	{
		foreach (var import in imports)
		{
			writer.WriteLine(import);
		}
		writer.WriteLine();
		writer.WriteLine("namespace ArgumentParser.Tests;");
		writer.WriteLine();
		writer.WriteLine(parameterCollectionAttribute);
		writer.WriteLine(classDeclaration);
		writer.WriteLine("{");
		writer.Indent++;
		foreach (var member in classMembers)
		{
			writer.WriteLine(member);
		}
		writer.Indent--;
		writer.WriteLine("}");
		writer.Flush();
		return stringWriter.ToString();
	}

	public void Dispose()
	{
		((IDisposable)writer).Dispose();
	}
}
