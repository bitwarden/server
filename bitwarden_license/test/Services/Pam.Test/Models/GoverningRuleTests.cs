using System.Text.Json;
using Bit.Pam.Enums;
using Bit.Services.Pam.Models;
using Bit.Services.Pam.Models.Conditions;
using Xunit;

namespace Bit.Services.Pam.Test.Models;

public class GoverningRuleTests
{
    private static GoverningRule RuleWith(params AccessCondition[] conditions) =>
        new(Guid.NewGuid(), Guid.NewGuid(), conditions.Any(c => c is HumanApprovalCondition), conditions);

    [Fact]
    public void AutomatedConditionKind_NoConditions_IsNull()
    {
        Assert.Null(RuleWith().AutomatedConditionKind);
    }

    [Fact]
    public void AutomatedConditionKind_OnlyHumanApproval_IsNull()
    {
        Assert.Null(RuleWith(new HumanApprovalCondition()).AutomatedConditionKind);
    }

    [Fact]
    public void AutomatedConditionKind_IpAllowlist_IsIpAllowlist()
    {
        var rule = RuleWith(new IpAllowlistCondition { Cidrs = ["10.0.0.0/8"] });

        Assert.Equal(AccessConditionKind.IpAllowlist, rule.AutomatedConditionKind);
    }

    [Fact]
    public void AutomatedConditionKind_IpAllowlistBehindHumanApproval_IsIpAllowlist()
    {
        var rule = RuleWith(
            new HumanApprovalCondition(),
            new IpAllowlistCondition { Cidrs = ["10.0.0.0/8"] },
            new IpAllowlistCondition { Cidrs = ["192.168.0.0/16"] });

        Assert.Equal(AccessConditionKind.IpAllowlist, rule.AutomatedConditionKind);
    }

    [Fact]
    public void ConditionKind_IsNotWrittenToTheStoredConditionsJson()
    {
        AccessCondition[] conditions = [new HumanApprovalCondition(), new IpAllowlistCondition { Cidrs = ["10.0.0.0/8"] }];

        var json = JsonSerializer.Serialize(conditions, AccessConditionJson.Options);

        Assert.Equal(
            """[{"kind":"human_approval"},{"kind":"ip_allowlist","cidrs":["10.0.0.0/8"]}]""",
            json);
    }
}
