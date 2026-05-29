using System.Net;
using Bit.Core.Enums;
using Bit.Core.PrivilegedAccessManagement.Engine;
using Bit.Core.PrivilegedAccessManagement.Engine.Conditions;
using Xunit;

namespace Bit.Core.Test.PrivilegedAccessManagement.Engine.Conditions;

public sealed class IpRangeConditionTests
{
    private readonly IpRangeCondition _condition = new();

    [Fact]
    public void Evaluate_EmptyAllowlist_ReturnsAllow()
    {
        var decision = _condition.Evaluate(ContextFor([], "10.0.0.5"));

        Assert.Equal(DecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public void Evaluate_IpWithinRange_ReturnsAllow()
    {
        var decision = _condition.Evaluate(ContextFor(["10.0.0.0/24"], "10.0.0.5"));

        Assert.Equal(DecisionKind.Allow, decision.Kind);
    }

    [Fact]
    public void Evaluate_IpOutsideRange_ReturnsDenyNotWithinIpRange()
    {
        var decision = _condition.Evaluate(ContextFor(["10.0.0.0/24"], "192.168.1.5"));

        Assert.Equal(DecisionKind.Deny, decision.Kind);
        Assert.Equal(DenyReason.NotWithinIpRange, decision.Reason);
    }

    [Fact]
    public void Evaluate_UnparseableEntryIsSkipped_AndALaterMatchAllows()
    {
        var decision = _condition.Evaluate(ContextFor(["not-a-cidr", "10.0.0.0/24"], "10.0.0.5"));

        Assert.Equal(DecisionKind.Allow, decision.Kind);
    }

    private static AccessRuleEngineContext ContextFor(List<string> requiredCidr, string ipAddress) => new()
    {
        Rule = new AccessRule { Name = "rule", Duration = TimeSpan.FromHours(1), RequiredCidr = requiredCidr },
        Signals = new AccessRuleSignals
        {
            Username = "alice",
            IpAddress = IPAddress.Parse(ipAddress),
            MultifactorEnabled = true,
            UserTime = DateTimeOffset.UnixEpoch,
            Device = DeviceType.ChromeBrowser,
        },
    };
}
