using System.Net;
using Bit.Commercial.Pam.Engine;
using Bit.Commercial.Pam.Enums;
using Bit.Commercial.Pam.Models.Conditions;
using Xunit;

namespace Bit.Commercial.Pam.Test.Engine;

public class AccessRuleEngineTests
{
    // 2026-06-04T12:00:00Z is a Thursday, so "thu" windows match in UTC.
    private static readonly DateTimeOffset _now = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly AccessRuleEngine _sut = new();

    private static AccessSignals Signals(IPAddress? ip = null, DateTimeOffset? at = null) => new()
    {
        IpAddress = ip,
        Timestamp = at ?? _now,
    };

    private static AccessCondition[] Set(params AccessCondition[] conditions) => conditions;

    [Fact]
    public void Evaluate_HumanApproval_RequiresApproval()
    {
        var evaluation = _sut.Evaluate(Set(new HumanApprovalCondition()), Signals());

        Assert.Equal(AccessEvaluationOutcome.RequiresApproval, evaluation.Outcome);
    }

    [Fact]
    public void Evaluate_IpAllowlist_IpInRange_Allows()
    {
        var conditions = Set(new IpAllowlistCondition { Cidrs = ["10.0.0.0/8"] });

        var evaluation = _sut.Evaluate(conditions, Signals(IPAddress.Parse("10.1.2.3")));

        Assert.Equal(AccessEvaluationOutcome.Allow, evaluation.Outcome);
    }

    [Fact]
    public void Evaluate_IpAllowlist_IpOutOfRange_Denies()
    {
        var conditions = Set(new IpAllowlistCondition { Cidrs = ["10.0.0.0/8"] });

        var evaluation = _sut.Evaluate(conditions, Signals(IPAddress.Parse("192.168.1.1")));

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.NotWithinIpRange, evaluation.Reason);
    }

    [Fact]
    public void Evaluate_IpAllowlist_UnknownIp_DeniesClosed()
    {
        var conditions = Set(new IpAllowlistCondition { Cidrs = ["10.0.0.0/8"] });

        var evaluation = _sut.Evaluate(conditions, Signals(ip: null));

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.NotWithinIpRange, evaluation.Reason);
    }

    [Fact]
    public void Evaluate_IpAllowlist_NoEntries_DeniesClosed()
    {
        var evaluation = _sut.Evaluate(Set(new IpAllowlistCondition()), Signals(IPAddress.Parse("10.1.2.3")));

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.NotWithinIpRange, evaluation.Reason);
    }

    [Fact]
    public void Evaluate_TimeOfDay_WithinWindow_Allows()
    {
        var conditions = Set(new TimeOfDayCondition
        {
            Tz = "UTC",
            Windows = [new TimeWindow { Days = [AccessWeekday.Thu], From = "09:00", To = "17:00" }],
        });

        var evaluation = _sut.Evaluate(conditions, Signals());

        Assert.Equal(AccessEvaluationOutcome.Allow, evaluation.Outcome);
    }

    [Fact]
    public void Evaluate_TimeOfDay_OutsideTimeWindow_Denies()
    {
        var conditions = Set(new TimeOfDayCondition
        {
            Tz = "UTC",
            Windows = [new TimeWindow { Days = [AccessWeekday.Thu], From = "00:00", To = "06:00" }],
        });

        var evaluation = _sut.Evaluate(conditions, Signals());

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.NotWithinTimeWindow, evaluation.Reason);
    }

    [Fact]
    public void Evaluate_TimeOfDay_DayNotListed_Denies()
    {
        var conditions = Set(new TimeOfDayCondition
        {
            Tz = "UTC",
            Windows = [new TimeWindow { Days = [AccessWeekday.Fri], From = "00:00", To = "23:59" }],
        });

        var evaluation = _sut.Evaluate(conditions, Signals());

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.NotWithinTimeWindow, evaluation.Reason);
    }

    [Fact]
    public void Evaluate_TimeOfDay_EvaluatesInConfiguredTimezone()
    {
        // 23:00 UTC is 19:00 (Thursday) in America/New_York during June DST, inside the window.
        var conditions = Set(new TimeOfDayCondition
        {
            Tz = "America/New_York",
            Windows = [new TimeWindow { Days = [AccessWeekday.Thu], From = "18:00", To = "20:00" }],
        });

        var evaluation = _sut.Evaluate(conditions, Signals(at: new DateTimeOffset(2026, 6, 4, 23, 0, 0, TimeSpan.Zero)));

        Assert.Equal(AccessEvaluationOutcome.Allow, evaluation.Outcome);
    }

    [Fact]
    public void Evaluate_TimeOfDay_UnknownTimezone_DeniesClosed()
    {
        var conditions = Set(new TimeOfDayCondition
        {
            Tz = "Not/AZone",
            Windows = [new TimeWindow { Days = [AccessWeekday.Thu], From = "00:00", To = "23:59" }],
        });

        var evaluation = _sut.Evaluate(conditions, Signals());

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.NotWithinTimeWindow, evaluation.Reason);
    }

    [Fact]
    public void Evaluate_AllConditionsAllow_Allows()
    {
        var conditions = Set(
            new IpAllowlistCondition { Cidrs = ["10.0.0.0/8"] },
            new TimeOfDayCondition { Tz = "UTC", Windows = [new TimeWindow { Days = [AccessWeekday.Thu], From = "09:00", To = "17:00" }] });

        var evaluation = _sut.Evaluate(conditions, Signals(IPAddress.Parse("10.1.2.3")));

        Assert.Equal(AccessEvaluationOutcome.Allow, evaluation.Outcome);
    }

    [Fact]
    public void Evaluate_OneConditionDenies_DeniesWithThatReason()
    {
        var conditions = Set(
            new IpAllowlistCondition { Cidrs = ["10.0.0.0/8"] },
            new TimeOfDayCondition { Tz = "UTC", Windows = [new TimeWindow { Days = [AccessWeekday.Thu], From = "00:00", To = "06:00" }] });

        var evaluation = _sut.Evaluate(conditions, Signals(IPAddress.Parse("10.1.2.3")));

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.NotWithinTimeWindow, evaluation.Reason);
    }

    [Fact]
    public void Evaluate_AllowPlusHumanApproval_RequiresApproval()
    {
        var conditions = Set(
            new IpAllowlistCondition { Cidrs = ["10.0.0.0/8"] },
            new HumanApprovalCondition());

        var evaluation = _sut.Evaluate(conditions, Signals(IPAddress.Parse("10.1.2.3")));

        Assert.Equal(AccessEvaluationOutcome.RequiresApproval, evaluation.Outcome);
    }

    [Fact]
    public void Evaluate_DenyOutranksApproval()
    {
        // A denying condition beats a pending approval: there is nothing to approve if access is barred outright.
        var conditions = Set(
            new HumanApprovalCondition(),
            new IpAllowlistCondition { Cidrs = ["10.0.0.0/8"] });

        var evaluation = _sut.Evaluate(conditions, Signals(IPAddress.Parse("192.168.1.1")));

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.NotWithinIpRange, evaluation.Reason);
    }

    [Fact]
    public void Evaluate_NoConditions_Allows()
    {
        // A rule with no conditions is vacuously satisfied: access is auto-granted while still flowing through
        // PAM for audit logging.
        var evaluation = _sut.Evaluate(Set(), Signals());

        Assert.Equal(AccessEvaluationOutcome.Allow, evaluation.Outcome);
    }

    [Fact]
    public void Evaluate_UnsupportedConditionKind_DeniesClosed()
    {
        var evaluation = _sut.Evaluate(Set(new UnknownCondition()), Signals());

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.UnsupportedCondition, evaluation.Reason);
    }

    private sealed class UnknownCondition : AccessCondition;
}
