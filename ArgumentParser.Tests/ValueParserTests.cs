using System.Globalization;

namespace ArgumentParser.Tests;

/// <summary>
/// Tests for <see cref="ValueParser"/>, which the generated parsers call for numbers, dates and times.
/// The current culture is Dutch, whose decimal separator is a comma and whose group separator is a dot,
/// so a test only passes when the strict mode really ignores it.
/// </summary>
public class ValueParserTests
{
	private static readonly CultureInfo Dutch = new("nl-NL");

	public ValueParserTests()
	{
		CultureInfo.CurrentCulture = Dutch;
	}

	[Theory]
	[InlineData("1.5", 1.5)]
	[InlineData("-2.5e3", -2500)]
	[InlineData("+0.25", 0.25)]
	[InlineData(" 7 ", 7)]
	[InlineData(".5", 0.5)]
	public void Double_Strict_ReadsTheInvariantForm(string text, double expected)
	{
		Assert.True(ValueParser.TryParse(text, false, out double value));
		Assert.Equal(expected, value);
		Assert.True(ValueParser.TryParse(text, false, out float single));
		Assert.Equal((float)expected, single);
	}

	[Theory]
	[InlineData("1,5")]
	[InlineData("1,000")]
	[InlineData("1,000.5")]
	[InlineData("1.000,5")]
	[InlineData("1 000")]
	[InlineData("")]
	[InlineData(null)]
	public void Numbers_Strict_RejectThousandsSeparatorsAndCommas(string? text)
	{
		Assert.False(ValueParser.TryParse(text, false, out double _));
		Assert.False(ValueParser.TryParse(text, false, out float _));
		Assert.False(ValueParser.TryParse(text, false, out decimal _));
		Assert.False(ValueParser.TryParse(text, false, out int _));
		Assert.False(ValueParser.TryParse(text, false, out long _));
	}

	[Fact]
	public void Double_Strict_ReadsNaNAndInfinityByTheirInvariantNames()
	{
		Assert.True(ValueParser.TryParse("NaN", false, out double nan));
		Assert.True(double.IsNaN(nan));
		Assert.True(ValueParser.TryParse("-Infinity", false, out double infinity));
		Assert.Equal(double.NegativeInfinity, infinity);
	}

	[Fact]
	public void Decimal_Strict_ReadsAPointAndAnExponent()
	{
		Assert.True(ValueParser.TryParse("3.25", false, out decimal value));
		Assert.Equal(3.25m, value);
		Assert.True(ValueParser.TryParse("1e3", false, out decimal thousand));
		Assert.Equal(1000m, thousand);
		Assert.False(ValueParser.TryParse("5-", false, out decimal _));
	}

	[Fact]
	public void Integers_Strict_ReadASignAndDigitsOnly()
	{
		Assert.True(ValueParser.TryParse("-42", false, out int negative));
		Assert.Equal(-42, negative);
		Assert.True(ValueParser.TryParse("+42", false, out int positive));
		Assert.Equal(42, positive);
		Assert.False(ValueParser.TryParse("1.5", false, out int _));
		Assert.False(ValueParser.TryParse("1e3", false, out int _));
		Assert.False(ValueParser.TryParse("0x10", false, out int _));
		Assert.True(ValueParser.TryParse("9000000000", false, out long big));
		Assert.Equal(9000000000L, big);
		Assert.True(ValueParser.TryParse("-12", false, out short small));
		Assert.Equal((short)-12, small);
		Assert.True(ValueParser.TryParse("200", false, out byte unsignedByte));
		Assert.Equal((byte)200, unsignedByte);
		Assert.True(ValueParser.TryParse("-5", false, out sbyte signedByte));
		Assert.Equal((sbyte)-5, signedByte);
		Assert.True(ValueParser.TryParse("4000000000", false, out uint unsignedInt));
		Assert.Equal(4000000000u, unsignedInt);
		Assert.True(ValueParser.TryParse("18000000000000000000", false, out ulong unsignedLong));
		Assert.Equal(18000000000000000000ul, unsignedLong);
		Assert.True(ValueParser.TryParse("65000", false, out ushort unsignedShort));
		Assert.Equal((ushort)65000, unsignedShort);
		Assert.False(ValueParser.TryParse("-1", false, out uint _));
	}

	[Fact]
	public void Numbers_CurrentCulture_ReadTheCultureLikeTheBclDoes()
	{
		Assert.True(ValueParser.TryParse("1,5", true, out double value));
		Assert.Equal(1.5, value);
		// the dot is the group separator of the Dutch culture
		Assert.True(ValueParser.TryParse("1.500,25", true, out double grouped));
		Assert.Equal(1500.25, grouped);
		Assert.True(ValueParser.TryParse("1,5", true, out float single));
		Assert.Equal(1.5f, single);
		Assert.True(ValueParser.TryParse("1.500,25", true, out decimal money));
		Assert.Equal(1500.25m, money);
		Assert.True(ValueParser.TryParse("-7", true, out int integer));
		Assert.Equal(-7, integer);
	}

	[Fact]
	public void Numbers_CurrentCulture_WithAnEnglishCultureReadACommaAsAGroupSeparator()
	{
		CultureInfo.CurrentCulture = new CultureInfo("en-US");

		Assert.True(ValueParser.TryParse("1,5", true, out double value));
		Assert.Equal(15, value);
	}

	[Theory]
	[InlineData("2026-10-26", 2026, 10, 26, 0, 0, 0, 0, DateTimeKind.Unspecified)]
	[InlineData("2026-10-26T14:30", 2026, 10, 26, 14, 30, 0, 0, DateTimeKind.Unspecified)]
	[InlineData("2026-10-26T14:30:15", 2026, 10, 26, 14, 30, 15, 0, DateTimeKind.Unspecified)]
	[InlineData("2026-10-26T14:30:15.250", 2026, 10, 26, 14, 30, 15, 250, DateTimeKind.Unspecified)]
	[InlineData("2026-10-26T14:30Z", 2026, 10, 26, 14, 30, 0, 0, DateTimeKind.Utc)]
	[InlineData("2026-10-26T14:30:15.5Z", 2026, 10, 26, 14, 30, 15, 500, DateTimeKind.Utc)]
	public void DateTime_Strict_ReadsIso8601(string text, int year, int month, int day, int hour, int minute, int second, int millisecond, DateTimeKind kind)
	{
		Assert.True(ValueParser.TryParse(text, false, out DateTime value));
		Assert.Equal(new DateTime(year, month, day, hour, minute, second, millisecond, kind), value);
		Assert.Equal(kind, value.Kind);
	}

	[Fact]
	public void DateTime_Strict_ReadsSevenDigitsOfAFraction()
	{
		Assert.True(ValueParser.TryParse("2026-10-26T14:30:15.1234567Z", false, out DateTime value));

		Assert.Equal(new DateTime(2026, 10, 26, 14, 30, 15, DateTimeKind.Utc).AddTicks(1234567), value);
	}

	[Fact]
	public void DateTime_Strict_ConvertsAnOffsetToLocalTime()
	{
		Assert.True(ValueParser.TryParse("2026-10-26T14:30:00+02:00", false, out DateTime value));

		Assert.Equal(DateTimeKind.Local, value.Kind);
		Assert.Equal(new DateTime(2026, 10, 26, 12, 30, 0, DateTimeKind.Utc), value.ToUniversalTime());
	}

	[Theory]
	[InlineData("01/02/2026")]
	[InlineData("26-10-2026")]
	[InlineData("2026-1-2")]
	[InlineData("2026-10-26 14:30")]
	[InlineData("2026-10-26t14:30")]
	[InlineData("2026-10-26T14")]
	[InlineData("2026-10-26T14:30:15.")]
	[InlineData("2026-10-26T14:30:15,5")]
	[InlineData("2026-10-26T14:30:15.12345678")]
	[InlineData("26 oktober 2026")]
	[InlineData("")]
	[InlineData(null)]
	public void DateTime_Strict_RejectsEverythingButIso8601(string? text)
	{
		Assert.False(ValueParser.TryParse(text, false, out DateTime _));
	}

	[Fact]
	public void DateTime_CurrentCulture_ReadsTheFormatsOfTheCulture()
	{
		Assert.True(ValueParser.TryParse("01-02-2026", true, out DateTime dutch));
		Assert.Equal(new DateTime(2026, 2, 1), dutch);

		CultureInfo.CurrentCulture = new CultureInfo("en-US");
		Assert.True(ValueParser.TryParse("01/02/2026", true, out DateTime english));
		Assert.Equal(new DateTime(2026, 1, 2), english);
	}

	[Fact]
	public void TimeSpan_Strict_UsesThePointForFractions()
	{
		Assert.True(ValueParser.TryParse("1:30:15.5", false, out TimeSpan value));
		Assert.Equal(new TimeSpan(0, 1, 30, 15, 500), value);
		Assert.True(ValueParser.TryParse("2.01:00", false, out TimeSpan days));
		Assert.Equal(new TimeSpan(2, 1, 0, 0), days);
		Assert.False(ValueParser.TryParse("1:30:15,5", false, out TimeSpan _));
	}

	[Fact]
	public void TimeSpan_CurrentCulture_UsesTheSeparatorOfTheCulture()
	{
		Assert.True(ValueParser.TryParse("1:30:15,5", true, out TimeSpan value));
		Assert.Equal(new TimeSpan(0, 1, 30, 15, 500), value);
	}

	[Fact]
	public void Attributes_UseCurrentCulture_DefaultsToFalseAndIsStored()
	{
		Assert.False(new ParameterCollectionAttribute().UseCurrentCulture);
		Assert.True(new ParameterCollectionAttribute { UseCurrentCulture = true }.UseCurrentCulture);
		Assert.False(new CommandAppAttribute("app").UseCurrentCulture);
		Assert.True(new CommandAppAttribute("app") { UseCurrentCulture = true }.UseCurrentCulture);
	}
}
