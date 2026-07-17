using System.Net;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Models.Conditions;
using Xunit;

namespace Bit.Services.Pam.Test.Engine;

public class AccessRuleEngineTests
{
    private readonly AccessRuleEngine _sut = new();

    private static AccessSignals Signals() => new()
    {
        IpAddress = IPAddress.Parse("10.1.2.3"),
        Timestamp = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero),
    };

    [Fact]
    public void Evaluate_NoConditions_Allows()
    {
        // A rule with no conditions is vacuously satisfied: access is auto-granted while still flowing through
        // PAM for audit logging.
        Assert.Equal(AccessEvaluationOutcome.Allow, _sut.Evaluate([], Signals()).Outcome);
    }

    [Fact]
    public void Evaluate_DefersToEachConditionsOwnResult()
    {
        // The engine does not decide anything itself; it returns what the condition's Evaluate reports.
        Assert.Equal(AccessEvaluationOutcome.RequiresApproval,
            _sut.Evaluate([new StubCondition(AccessEvaluation.RequiresApproval)], Signals()).Outcome);
    }

    [Fact]
    public void Evaluate_CombinesConditionResults_DenyWins()
    {
        // Folding is delegated to AccessEvaluation.Combine (deny > approval > allow); a single denying condition
        // drives the whole rule to deny. The full precedence matrix is covered in AccessEvaluationTests.
        var conditions = new AccessCondition[]
        {
            new StubCondition(AccessEvaluation.Allow),
            new StubCondition(AccessEvaluation.Deny(DenyReason.NotWithinIpRange)),
        };

        var evaluation = _sut.Evaluate(conditions, Signals());

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.NotWithinIpRange, evaluation.Reason);
    }

    [Fact]
    public void Evaluate_ForwardsSignalsToConditions()
    {
        var signals = Signals();
        var condition = new StubCondition(AccessEvaluation.Allow);

        _sut.Evaluate([condition], signals);

        Assert.Same(signals, condition.ReceivedSignals);
    }

    [Fact]
    public void Evaluate_NullConditionEntry_DeniesClosed()
    {
        // A null entry (only reachable from a malformed stored document) cannot be evaluated, so it fails closed.
        // An unknown condition kind can no longer reach the engine: it is rejected at JSON deserialization.
        var evaluation = _sut.Evaluate([null!], Signals());

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.UnsupportedCondition, evaluation.Reason);
    }

    /// <summary>A condition with a fixed result, isolating the engine's folding from any real condition logic.</summary>
    private sealed class StubCondition(AccessEvaluation result) : AccessCondition
    {
        public AccessSignals? ReceivedSignals { get; private set; }

        public override AccessEvaluation Evaluate(AccessSignals signals)
        {
            ReceivedSignals = signals;
            return result;
        }

        public override T Accept<T>(IAccessConditionVisitor<T> visitor) => throw new NotSupportedException();
    }
}
