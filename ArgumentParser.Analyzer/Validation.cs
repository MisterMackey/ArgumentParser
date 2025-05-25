using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArgumentParser.Internal;

/// <summary>
/// Contains methods for validating class declarations and attributes.
/// </summary>
public static class Validation
{
	/// <summary>
	/// Validates that a class with the ParameterCollection attribute meets the requirements.
	/// </summary>
	/// <param name="classDeclaration">The class declaration to validate.</param>
	/// <returns>List of diagnostics if validation fails, empty list if validation passes.</returns>
	public static ReadOnlyCollection<Diagnostic> ValidateClassDeclaration(ClassDeclarationSyntax classDeclaration)
	{
		if (classDeclaration is null)
		{
			throw new ArgumentNullException(nameof(classDeclaration));
		}
		var diagnostics = new List<Diagnostic>();

		// Validate that the class is partial
		if (!classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
		{
			diagnostics.Add(Diagnostic.Create(
			    GeneratorDiagnostics.ARG001,
			    classDeclaration.GetLocation(),
			    classDeclaration.Identifier.Text));
		}

		// Validate that the class doesn't already have a Parse method
		if (classDeclaration.Members.OfType<MethodDeclarationSyntax>()
		    .Any(m => m.Identifier.Text == "Parse" && m.ParameterList.Parameters.Count == 1
			&& m.ParameterList.Parameters[0].Type!.ToString() == "string[]"))
		{
			diagnostics.Add(Diagnostic.Create(
			    GeneratorDiagnostics.ARG002,
			    classDeclaration.GetLocation(),
			    classDeclaration.Identifier.Text));
		}

		// Validate that the class has a constructor with no parameters
		// or no constructor at all (which implies a default constructor)
		if (
		    classDeclaration.Members.OfType<ConstructorDeclarationSyntax>().Any()
			&& !classDeclaration.Members.OfType<ConstructorDeclarationSyntax>()
		    .Any(c => c.ParameterList.Parameters.Count == 0) 
		)
		{
			diagnostics.Add(Diagnostic.Create(
			    GeneratorDiagnostics.ARG006,
			    classDeclaration.GetLocation(),
			    classDeclaration.Identifier.Text));
		}

		return diagnostics.AsReadOnly();
	}

	/// <summary>
	/// Validates that the attributes don't have conflicting values.
	/// </summary>
	/// <param name="provider">The argument provider to validate.</param>
	/// <param name="classDeclarationSyntax"></param>
	/// <returns>List of diagnostics if validation fails, empty list if validation passes.</returns>
	public static ReadOnlyCollection<Diagnostic> ValidateAttributes(
		ArgumentProvider provider,
		ClassDeclarationSyntax classDeclarationSyntax
	)
	{
		if (classDeclarationSyntax is null)
		{
			throw new ArgumentNullException(nameof(classDeclarationSyntax));
		}
		if (provider is null)
		{
			throw new ArgumentNullException(nameof(provider));
		}
		var options = provider.GetOptionArguments();
		var flags = provider.GetFlagArguments();
		var positionals = provider.GetPositionalArguments();
		var diagnostics = new List<Diagnostic>();

		// Check for duplicate short names
		var duplicateShortNames = options.
			Select(x => x.Attribute.ShortName)
			.Concat(flags.Select(x => x.Attribute.ShortName))
			.GroupBy(x => x)
			.Where(g => g.Count() > 1)
			.Select(g => g.Key)
			.ToList();
		
		var duplicateOptions = options
			.Where(x => duplicateShortNames.Contains(x.Attribute.ShortName));
		var duplicateFlags = flags
			.Where(x => duplicateShortNames.Contains(x.Attribute.ShortName));
		
		foreach (var option in duplicateOptions)
		{
			var location = GetLocation(option, classDeclarationSyntax);
			diagnostics.Add(Diagnostic.Create(
			    GeneratorDiagnostics.ARG003,
			    location,
			    "short",
			    option.Attribute.ShortName));
		}
		foreach (var flag in duplicateFlags)
		{
			var location = GetLocation(flag, classDeclarationSyntax);
			diagnostics.Add(Diagnostic.Create(
			    GeneratorDiagnostics.ARG003,
			    location,
			    "short",
			    flag.Attribute.ShortName));
		}

		// Check for duplicate long names
		var duplicateLongNames = options.
			Select(x => x.Attribute.LongName)
			.Concat(flags.Select(x => x.Attribute.LongName))
			.GroupBy(x => x)
			.Where(g => g.Count() > 1)
			.Select(g => g.Key)
			.ToList();
		var duplicateLongOptions = options
			.Where(x => duplicateLongNames.Contains(x.Attribute.LongName));
		var duplicateLongFlags = flags
			.Where(x => duplicateLongNames.Contains(x.Attribute.LongName));
		foreach (var option in duplicateLongOptions)
		{
			var location = GetLocation(option, classDeclarationSyntax);
			diagnostics.Add(Diagnostic.Create(
			    GeneratorDiagnostics.ARG003,
			    location,
			    "long",
			    option.Attribute.LongName));
		}
		foreach (var flag in duplicateLongFlags)
		{
			var location = GetLocation(flag, classDeclarationSyntax);
			diagnostics.Add(Diagnostic.Create(
			    GeneratorDiagnostics.ARG003,
			    location,
			    "long",
			    flag.Attribute.LongName));
		}

		// Check for duplicate positional positions
		var duplicatePositions = positionals.GroupBy(x => x.Attribute.Position)
		    .Where(g => g.Count() > 1)
		    .SelectMany(g => g.ToList())
		    .ToList();
		foreach (var position in duplicatePositions)
		{
			var location = GetLocation(position, classDeclarationSyntax);
			diagnostics.Add(Diagnostic.Create(
			    GeneratorDiagnostics.ARG004,
			    location,
			    position.Attribute.Position));
		}

		// Check that positional attributes form a sequence starting from 0
		var invalidPositions = positionals.Where(p => p.Attribute.Position < 0 || p.Attribute.Position >= positionals.Count).ToList();
		foreach (var position in invalidPositions)
		{
			var location = GetLocation(position, classDeclarationSyntax);
			diagnostics.Add(Diagnostic.Create(
			    GeneratorDiagnostics.ARG005,
			    location,
			    position.Attribute.Position));
		}

		// Check for unsupported property types
		var allProperties = options.Concat(positionals).Concat(flags).ToList();
		foreach (var prop in allProperties)
		{
			if (!IsSupportedPropertyType(prop.PropertyType))
			{
				var location = GetLocation(prop, classDeclarationSyntax);
				diagnostics.Add(Diagnostic.Create(
					GeneratorDiagnostics.ARG010,
					location,
					prop.PropertyType));
			}
		}

		return diagnostics.AsReadOnly();
	}

	/// <summary>
	/// Determines if a property type is supported by the argument parser generator.
	/// </summary>
	/// <param name="propertyType">The property type to check.</param>
	/// <returns>True if the property type is supported; otherwise, false.</returns>
	private static bool IsSupportedPropertyType(string propertyType)
	{
		// Match the types supported in SourceTextGenerator.WriteValueParseCode
		return propertyType switch
		{
			"int" => true,
			"double" => true,
			"float" => true,
			"long" => true,
			"short" => true,
			"decimal" => true,
			"byte" => true,
			"sbyte" => true,
			"char" => true,
			"uint" => true,
			"ulong" => true,
			"ushort" => true,
			"Guid" => true,
			"Uri" => true,
			"TimeSpan" => true,
			"bool" => true,
			"DateTime" => true,
			"string" => true,
			"string?" => true,
			_ => false
		};
	}

	private static Location? GetLocation(PropertyAndAttributeInfo info, ClassDeclarationSyntax classDeclarationSyntax)
	{
		var property = classDeclarationSyntax.DescendantNodes()
		    .OfType<PropertyDeclarationSyntax>()
		    .FirstOrDefault(p => p.Identifier.Text == info.PropertyName);
		if (property != null)
		{
			return property.GetLocation();
		}
		return null;
	}

	/// <summary>
	/// Reports a list of diagnostics to the source production context.
	/// </summary>
	/// <param name="context">The source production context to report to.</param>
	/// <param name="diagnostics">The diagnostics to report.</param>
	public static void ReportDiagnostics(SourceProductionContext context, ReadOnlyCollection<Diagnostic> diagnostics)
	{
		if (diagnostics is null)
		{
			return;
		}
		foreach (var diagnostic in diagnostics)
		{
			context.ReportDiagnostic(diagnostic);
		}
	}
}
