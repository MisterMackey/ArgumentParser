using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ArgumentParser.Internal.Utilities;

/// <summary>
/// An array that compares by content instead of by reference, so models that hold one can be compared by the incremental generator pipeline.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>> where T : IEquatable<T>
{
	private readonly T[]? _items;

	/// <summary>
	/// Initializes a new instance of the <see cref="EquatableArray{T}"/> struct holding a copy of the given items.
	/// </summary>
	/// <param name="items">The items to copy.</param>
	public EquatableArray(IEnumerable<T> items)
	{
		if (items is null)
		{
			throw new ArgumentNullException(nameof(items));
		}
		_items = items.ToArray();
	}

	/// <summary>
	/// Gets the number of items.
	/// </summary>
	public int Count => _items?.Length ?? 0;

	/// <summary>
	/// Gets the item at the given index.
	/// </summary>
	/// <param name="index">The zero based index.</param>
	public T this[int index] => (_items ?? Array.Empty<T>())[index];

	/// <summary>
	/// Returns the items as a read-only collection.
	/// </summary>
	/// <returns>A read-only collection with the same items.</returns>
	public ReadOnlyCollection<T> AsReadOnly()
	{
		return Array.AsReadOnly(_items ?? Array.Empty<T>());
	}

	/// <inheritdoc/>
	public bool Equals(EquatableArray<T> other)
	{
		var left = _items ?? Array.Empty<T>();
		var right = other._items ?? Array.Empty<T>();
		if (left.Length != right.Length)
		{
			return false;
		}
		for (int i = 0; i < left.Length; i++)
		{
			if (!EqualityComparer<T>.Default.Equals(left[i], right[i]))
			{
				return false;
			}
		}
		return true;
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj)
	{
		return obj is EquatableArray<T> other && Equals(other);
	}

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		int hash = 17;
		foreach (var item in _items ?? Array.Empty<T>())
		{
			hash = hash * 23 + (item?.GetHashCode() ?? 0);
		}
		return hash;
	}

	/// <summary>
	/// Determines whether two arrays hold equal items in the same order.
	/// </summary>
	public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right)
	{
		return left.Equals(right);
	}

	/// <summary>
	/// Determines whether two arrays differ.
	/// </summary>
	public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right)
	{
		return !left.Equals(right);
	}
}
