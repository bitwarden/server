using System.Net;
using Bit.Core.Enums;
using Bit.Core.PrivilegedAccessManagement.Engine;
using Bit.Core.PrivilegedAccessManagement.Engine.Conditions;
using Xunit;

namespace Bit.Core.Test.PrivilegedAccessManagement.Engine.Conditions;

public sealed class TimeOfDayConditionTests
{
    private readonly TimeOfDayCondition _condition = new();

    // 2026-01-15 is a Thursday; 2026-01-16 is a Friday. January is EST (UTC-5) in New York.
    private static readonly DateTimeOffset ThursdayNoonUtc = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FridayNoonUtc = new(2026, 1, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Evaluate_NoConfig_ReturnsAllow()
    {
        var decision = _condition.Evaluate(ContextFor(null, ThursdayNoonUtc));

        Assert.Equal(DecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public void Evaluate_WithinWindow_ReturnsAllow()
    {
        var config = Config("UTC", Window(new TimeOnly(9, 0), new TimeOnly(17, 0), DayOfWeek.Thursday));

        var decision = _condition.Evaluate(ContextFor(config, ThursdayNoonUtc));

        Assert.Equal(DecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public void Evaluate_OutsideWindowTime_ReturnsDeny()
    {
        var config = Config("UTC", Window(new TimeOnly(9, 0), new TimeOnly(11, 0), DayOfWeek.Thursday));

        var decision = _condition.Evaluate(ContextFor(config, ThursdayNoonUtc));

        Assert.Equal(DecisionKind.Deny, decision.Kind);
        Assert.Equal(DenyReason.NotWithinTimeWindow, decision.Reason);
    }

    [Fact]
    public void Evaluate_NonMatchingDay_ReturnsDeny()
    {
        var config = Config("UTC", Window(new TimeOnly(9, 0), new TimeOnly(17, 0), DayOfWeek.Thursday));

        var decision = _condition.Evaluate(ContextFor(config, FridayNoonUtc));

        Assert.Equal(DecisionKind.Deny, decision.Kind);
        Assert.Equal(DenyReason.NotWithinTimeWindow, decision.Reason);
    }

    [Fact]
    public void Evaluate_EmptyDays_MatchesAnyDay()
    {
        var config = Config("UTC", Window(new TimeOnly(9, 0), new TimeOnly(17, 0)));

        var decision = _condition.Evaluate(ContextFor(config, FridayNoonUtc));

        Assert.Equal(DecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public void Evaluate_ConvertsUserTimeIntoConfiguredTimezone()
    {
        // 20:00 UTC is outside 09:00-17:00 in UTC, but is 15:00 Thursday in New York (EST), inside the window
        var config = Config("America/New_York", Window(new TimeOnly(9, 0), new TimeOnly(17, 0), DayOfWeek.Thursday));
        var userTime = new DateTimeOffset(2026, 1, 15, 20, 0, 0, TimeSpan.Zero);

        var decision = _condition.Evaluate(ContextFor(config, userTime));

        Assert.Equal(DecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public void Evaluate_InvalidTimezone_ReturnsDeny()
    {
        var config = Config("Not/AZone", Window(new TimeOnly(0, 0), new TimeOnly(23, 59), DayOfWeek.Thursday));

        var decision = _condition.Evaluate(ContextFor(config, ThursdayNoonUtc));

        Assert.Equal(DecisionKind.Deny, decision.Kind);
        Assert.Equal(DenyReason.NotWithinTimeWindow, decision.Reason);
    }

    private static TimeOfDayConfig Config(string timeZone, params AccessTimeWindow[] windows)
    {
        return new TimeOfDayConfig { TimeZone = timeZone, Windows = windows };
    }

    private static AccessTimeWindow Window(TimeOnly from, TimeOnly to, params DayOfWeek[] days)
    {
        return new AccessTimeWindow { Days = days, From = from, To = to };
    }

    private static AccessRuleEngineContext ContextFor(TimeOfDayConfig? config, DateTimeOffset userTime) => new()
    {
        Rule = new AccessRule { Name = "rule", Duration = TimeSpan.FromHours(1), TimeOfDay = config },
        Signals = new AccessRuleSignals
        {
            Username = "alice",
            IpAddress = IPAddress.Loopback,
            MultifactorEnabled = true,
            UserTime = userTime,
            Device = DeviceType.ChromeBrowser,
        },
    };
}
