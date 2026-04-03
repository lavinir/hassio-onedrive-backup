using HassioOneDriveBackup.Utils;

namespace HassioOneDriveBackup.Tests;

public class TimeRangeHelperTests
{
    // --- GetAllowedHours ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrWhitespace_AllHoursAllowed(string? expression)
    {
        var result = TimeRangeHelper.GetAllowedHours(expression);
        for (int i = 0; i < 24; i++)
            Assert.True(result[i], $"Hour {i} should be allowed");
    }

    [Fact]
    public void SingleHour_OnlyThatHourAllowed()
    {
        var result = TimeRangeHelper.GetAllowedHours("9");
        Assert.True(result[9]);
        for (int i = 0; i < 24; i++)
            if (i != 9) Assert.False(result[i], $"Hour {i} should not be allowed");
    }

    [Fact]
    public void SingleRange_CorrectHoursAllowed()
    {
        var result = TimeRangeHelper.GetAllowedHours("8-17");
        for (int i = 8; i <= 17; i++)
            Assert.True(result[i], $"Hour {i} should be allowed");
        Assert.False(result[7]);
        Assert.False(result[18]);
    }

    [Fact]
    public void MultipleRanges_UnionAllowed()
    {
        var result = TimeRangeHelper.GetAllowedHours("0-6,22-23");
        for (int i = 0; i <= 6; i++)
            Assert.True(result[i]);
        for (int i = 22; i <= 23; i++)
            Assert.True(result[i]);
        Assert.False(result[7]);
        Assert.False(result[21]);
    }

    [Fact]
    public void EdgeHours_BoundaryIncluded()
    {
        var result = TimeRangeHelper.GetAllowedHours("0-0");
        Assert.True(result[0]);
        for (int i = 1; i < 24; i++)
            Assert.False(result[i]);

        var result2 = TimeRangeHelper.GetAllowedHours("23-23");
        Assert.True(result2[23]);
        for (int i = 0; i < 23; i++)
            Assert.False(result2[i]);
    }

    [Theory]
    [InlineData("25")]        // out of range
    [InlineData("10-8")]      // from > to
    [InlineData("abc")]       // non-numeric
    public void InvalidExpression_FallsBackToAllHours(string expression)
    {
        var result = TimeRangeHelper.GetAllowedHours(expression);
        for (int i = 0; i < 24; i++)
            Assert.True(result[i], $"Hour {i} should be allowed after invalid expression fallback");
    }

    [Fact]
    public void LeadingDashShorthand_TreatedAsZeroToN()
    {
        // "-5" means "0 through 5"
        var result = TimeRangeHelper.GetAllowedHours("-5");
        for (int i = 0; i <= 5; i++)
            Assert.True(result[i], $"Hour {i} should be allowed");
        Assert.False(result[6]);
    }

    // --- GetClosestAllowedTimeSlot ---

    [Fact]
    public void CurrentHourAllowed_ReturnsSameTime()
    {
        var target = new DateTime(2024, 6, 10, 10, 30, 0);
        var result = TimeRangeHelper.GetClosestAllowedTimeSlot(target, "8-18");
        Assert.Equal(target, result);
    }

    [Fact]
    public void CurrentHourBlocked_ReturnsNextAllowedHour()
    {
        var target = new DateTime(2024, 6, 10, 7, 0, 0);
        var result = TimeRangeHelper.GetClosestAllowedTimeSlot(target, "8-18");
        Assert.NotNull(result);
        Assert.Equal(8, result!.Value.Hour);
    }

    [Fact]
    public void CurrentHourBlocked_WrapsToNextDay()
    {
        // Hour 22 is blocked, allowed hours are 0-6 — next slot is hour 23? No, 22 is blocked.
        // allowed = 0-6, target at hour 22 → nearest allowed is 0 on next day (+2 hours)
        var target = new DateTime(2024, 6, 10, 22, 0, 0);
        var result = TimeRangeHelper.GetClosestAllowedTimeSlot(target, "0-6");
        Assert.NotNull(result);
        Assert.Equal(0, result!.Value.Hour);
    }

    [Fact]
    public void NullAllowedHours_ReturnsTarget()
    {
        var target = new DateTime(2024, 6, 10, 3, 0, 0);
        var result = TimeRangeHelper.GetClosestAllowedTimeSlot(target, null);
        Assert.Equal(target, result);
    }
}
