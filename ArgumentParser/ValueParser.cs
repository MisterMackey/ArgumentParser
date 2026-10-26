using System;
using System.Globalization;

namespace ArgumentParser
{

	/// <summary>
	/// Converts the text of an argument to the numbers, dates and times the generated parsers bind.
	/// By default the text is read with the invariant culture and only in forms that mean the same everywhere:
	/// numbers have no thousands separators and use <c>.</c> as the decimal point, so <c>1,5</c> is rejected instead of being read as 15
	/// (or as 1.5 on a machine with a Dutch or German culture), and a date is written as ISO 8601 (<c>2026-10-26</c>,
	/// <c>2026-10-26T14:30:00Z</c>), so <c>01/02/2026</c> is rejected instead of being read as the second of January or the first of February.
	/// With <c>useCurrentCulture</c> the text is read the way the <c>TryParse(string, out T)</c> method of the type reads it,
	/// with the culture of the current thread and its default number styles and date formats.
	/// </summary>
	public static class ValueParser
	{
		// ISO 8601 in its extended format: a date, or a date and a time with minutes, seconds and up to seven digits of a fraction,
		// optionally followed by Z or an offset such as +02:00
		private static readonly string[] IsoDateTimeFormats =
		[
			"yyyy'-'MM'-'dd",
			"yyyy'-'MM'-'dd'T'HH':'mmK",
			"yyyy'-'MM'-'dd'T'HH':'mm':'ssK",
			"yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fK",
			"yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'ffK",
			"yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffK",
			"yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'ffffK",
			"yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffK",
			"yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'ffffffK",
			"yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffffK",
		];

		private const NumberStyles StrictInteger = NumberStyles.Integer;
		private const NumberStyles StrictFloat = NumberStyles.Float;

		private static CultureInfo Culture(bool useCurrentCulture)
		{
			return useCurrentCulture ? CultureInfo.CurrentCulture : CultureInfo.InvariantCulture;
		}

		/// <summary>
		/// Converts the text to an <see cref="int"/>: an optional sign followed by digits.
		/// </summary>
		/// <param name="value">The text of the argument.</param>
		/// <param name="useCurrentCulture">True to read the text with the culture of the current thread instead of the invariant culture.</param>
		/// <param name="result">The value, or the default of the type when the text could not be converted.</param>
		/// <returns>True if the text could be converted; otherwise, false.</returns>
		public static bool TryParse(string? value, bool useCurrentCulture, out int result)
		{
			return int.TryParse(value, StrictInteger, Culture(useCurrentCulture), out result);
		}

		/// <summary>
		/// Converts the text to a <see cref="long"/>: an optional sign followed by digits.
		/// </summary>
		/// <param name="value">The text of the argument.</param>
		/// <param name="useCurrentCulture">True to read the text with the culture of the current thread instead of the invariant culture.</param>
		/// <param name="result">The value, or the default of the type when the text could not be converted.</param>
		/// <returns>True if the text could be converted; otherwise, false.</returns>
		public static bool TryParse(string? value, bool useCurrentCulture, out long result)
		{
			return long.TryParse(value, StrictInteger, Culture(useCurrentCulture), out result);
		}

		/// <summary>
		/// Converts the text to a <see cref="short"/>: an optional sign followed by digits.
		/// </summary>
		/// <param name="value">The text of the argument.</param>
		/// <param name="useCurrentCulture">True to read the text with the culture of the current thread instead of the invariant culture.</param>
		/// <param name="result">The value, or the default of the type when the text could not be converted.</param>
		/// <returns>True if the text could be converted; otherwise, false.</returns>
		public static bool TryParse(string? value, bool useCurrentCulture, out short result)
		{
			return short.TryParse(value, StrictInteger, Culture(useCurrentCulture), out result);
		}

		/// <summary>
		/// Converts the text to a <see cref="byte"/>: an optional sign followed by digits.
		/// </summary>
		/// <param name="value">The text of the argument.</param>
		/// <param name="useCurrentCulture">True to read the text with the culture of the current thread instead of the invariant culture.</param>
		/// <param name="result">The value, or the default of the type when the text could not be converted.</param>
		/// <returns>True if the text could be converted; otherwise, false.</returns>
		public static bool TryParse(string? value, bool useCurrentCulture, out byte result)
		{
			return byte.TryParse(value, StrictInteger, Culture(useCurrentCulture), out result);
		}

		/// <summary>
		/// Converts the text to an <see cref="sbyte"/>: an optional sign followed by digits.
		/// </summary>
		/// <param name="value">The text of the argument.</param>
		/// <param name="useCurrentCulture">True to read the text with the culture of the current thread instead of the invariant culture.</param>
		/// <param name="result">The value, or the default of the type when the text could not be converted.</param>
		/// <returns>True if the text could be converted; otherwise, false.</returns>
		public static bool TryParse(string? value, bool useCurrentCulture, out sbyte result)
		{
			return sbyte.TryParse(value, StrictInteger, Culture(useCurrentCulture), out result);
		}

		/// <summary>
		/// Converts the text to a <see cref="uint"/>: an optional sign followed by digits.
		/// </summary>
		/// <param name="value">The text of the argument.</param>
		/// <param name="useCurrentCulture">True to read the text with the culture of the current thread instead of the invariant culture.</param>
		/// <param name="result">The value, or the default of the type when the text could not be converted.</param>
		/// <returns>True if the text could be converted; otherwise, false.</returns>
		public static bool TryParse(string? value, bool useCurrentCulture, out uint result)
		{
			return uint.TryParse(value, StrictInteger, Culture(useCurrentCulture), out result);
		}

		/// <summary>
		/// Converts the text to a <see cref="ulong"/>: an optional sign followed by digits.
		/// </summary>
		/// <param name="value">The text of the argument.</param>
		/// <param name="useCurrentCulture">True to read the text with the culture of the current thread instead of the invariant culture.</param>
		/// <param name="result">The value, or the default of the type when the text could not be converted.</param>
		/// <returns>True if the text could be converted; otherwise, false.</returns>
		public static bool TryParse(string? value, bool useCurrentCulture, out ulong result)
		{
			return ulong.TryParse(value, StrictInteger, Culture(useCurrentCulture), out result);
		}

		/// <summary>
		/// Converts the text to a <see cref="ushort"/>: an optional sign followed by digits.
		/// </summary>
		/// <param name="value">The text of the argument.</param>
		/// <param name="useCurrentCulture">True to read the text with the culture of the current thread instead of the invariant culture.</param>
		/// <param name="result">The value, or the default of the type when the text could not be converted.</param>
		/// <returns>True if the text could be converted; otherwise, false.</returns>
		public static bool TryParse(string? value, bool useCurrentCulture, out ushort result)
		{
			return ushort.TryParse(value, StrictInteger, Culture(useCurrentCulture), out result);
		}

		/// <summary>
		/// Converts the text to a <see cref="double"/>: an optional sign, digits with <c>.</c> as the decimal point and an optional exponent,
		/// or <c>NaN</c>, <c>Infinity</c> or <c>-Infinity</c>. Thousands separators are rejected unless <paramref name="useCurrentCulture"/> is true.
		/// </summary>
		/// <param name="value">The text of the argument.</param>
		/// <param name="useCurrentCulture">True to read the text the way <see cref="double.TryParse(string, out double)"/> does, with the culture of the current thread.</param>
		/// <param name="result">The value, or the default of the type when the text could not be converted.</param>
		/// <returns>True if the text could be converted; otherwise, false.</returns>
		public static bool TryParse(string? value, bool useCurrentCulture, out double result)
		{
			var styles = useCurrentCulture ? NumberStyles.Float | NumberStyles.AllowThousands : StrictFloat;
			return double.TryParse(value, styles, Culture(useCurrentCulture), out result);
		}

		/// <summary>
		/// Converts the text to a <see cref="float"/>: an optional sign, digits with <c>.</c> as the decimal point and an optional exponent,
		/// or <c>NaN</c>, <c>Infinity</c> or <c>-Infinity</c>. Thousands separators are rejected unless <paramref name="useCurrentCulture"/> is true.
		/// </summary>
		/// <param name="value">The text of the argument.</param>
		/// <param name="useCurrentCulture">True to read the text the way <see cref="float.TryParse(string, out float)"/> does, with the culture of the current thread.</param>
		/// <param name="result">The value, or the default of the type when the text could not be converted.</param>
		/// <returns>True if the text could be converted; otherwise, false.</returns>
		public static bool TryParse(string? value, bool useCurrentCulture, out float result)
		{
			var styles = useCurrentCulture ? NumberStyles.Float | NumberStyles.AllowThousands : StrictFloat;
			return float.TryParse(value, styles, Culture(useCurrentCulture), out result);
		}

		/// <summary>
		/// Converts the text to a <see cref="decimal"/>: an optional sign, digits with <c>.</c> as the decimal point and an optional exponent.
		/// Thousands separators are rejected unless <paramref name="useCurrentCulture"/> is true.
		/// </summary>
		/// <param name="value">The text of the argument.</param>
		/// <param name="useCurrentCulture">True to read the text the way <see cref="decimal.TryParse(string, out decimal)"/> does, with the culture of the current thread.</param>
		/// <param name="result">The value, or the default of the type when the text could not be converted.</param>
		/// <returns>True if the text could be converted; otherwise, false.</returns>
		public static bool TryParse(string? value, bool useCurrentCulture, out decimal result)
		{
			var styles = useCurrentCulture ? NumberStyles.Number : StrictFloat;
			return decimal.TryParse(value, styles, Culture(useCurrentCulture), out result);
		}

		/// <summary>
		/// Converts the text to a <see cref="DateTime"/> written as ISO 8601: a date (<c>2026-10-26</c>), or a date and a time
		/// (<c>2026-10-26T14:30</c>, <c>2026-10-26T14:30:15.250</c>), optionally followed by <c>Z</c> or an offset such as <c>+02:00</c>.
		/// A value with <c>Z</c> has <see cref="DateTimeKind.Utc"/>, one with an offset is converted to local time, and one without either is
		/// <see cref="DateTimeKind.Unspecified"/>.
		/// </summary>
		/// <param name="value">The text of the argument.</param>
		/// <param name="useCurrentCulture">True to read the text the way <see cref="DateTime.TryParse(string, out DateTime)"/> does, in any format the culture of the current thread knows.</param>
		/// <param name="result">The value, or the default of the type when the text could not be converted.</param>
		/// <returns>True if the text could be converted; otherwise, false.</returns>
		public static bool TryParse(string? value, bool useCurrentCulture, out DateTime result)
		{
			if (useCurrentCulture)
			{
				return DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out result);
			}
			return DateTime.TryParseExact(value, IsoDateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result);
		}

		/// <summary>
		/// Converts the text to a <see cref="TimeSpan"/> in the form <c>[-][d.]hh:mm[:ss[.fffffff]]</c>, or a number of days.
		/// </summary>
		/// <param name="value">The text of the argument.</param>
		/// <param name="useCurrentCulture">True to read the text with the culture of the current thread instead of the invariant culture.</param>
		/// <param name="result">The value, or the default of the type when the text could not be converted.</param>
		/// <returns>True if the text could be converted; otherwise, false.</returns>
		public static bool TryParse(string? value, bool useCurrentCulture, out TimeSpan result)
		{
			return TimeSpan.TryParse(value, Culture(useCurrentCulture), out result);
		}
	}
}
