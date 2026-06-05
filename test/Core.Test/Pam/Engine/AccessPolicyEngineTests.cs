using System.Net;
using Bit.Core.Pam.Engine;
using Bit.Core.Pam.Models.Rules;
using Xunit;

namespace Bit.Core.Test.Pam.Engine;

public class AccessPolicyEngineTests
{
    // 2026-06-04T12:00:00Z is a Thursday, so "thu" windows match in UTC.
    private static readonly DateTimeOffset _now = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly AccessPolicyEngine _sut = new();

    private static AccessPolicySignals Signals(IPAddress? ip = null, DateTimeOffset? at = null) => new()
    {
        IpAddress = ip,
        Timestamp = at ?? _now,
    };

    [Fact]
    public void Evaluate_HumanApproval_RequiresApproval()
    {
        var decision = _sut.Evaluate(new HumanApprovalRule(), Signals());

        Assert.Equal(DecisionKind.RequiresApproval, decision.Kind);
    }

    [Fact]
    public void Evaluate_IpAllowlist_IpInRange_Allows()
    {
        var rule = new IpAllowlistRule { Cidrs = ["10.0.0.0/8"] };

        var decision = _sut.Evaluate(rule, Signals(IPAddress.Parse("10.1.2.3")));

        Assert.Equal(DecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public void Evaluate_IpAllowlist_IpOutOfRange_Denies()
    {
        var rule = new IpAllowlistRule { Cidrs = ["10.0.0.0/8"] };

        var decision = _sut.Evaluate(rule, Signals(IPAddress.Parse("192.168.1.1")));

        Assert.Equal(DecisionKind.Deny, decision.Kind);
        Assert.Equal(DenyReason.NotWithinIpRange, decision.Reason);
    }

    [Fact]
    public void Evaluate_IpAllowlist_UnknownIp_DeniesClosed()
    {
        var rule = new IpAllowlistRule { Cidrs = ["10.0.0.0/8"] };

        var decision = _sut.Evaluate(rule, Signals(ip: null));

        Assert.Equal(DecisionKind.Deny, decision.Kind);
        Assert.Equal(DenyReason.NotWithinIpRange, decision.Reason);
    }

    [Fact]
    public void Evaluate_IpAllowlist_NoEntries_DeniesClosed()
    {
        var decision = _sut.Evaluate(new IpAllowlistRule(), Signals(IPAddress.Parse("10.1.2.3")));

        Assert.Equal(DecisionKind.Deny, decision.Kind);
        Assert.Equal(DenyReason.NotWithinIpRange, decision.Reason);
    }

    [Fact]
    public void Evaluate_TimeOfDay_WithinWindow_Allows()
    {
        var rule = new TimeOfDayRule
        {
            Tz = "UTC",
            Windows = [new TimeWindow { Days = ["thu"], From = "09:00", To = "17:00" }],
        };

        var decision = _sut.Evaluate(rule, Signals());

        Assert.Equal(DecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public void Evaluate_TimeOfDay_OutsideTimeWindow_Denies()
    {
        var rule = new TimeOfDayRule
        {
            Tz = "UTC",
            Windows = [new TimeWindow { Days = ["thu"], From = "00:00", To = "06:00" }],
        };

        var decision = _sut.Evaluate(rule, Signals());

        Assert.Equal(DecisionKind.Deny, decision.Kind);
        Assert.Equal(DenyReason.NotWithinTimeWindow, decision.Reason);
    }

    [Fact]
    public void Evaluate_TimeOfDay_DayNotListed_Denies()
    {
        var rule = new TimeOfDayRule
        {
            Tz = "UTC",
            Windows = [new TimeWindow { Days = ["fri"], From = "00:00", To = "23:59" }],
        };

        var decision = _sut.Evaluate(rule, Signals());

        Assert.Equal(DecisionKind.Deny, decision.Kind);
        Assert.Equal(DenyReason.NotWithinTimeWindow, decision.Reason);
    }

    [Fact]
    public void Evaluate_TimeOfDay_EvaluatesInConfiguredTimezone()
    {
        // 23:00 UTC is 19:00 (Thursday) in America/New_York during June DST, inside the window.
        var rule = new TimeOfDayRule
        {
            Tz = "America/New_York",
            Windows = [new TimeWindow { Days = ["thu"], From = "18:00", To = "20:00" }],
        };

        var decision = _sut.Evaluate(rule, Signals(at: new DateTimeOffset(2026, 6, 4, 23, 0, 0, TimeSpan.Zero)));

        Assert.Equal(DecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public void Evaluate_TimeOfDay_UnknownTimezone_DeniesClosed()
    {
        var rule = new TimeOfDayRule
        {
            Tz = "Not/AZone",
            Windows = [new TimeWindow { Days = ["thu"], From = "00:00", To = "23:59" }],
        };

        var decision = _sut.Evaluate(rule, Signals());

        Assert.Equal(DecisionKind.Deny, decision.Kind);
        Assert.Equal(DenyReason.NotWithinTimeWindow, decision.Reason);
    }

    [Fact]
    public void Evaluate_AllOf_AllAllow_Allows()
    {
        var rule = new AllOfRule
        {
            Rules =
            [
                new IpAllowlistRule { Cidrs = ["10.0.0.0/8"] },
                new TimeOfDayRule { Tz = "UTC", Windows = [new TimeWindow { Days = ["thu"], From = "09:00", To = "17:00" }] },
            ],
        };

        var decision = _sut.Evaluate(rule, Signals(IPAddress.Parse("10.1.2.3")));

        Assert.Equal(DecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public void Evaluate_AllOf_OneDenies_DeniesWithThatReason()
    {
        var rule = new AllOfRule
        {
            Rules =
            [
                new IpAllowlistRule { Cidrs = ["10.0.0.0/8"] },
                new TimeOfDayRule { Tz = "UTC", Windows = [new TimeWindow { Days = ["thu"], From = "00:00", To = "06:00" }] },
            ],
        };

        var decision = _sut.Evaluate(rule, Signals(IPAddress.Parse("10.1.2.3")));

        Assert.Equal(DecisionKind.Deny, decision.Kind);
        Assert.Equal(DenyReason.NotWithinTimeWindow, decision.Reason);
    }

    [Fact]
    public void Evaluate_AllOf_AllowPlusHumanApproval_RequiresApproval()
    {
        var rule = new AllOfRule
        {
            Rules =
            [
                new IpAllowlistRule { Cidrs = ["10.0.0.0/8"] },
                new HumanApprovalRule(),
            ],
        };

        var decision = _sut.Evaluate(rule, Signals(IPAddress.Parse("10.1.2.3")));

        Assert.Equal(DecisionKind.RequiresApproval, decision.Kind);
    }

    [Fact]
    public void Evaluate_AllOf_DenyOutranksApproval()
    {
        // A denying condition beats a pending approval: there is nothing to approve if access is barred outright.
        var rule = new AllOfRule
        {
            Rules =
            [
                new HumanApprovalRule(),
                new IpAllowlistRule { Cidrs = ["10.0.0.0/8"] },
            ],
        };

        var decision = _sut.Evaluate(rule, Signals(IPAddress.Parse("192.168.1.1")));

        Assert.Equal(DecisionKind.Deny, decision.Kind);
        Assert.Equal(DenyReason.NotWithinIpRange, decision.Reason);
    }

    [Fact]
    public void Evaluate_NestedAllOf_Allows()
    {
        var rule = new AllOfRule
        {
            Rules =
            [
                new AllOfRule { Rules = [new IpAllowlistRule { Cidrs = ["10.0.0.0/8"] }] },
                new TimeOfDayRule { Tz = "UTC", Windows = [new TimeWindow { Days = ["thu"], From = "09:00", To = "17:00" }] },
            ],
        };

        var decision = _sut.Evaluate(rule, Signals(IPAddress.Parse("10.1.2.3")));

        Assert.Equal(DecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public void Evaluate_UnsupportedRuleKind_DeniesClosed()
    {
        var decision = _sut.Evaluate(new UnknownRule(), Signals());

        Assert.Equal(DecisionKind.Deny, decision.Kind);
        Assert.Equal(DenyReason.UnsupportedRule, decision.Reason);
    }

    private sealed class UnknownRule : Rule;
}
