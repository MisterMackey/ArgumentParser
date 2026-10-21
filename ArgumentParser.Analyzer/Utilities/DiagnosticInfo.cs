using System;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace ArgumentParser.Internal.Utilities;

/// <summary>
/// A diagnostic described by plain data, so it can be kept in a model that the incremental pipeline compares.
/// A <see cref="Diagnostic"/> itself cannot be, because its location points into a syntax tree.
/// </summary>
public sealed class DiagnosticInfo : IEquatable<DiagnosticInfo>
{
	/// <summary>
	/// Gets the descriptor of the rule that was broken.
	/// </summary>
	public DiagnosticDescriptor Descriptor { get; }

	/// <summary>
	/// Gets where the diagnostic is reported, or null when it has no source location.
	/// </summary>
	public LocationInfo? Location { get; }

	/// <summary>
	/// Gets the arguments that fill in the message of the descriptor.
	/// </summary>
	public EquatableArray<string> Arguments { get; }

	/// <summary>
	/// Gets the id of the rule, e.g. ARG001.
	/// </summary>
	public string Id => Descriptor.Id;

	/// <summary>
	/// Gets the severity the rule is reported with.
	/// </summary>
	public DiagnosticSeverity Severity => Descriptor.DefaultSeverity;

	private DiagnosticInfo(DiagnosticDescriptor descriptor, LocationInfo? location, EquatableArray<string> arguments)
	{
		Descriptor = descriptor;
		Location = location;
		Arguments = arguments;
	}

	/// <summary>
	/// Creates a diagnostic description, mirroring <see cref="Diagnostic.Create(DiagnosticDescriptor, Location, object[])"/>.
	/// </summary>
	/// <param name="descriptor">The descriptor of the rule that was broken.</param>
	/// <param name="location">The location in source, if any.</param>
	/// <param name="messageArgs">The arguments for the message of the descriptor.</param>
	/// <returns>The diagnostic description.</returns>
	public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location? location, params object?[] messageArgs)
	{
		if (descriptor is null)
		{
			throw new ArgumentNullException(nameof(descriptor));
		}
		var arguments = (messageArgs ?? Array.Empty<object?>())
			.Select(a => Convert.ToString(a, CultureInfo.InvariantCulture) ?? "");
		return new DiagnosticInfo(descriptor, LocationInfo.From(location), new EquatableArray<string>(arguments));
	}

	/// <summary>
	/// Creates the real diagnostic that can be reported to the compiler.
	/// </summary>
	/// <returns>The diagnostic.</returns>
	public Diagnostic ToDiagnostic()
	{
		var arguments = new object[Arguments.Count];
		for (int i = 0; i < arguments.Length; i++)
		{
			arguments[i] = Arguments[i];
		}
		return Diagnostic.Create(Descriptor, Location?.ToLocation(), arguments);
	}

	/// <inheritdoc/>
	public bool Equals(DiagnosticInfo? other)
	{
		return other is not null
			&& Descriptor.Id == other.Descriptor.Id
			&& Equals(Location, other.Location)
			&& Arguments.Equals(other.Arguments);
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj)
	{
		return Equals(obj as DiagnosticInfo);
	}

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		int hash = 17;
		hash = hash * 23 + Descriptor.Id.GetHashCode();
		hash = hash * 23 + (Location?.GetHashCode() ?? 0);
		hash = hash * 23 + Arguments.GetHashCode();
		return hash;
	}
}

/// <summary>
/// A source location described by plain data: the file and the spans in it.
/// </summary>
public sealed class LocationInfo : IEquatable<LocationInfo>
{
	/// <summary>
	/// Gets the path of the file the location is in.
	/// </summary>
	public string FilePath { get; }

	/// <summary>
	/// Gets the span of the location in the text of the file.
	/// </summary>
	public TextSpan TextSpan { get; }

	/// <summary>
	/// Gets the span of the location as lines and characters.
	/// </summary>
	public LinePositionSpan LineSpan { get; }

	private LocationInfo(string filePath, TextSpan textSpan, LinePositionSpan lineSpan)
	{
		FilePath = filePath;
		TextSpan = textSpan;
		LineSpan = lineSpan;
	}

	/// <summary>
	/// Describes a location, or returns null when it is not in source.
	/// </summary>
	/// <param name="location">The location to describe.</param>
	/// <returns>The description, or null.</returns>
	public static LocationInfo? From(Location? location)
	{
		if (location is null || location.SourceTree is null)
		{
			return null;
		}
		return new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
	}

	/// <summary>
	/// Creates the location that can be attached to a diagnostic.
	/// </summary>
	/// <returns>The location.</returns>
	public Location ToLocation()
	{
		return Location.Create(FilePath, TextSpan, LineSpan);
	}

	/// <inheritdoc/>
	public bool Equals(LocationInfo? other)
	{
		return other is not null
			&& FilePath == other.FilePath
			&& TextSpan.Equals(other.TextSpan)
			&& LineSpan.Equals(other.LineSpan);
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj)
	{
		return Equals(obj as LocationInfo);
	}

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		int hash = 17;
		hash = hash * 23 + FilePath.GetHashCode();
		hash = hash * 23 + TextSpan.GetHashCode();
		hash = hash * 23 + LineSpan.GetHashCode();
		return hash;
	}
}
