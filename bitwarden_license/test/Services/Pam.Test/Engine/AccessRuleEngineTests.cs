using System.Net;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Models.Conditions;
using Xunit;

namespace Bit.Services.Pam.Test.Engine;

public class AccessRuleEngineTests
{
    private readonly AccessRuleEngine _sut = new();

    // No case here has a time-of-day condition, so the instant only has to be deterministic.
    private static AccessSignals Signals() => new()
    {
        IpAddress = IPAddress.Parse("10.1.2.3"),
        Timestamp = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
    };

    [Fact]
    public void Evaluate_NoConditions_Allows()
    {
        // A rule with no conditions auto-grants, while the access still flows through PAM for auditing.
        Assert.Equal(AccessEvaluationOutcome.Allow, _sut.Evaluate([], Signals()).Outcome);
    }

    [Fact]
    public void Evaluate_DefersToEachConditionsOwnResult()
    {
        Assert.Equal(AccessEvaluationOutcome.RequiresApproval,
            _sut.Evaluate([new StubCondition(AccessEvaluation.RequiresApproval)], Signals()).Outcome);
    }

    [Fact]
    public void Evaluate_CombinesConditionResults_DenyWins()
    {
        // AccessEvaluation.Combine does the folding; AccessEvaluationTests covers its full precedence.
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
        // Only a malformed stored document yields a null entry, and it fails closed. Unknown kinds never reach
        // the engine, since deserialization rejects them.
        var evaluation = _sut.Evaluate([null!], Signals());

        Assert.Equal(AccessEvaluationOutcome.Deny, evaluation.Outcome);
        Assert.Equal(DenyReason.UnsupportedCondition, evaluation.Reason);
    }

    private sealed class StubCondition(AccessEvaluation result) : AccessCondition
    {
        public AccessSignals? ReceivedSignals { get; private set; }

        public override AccessEvaluation Evaluate(AccessSignals signals)
        {
            ReceivedSignals = signals;
            return result;
        }

        public override AccessRuleValidationResult Validate() => AccessRuleValidationResult.Valid;
    }
}
