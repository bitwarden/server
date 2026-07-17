using System.Net;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Models.Conditions;
using Xunit;

namespace Bit.Services.Pam.Test.Engine;

public class AccessRuleEngineTests
{
    private readonly AccessRuleEngine _sut = new();

    private static AccessSignals Signals(IPAddress? ip = null) => new()
    {
        IpAddress = ip,
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
    public void Evaluate_AllConditionsAllow_Allows()
    {
        var conditions = Set(
            new IpAllowlistCondition { Cidrs = ["10.0.0.0/8"] },
            new IpAllowlistCondition { Cidrs = ["10.1.0.0/16"] });

        var evaluation = _sut.Evaluate(conditions, Signals(IPAddress.Parse("10.1.2.3")));

        Assert.Equal(AccessEvaluationOutcome.Allow, evaluation.Outcome);
    }

    [Fact]
    public void Evaluate_OneConditionDenies_DeniesWithThatReason()
    {
        // The reason travels from the condition that denied, not from the rule as a whole.
        var conditions = Set(
            new IpAllowlistCondition { Cidrs = ["10.0.0.0/8"] },
            new IpAllowlistCondition { Cidrs = ["192.168.0.0/16"] });

        var evaluation = _sut.Evaluate(conditions, Signals(IPAddress.Parse("10.1.2.3")));

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.NotWithinIpRange, evaluation.Reason);
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
    public void Evaluate_NullConditionEntry_DeniesClosed()
    {
        // A null entry (only reachable from a malformed stored document) cannot be evaluated, so it fails closed.
        // An unknown condition kind can no longer reach the engine: visitor dispatch is exhaustive at compile time.
        var evaluation = _sut.Evaluate([null!], Signals());

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.UnsupportedCondition, evaluation.Reason);
    }

    [Fact]
    public void Evaluate_IpAllowlist_MalformedCidr_DeniesClosed()
    {
        // A present-but-unparseable CIDR matches no address, so a caller with a known IP still fails closed.
        var conditions = Set(new IpAllowlistCondition { Cidrs = ["not-a-cidr"] });

        var evaluation = _sut.Evaluate(conditions, Signals(IPAddress.Parse("10.1.2.3")));

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.NotWithinIpRange, evaluation.Reason);
    }

    [Fact]
    public void Evaluate_IpAllowlist_LaterCidrMatches_Allows()
    {
        // The caller matches the second entry, so evaluation must not stop at the first non-matching CIDR.
        var conditions = Set(new IpAllowlistCondition { Cidrs = ["192.168.0.0/16", "10.0.0.0/8"] });

        var evaluation = _sut.Evaluate(conditions, Signals(IPAddress.Parse("10.1.2.3")));

        Assert.Equal(AccessEvaluationOutcome.Allow, evaluation.Outcome);
    }

}
