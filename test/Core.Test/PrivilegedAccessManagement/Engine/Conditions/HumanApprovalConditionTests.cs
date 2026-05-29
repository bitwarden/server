using System.Net;
using Bit.Core.Enums;
using Bit.Core.PrivilegedAccessManagement.Engine;
using Bit.Core.PrivilegedAccessManagement.Engine.Conditions;
using Xunit;

namespace Bit.Core.Test.PrivilegedAccessManagement.Engine.Conditions;

public sealed class HumanApprovalConditionTests
{
    private readonly HumanApprovalCondition _condition = new();

    [Fact]
    public void Evaluate_RuleRequiresApproval_ReturnsRequiresApproval()
    {
        var context = ContextFor(new AccessRule { Name = "rule", Duration = TimeSpan.FromHours(1), RequireApproval = true });

        var decision = _condition.Evaluate(context);

        Assert.Equal(DecisionKind.RequiresApproval, decision.Kind);
    }

    [Fact]
    public void Evaluate_RuleDoesNotRequireApproval_ReturnsAllow()
    {
        var context = ContextFor(new AccessRule { Name = "rule", Duration = TimeSpan.FromHours(1) });

        var decision = _condition.Evaluate(context);

        Assert.Equal(DecisionKind.Allow, decision.Kind);
    }

    private static AccessRuleEngineContext ContextFor(AccessRule rule) => new()
    {
        Rule = rule,
        Signals = new AccessRuleSignals
        {
            Username = "alice",
            IpAddress = IPAddress.Loopback,
            MultifactorEnabled = true,
            UserTime = DateTimeOffset.UnixEpoch,
            Device = DeviceType.ChromeBrowser,
        },
    };
}
