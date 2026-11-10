using RotaWeb.Api.Services;
using Xunit;

namespace RotaWeb.Tests;

public sealed class AgePolicyTests
{
    [Theory]
    [InlineData(2008, 10, 27, 2026, 10, 26, 17)]
    [InlineData(2008, 10, 27, 2026, 10, 27, 18)]
    [InlineData(1990, 1, 1, 2026, 10, 27, 36)]
    [InlineData(2008, 2, 29, 2026, 2, 28, 17)]
    [InlineData(2008, 2, 29, 2026, 3, 1, 18)]
    public void CalculateAge_UsesFullCalendarYears(int by, int bm, int bd, int ry, int rm, int rd, int expected)
    {
        var actual = AgePolicy.CalculateAge(new DateOnly(by, bm, bd), new DateOnly(ry, rm, rd));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void IsAdult_RejectsFutureBirthDates() =>
        Assert.False(AgePolicy.IsAdult(new DateOnly(2030, 1, 1), new DateOnly(2026, 10, 27)));

    [Fact]
    public void IsAdult_AcceptsExactEighteenthBirthday() =>
        Assert.True(AgePolicy.IsAdult(new DateOnly(2008, 10, 27), new DateOnly(2026, 10, 27)));
}
