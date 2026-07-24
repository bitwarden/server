using Bit.Services.Pam.Engine;
using Xunit;

namespace Bit.Services.Pam.Test.Engine;

public class AccessEvaluationTests
{
    [Fact]
    public void Combine_Empty_Allows()
    {
        // An empty rule is vacuously satisfied.
        Assert.Equal(AccessEvaluationOutcome.Allow, AccessEvaluation.Combine([]).Outcome);
    }

    [Fact]
    public void Combine_AllAllow_Allows()
    {
        var result = AccessEvaluation.Combine([AccessEvaluation.Allow, AccessEvaluation.Allow]);

        Assert.Equal(AccessEvaluationOutcome.Allow, result.Outcome);
    }

    [Fact]
    public void Combine_ApprovalOverAllow_RequiresApproval()
    {
        var result = AccessEvaluation.Combine([AccessEvaluation.Allow, AccessEvaluation.RequiresApproval]);

        Assert.Equal(AccessEvaluationOutcome.RequiresApproval, result.Outcome);
    }

    [Fact]
    public void Combine_DenyOutranksAllow_Denies()
    {
        var result = AccessEvaluation.Combine([AccessEvaluation.Allow, AccessEvaluation.Deny(DenyReason.NotWithinIpRange)]);

        Assert.Equal(AccessEvaluationOutcome.Deny, result.Outcome);
        Assert.Equal(DenyReason.NotWithinIpRange, result.Reason);
    }

    [Fact]
    public void Combine_DenyOutranksApproval_Denies()
    {
        // Deny beats a pending approval regardless of order: there is nothing to approve if access is barred.
        var result = AccessEvaluation.Combine([AccessEvaluation.RequiresApproval, AccessEvaluation.Deny(DenyReason.NotWithinTimeWindow)]);

        Assert.Equal(AccessEvaluationOutcome.Deny, result.Outcome);
        Assert.Equal(DenyReason.NotWithinTimeWindow, result.Reason);
    }

    [Fact]
    public void Combine_ApprovalAfterDeny_StillDenies()
    {
        var result = AccessEvaluation.Combine([AccessEvaluation.Deny(DenyReason.NotWithinIpRange), AccessEvaluation.RequiresApproval]);

        Assert.Equal(AccessEvaluationOutcome.Deny, result.Outcome);
        Assert.Equal(DenyReason.NotWithinIpRange, result.Reason);
    }

    [Fact]
    public void Combine_FirstDenyWins_PreservesItsReason()
    {
        // Combine short-circuits on the first deny, so its reason is the one reported.
        var result = AccessEvaluation.Combine(
        [
            AccessEvaluation.Deny(DenyReason.NotWithinTimeWindow),
            AccessEvaluation.Deny(DenyReason.NotWithinIpRange),
        ]);

        Assert.Equal(AccessEvaluationOutcome.Deny, result.Outcome);
        Assert.Equal(DenyReason.NotWithinTimeWindow, result.Reason);
    }
}
