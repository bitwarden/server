using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Enums;
using Bit.Services.Pam.Models.Conditions;
using Xunit;

namespace Bit.Services.Pam.Test.Models.Conditions;

public class TimeOfDayConditionTests
{
    // 2026-06-04T12:00:00Z is a Thursday, so "thu" windows match in UTC.
    private static readonly DateTimeOffset _now = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    private static AccessSignals Signals(DateTimeOffset? at = null) => new()
    {
        IpAddress = null,
        Timestamp = at ?? _now,
    };

    [Fact]
    public void Evaluate_WithinWindow_Allows()
    {
        var condition = new TimeOfDayCondition
        {
            Tz = "UTC",
            Windows = [new TimeWindow { Days = [AccessWeekday.Thu], From = "09:00", To = "17:00" }],
        };

        Assert.Equal(AccessEvaluationOutcome.Allow, condition.Evaluate(Signals()).Outcome);
    }

    [Fact]
    public void Evaluate_OutsideWindow_Denies()
    {
        var condition = new TimeOfDayCondition
        {
            Tz = "UTC",
            Windows = [new TimeWindow { Days = [AccessWeekday.Thu], From = "00:00", To = "06:00" }],
        };

        var evaluation = condition.Evaluate(Signals());

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.NotWithinTimeWindow, evaluation.Reason);
    }

    [Fact]
    public void Evaluate_DayNotListed_Denies()
    {
        var condition = new TimeOfDayCondition
        {
            Tz = "UTC",
            Windows = [new TimeWindow { Days = [AccessWeekday.Fri], From = "00:00", To = "23:59" }],
        };

        var evaluation = condition.Evaluate(Signals());

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.NotWithinTimeWindow, evaluation.Reason);
    }

    [Fact]
    public void Evaluate_EvaluatesInConfiguredTimezone_Allows()
    {
        // 23:00 UTC is 19:00 (Thursday) in America/New_York during June DST, inside the window.
        var condition = new TimeOfDayCondition
        {
            Tz = "America/New_York",
            Windows = [new TimeWindow { Days = [AccessWeekday.Thu], From = "18:00", To = "20:00" }],
        };

        var evaluation = condition.Evaluate(Signals(new DateTimeOffset(2026, 6, 4, 23, 0, 0, TimeSpan.Zero)));

        Assert.Equal(AccessEvaluationOutcome.Allow, evaluation.Outcome);
    }

    [Fact]
    public void Evaluate_UnknownTimezone_DeniesClosed()
    {
        var condition = new TimeOfDayCondition
        {
            Tz = "Not/AZone",
            Windows = [new TimeWindow { Days = [AccessWeekday.Thu], From = "00:00", To = "23:59" }],
        };

        var evaluation = condition.Evaluate(Signals());

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.NotWithinTimeWindow, evaluation.Reason);
    }

    [Fact]
    public void Evaluate_MalformedWindowTime_DeniesClosed()
    {
        // The day matches but the window's bounds are unparseable, so the window cannot admit the caller.
        var condition = new TimeOfDayCondition
        {
            Tz = "UTC",
            Windows = [new TimeWindow { Days = [AccessWeekday.Thu], From = "25:99", To = "30:00" }],
        };

        var evaluation = condition.Evaluate(Signals());

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.NotWithinTimeWindow, evaluation.Reason);
    }

    [Fact]
    public void Evaluate_LaterWindowMatches_Allows()
    {
        // The first window is the wrong day; the second admits the caller, so windows past the first must be checked.
        var condition = new TimeOfDayCondition
        {
            Tz = "UTC",
            Windows =
            [
                new TimeWindow { Days = [AccessWeekday.Fri], From = "09:00", To = "17:00" },
                new TimeWindow { Days = [AccessWeekday.Thu], From = "09:00", To = "17:00" },
            ],
        };

        Assert.Equal(AccessEvaluationOutcome.Allow, condition.Evaluate(Signals()).Outcome);
    }

    [Fact]
    public void Validate_MissingTz_IsInvalid()
    {
        var result = new TimeOfDayCondition { Windows = [ValidWindow()] }.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("tz", result.Error);
    }

    [Fact]
    public void Validate_UnknownTz_IsInvalid()
    {
        var result = new TimeOfDayCondition { Tz = "Invalid/Zone", Windows = [ValidWindow()] }.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("timezone", result.Error);
    }

    [Fact]
    public void Validate_NoWindows_IsInvalid()
    {
        var result = new TimeOfDayCondition { Tz = "UTC", Windows = [] }.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("at least one window", result.Error);
    }

    [Fact]
    public void Validate_Valid_IsValid()
    {
        var result = new TimeOfDayCondition { Tz = "UTC", Windows = [ValidWindow()] }.Validate();

        Assert.True(result.IsValid);
    }

    private static TimeWindow ValidWindow() => new() { Days = [AccessWeekday.Mon], From = "09:00", To = "17:00" };
}

public class TimeWindowTests
{
    private static TimeWindow BusinessHours() => new()
    {
        Days = [AccessWeekday.Thu],
        From = "09:00",
        To = "17:00",
    };

    [Fact]
    public void Contains_TimeEqualsFrom_IsInclusive()
    {
        // The lower bound is inclusive.
        Assert.True(BusinessHours().Contains(DayOfWeek.Thursday, new TimeOnly(9, 0)));
    }

    [Fact]
    public void Contains_TimeEqualsTo_IsInclusive()
    {
        // The upper bound is inclusive.
        Assert.True(BusinessHours().Contains(DayOfWeek.Thursday, new TimeOnly(17, 0)));
    }

    [Fact]
    public void Contains_TimeInside_True()
    {
        Assert.True(BusinessHours().Contains(DayOfWeek.Thursday, new TimeOnly(12, 0)));
    }

    [Fact]
    public void Contains_TimeJustBeforeFrom_False()
    {
        Assert.False(BusinessHours().Contains(DayOfWeek.Thursday, new TimeOnly(8, 59)));
    }

    [Fact]
    public void Contains_TimeJustAfterTo_False()
    {
        Assert.False(BusinessHours().Contains(DayOfWeek.Thursday, new TimeOnly(17, 1)));
    }

    [Fact]
    public void Contains_DayNotListed_False()
    {
        Assert.False(BusinessHours().Contains(DayOfWeek.Friday, new TimeOnly(12, 0)));
    }

    [Fact]
    public void Contains_UnparseableBounds_FailsClosed()
    {
        // Bounds that are not HH:mm admit nothing, even on a matching day and a plausible time.
        var window = new TimeWindow { Days = [AccessWeekday.Thu], From = "9am", To = "5pm" };

        Assert.False(window.Contains(DayOfWeek.Thursday, new TimeOnly(12, 0)));
    }

    [Fact]
    public void Validate_NoDays_IsInvalid()
    {
        var result = new TimeWindow { Days = [], From = "09:00", To = "17:00" }.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("at least one day", result.Error);
    }

    [Theory]
    [InlineData("9am", "17:00")]
    [InlineData("25:00", "17:00")]
    public void Validate_InvalidFrom_IsInvalid(string from, string to)
    {
        var result = new TimeWindow { Days = [AccessWeekday.Mon], From = from, To = to }.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("Expected HH:mm", result.Error);
    }

    [Fact]
    public void Validate_InvalidTo_IsInvalid()
    {
        var result = new TimeWindow { Days = [AccessWeekday.Mon], From = "09:00", To = "5pm" }.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("Expected HH:mm", result.Error);
    }

    [Fact]
    public void Validate_ValidWindow_IsValid()
    {
        var result = new TimeWindow { Days = [AccessWeekday.Mon], From = "09:00", To = "17:00" }.Validate();

        Assert.True(result.IsValid);
    }
}
