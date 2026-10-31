using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ArgumentParser.Internal.Utilities;

/// <summary>
/// The kind of argument a field of a tool comes from.
/// </summary>
public enum ArgumentKind
{
	/// <summary>
	/// A property with the Option attribute.
	/// </summary>
	Option,

	/// <summary>
	/// A property with the Flag attribute.
	/// </summary>
	Flag,

	/// <summary>
	/// A property with the Positional attribute.
	/// </summary>
	Positional,
}

/// <summary>
/// One argument of a tool as JSON sees it: the key it has in the JSON object of a tool call, the type of its value, and the property it fills.
/// The JSON Schema and the JSON binder are both written from these fields, never from the attributes, so they cannot disagree.
/// A field is built in the output step from models that are compared already, so it is not compared itself.
/// </summary>
public sealed class ToolField
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ToolField"/> class.
	/// </summary>
	/// <param name="key">The key in the JSON object: the name of the property in camelCase.</param>
	/// <param name="target">The property names that lead from the class of the tool (the root of a tree) to the property, the property last.</param>
	/// <param name="kind">The kind of argument the field comes from.</param>
	/// <param name="jsonType">The JSON type of a value: string, integer, number or boolean.</param>
	/// <param name="valueType">The type that a value is converted to, as Roslyn displays it, without <c>global::</c> (the element type for a list).</param>
	/// <param name="enumMembers">The names of the members of an enum, empty for any other type and for an enum with a ParsedWithMethod attribute.</param>
	/// <param name="isEnum">Whether the value is given by the names of <paramref name="enumMembers"/> (an enum without a ParsedWithMethod attribute), even when it has none.</param>
	/// <param name="isArray">Whether the value is an array of values (a list argument).</param>
	/// <param name="required">Whether the key must be in the JSON object.</param>
	/// <param name="nullable">Whether the value can be JSON null.</param>
	/// <param name="description">The description of the argument.</param>
	/// <param name="parseMethodOwner">The type that holds the method of the ParsedWithMethod attribute, empty without one.</param>
	/// <param name="parseMethodName">The name of the method of the ParsedWithMethod attribute, empty without one.</param>
	public ToolField(
		string key,
		IEnumerable<string> target,
		ArgumentKind kind,
		string jsonType,
		string valueType,
		IEnumerable<string> enumMembers,
		bool isEnum,
		bool isArray,
		bool required,
		bool nullable,
		string description,
		string parseMethodOwner,
		string parseMethodName)
	{
		Key = key ?? throw new ArgumentNullException(nameof(key));
		Target = (target ?? throw new ArgumentNullException(nameof(target))).ToList().AsReadOnly();
		Kind = kind;
		JsonType = jsonType ?? throw new ArgumentNullException(nameof(jsonType));
		ValueType = valueType ?? "";
		EnumMembers = (enumMembers ?? []).ToList().AsReadOnly();
		IsEnum = isEnum;
		IsArray = isArray;
		Required = required;
		Nullable = nullable;
		Description = description ?? "";
		ParseMethodOwner = parseMethodOwner ?? "";
		ParseMethodName = parseMethodName ?? "";
	}

	/// <summary>
	/// Gets the key in the JSON object: the name of the property in camelCase.
	/// </summary>
	public string Key { get; }

	/// <summary>
	/// Gets the property names that lead from the class of the tool (the root of a tree) to the property, the property last.
	/// For a class on its own it is only the property; in a tree it starts with the child command properties from the root.
	/// The names are the exact names of the properties, as code refers to them.
	/// </summary>
	public ReadOnlyCollection<string> Target { get; }

	/// <summary>
	/// Gets the name of the property the field fills.
	/// </summary>
	public string PropertyName => Target[Target.Count - 1];

	/// <summary>
	/// Gets the kind of argument the field comes from.
	/// </summary>
	public ArgumentKind Kind { get; }

	/// <summary>
	/// Gets the JSON type of a value: <c>string</c>, <c>integer</c>, <c>number</c> or <c>boolean</c>.
	/// An enum is a string (the name of a member); an enum flag also takes an integer, see <see cref="AcceptsEnumNumber"/>.
	/// A type with a ParsedWithMethod attribute is a string, which is handed to the method as on the command line.
	/// </summary>
	public string JsonType { get; }

	/// <summary>
	/// Gets the type that a value is converted to, as Roslyn displays it (<c>int</c>, <c>System.DateTime</c>, <c>MyApp.Level</c>), without the question mark
	/// of a nullable type; for a list it is the type of the elements. It is not qualified with <c>global::</c>: code that writes it into generated code
	/// must add that for any type that is not a keyword, or a user namespace (<c>MyApp.System</c>) could capture the name.
	/// </summary>
	public string ValueType { get; }

	/// <summary>
	/// Gets the names of the members of an enum, in the order they are declared; empty for any other type.
	/// It is also empty for an enum option or positional with a ParsedWithMethod attribute: the method decides which text it takes.
	/// </summary>
	public ReadOnlyCollection<string> EnumMembers { get; }

	/// <summary>
	/// Gets a value indicating whether the value can also be the number of an enum member: a flag that holds an enum counts how often it is given,
	/// so it takes the level as a number as well as the name.
	/// </summary>
	public bool AcceptsEnumNumber => Kind == ArgumentKind.Flag && IsEnum;

	/// <summary>
	/// Gets a value indicating whether the value is the name of one of the <see cref="EnumMembers"/>: an enum without a ParsedWithMethod attribute.
	/// An enum without members is one too, and then takes no value at all.
	/// </summary>
	public bool IsEnum { get; }

	/// <summary>
	/// Gets a value indicating whether the value is an array of values, for a list argument.
	/// </summary>
	public bool IsArray { get; }

	/// <summary>
	/// Gets a value indicating whether the key must be in the JSON object: the argument has <c>required: true</c>, as on the command line.
	/// </summary>
	public bool Required { get; }

	/// <summary>
	/// Gets a value indicating whether the value can be JSON null, because the property is nullable.
	/// A field can be both <see cref="Required"/> and nullable: the key must be given, but its value can be null.
	/// </summary>
	public bool Nullable { get; }

	/// <summary>
	/// Gets the description of the argument.
	/// </summary>
	public string Description { get; }

	/// <summary>
	/// Gets the type that holds the method of the ParsedWithMethod attribute, written to be used in generated code; empty without one.
	/// </summary>
	public string ParseMethodOwner { get; }

	/// <summary>
	/// Gets the name of the method of the ParsedWithMethod attribute; empty without one.
	/// </summary>
	public string ParseMethodName { get; }
}

/// <summary>
/// A command described as a tool, such as one of the Model Context Protocol (MCP): its name, description, annotations and the fields of its JSON object.
/// </summary>
public sealed class ToolSpec
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ToolSpec"/> class.
	/// </summary>
	/// <param name="name">The name of the tool.</param>
	/// <param name="description">The description of the tool.</param>
	/// <param name="annotations">The numeric value of the CommandAnnotations of the command.</param>
	/// <param name="fields">The fields of the JSON object of a call.</param>
	public ToolSpec(string name, string description, int annotations, IEnumerable<ToolField> fields)
	{
		Name = name ?? throw new ArgumentNullException(nameof(name));
		Description = description ?? "";
		Annotations = annotations;
		Fields = (fields ?? throw new ArgumentNullException(nameof(fields))).ToList().AsReadOnly();
	}

	/// <summary>
	/// Gets the name of the tool: the path of a command joined with <c>__</c> (<c>ingredient__add</c>), or the name of a class on its own.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Gets the description of the tool.
	/// </summary>
	public string Description { get; }

	/// <summary>
	/// Gets the numeric value of the CommandAnnotations of the command.
	/// </summary>
	public int Annotations { get; }

	/// <summary>
	/// Gets the fields of the JSON object of a call: in a tree those of the root and the groups first, then those of the leaf;
	/// in every class its positional arguments by position, then its options, then its flags.
	/// </summary>
	public ReadOnlyCollection<ToolField> Fields { get; }
}
