using System.Text.Json;
using Bit.Seeder.Factories;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Factories;

public class AccessRuleSeederTests
{
    [Fact]
    public void Create_WithNoConditions_SerializesEmptyArrayWithDomainDefaults()
    {
        var orgId = Guid.NewGuid();

        var rule = AccessRuleSeeder.Create(orgId, "Auto");

        Assert.NotEqual(Guid.Empty, rule.Id);
        Assert.Equal(orgId, rule.OrganizationId);
        Assert.Equal("Auto", rule.Name);
        Assert.Equal("[]", rule.Conditions);
        Assert.True(rule.Enabled);
        Assert.False(rule.AllowsExtensions);
        Assert.False(rule.SingleActiveLease);
        Assert.Null(rule.DefaultLeaseDurationSeconds);
        Assert.Null(rule.MaxExtensionDurationSeconds);
    }

    [Fact]
    public void Create_WithIpAllowlist_SerializesIpAllowlistCondition()
    {
        var rule = AccessRuleSeeder.Create(Guid.NewGuid(), "Ip", ipAllowlistCidrs: ["10.0.0.0/8", "2001:db8::/32"]);

        using var doc = JsonDocument.Parse(rule.Conditions);
        var condition = Assert.Single(doc.RootElement.EnumerateArray());
        Assert.Equal("ip_allowlist", condition.GetProperty("kind").GetString());
        Assert.Equal(["10.0.0.0/8", "2001:db8::/32"],
            condition.GetProperty("cidrs").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public void Create_WithHumanApproval_SerializesHumanApprovalCondition()
    {
        var rule = AccessRuleSeeder.Create(Guid.NewGuid(), "Approval", requireHumanApproval: true);

        Assert.Equal("""[{"kind":"human_approval"}]""", rule.Conditions);
    }

    [Fact]
    public void Create_WithBoth_SerializesBothConditions()
    {
        var rule = AccessRuleSeeder.Create(Guid.NewGuid(), "Both",
            ipAllowlistCidrs: ["192.168.0.0/16"], requireHumanApproval: true);

        Assert.Equal(
            """[{"kind":"ip_allowlist","cidrs":["192.168.0.0/16"]},{"kind":"human_approval"}]""",
            rule.Conditions);
    }

    [Fact]
    public void Create_WithInvalidCidr_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            AccessRuleSeeder.Create(Guid.NewGuid(), "Bad", ipAllowlistCidrs: ["not-a-cidr"]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void Create_WithExtensionsAndNoPositiveMax_Throws(int? max)
    {
        Assert.Throws<ArgumentException>(() =>
            AccessRuleSeeder.Create(Guid.NewGuid(), "Ext", allowsExtensions: true, maxExtensionDurationSeconds: max));
    }

    [Fact]
    public void Create_MapsLeaseSettings()
    {
        var rule = AccessRuleSeeder.Create(Guid.NewGuid(), "Lease", description: "d", enabled: false,
            singleActiveLease: true, defaultLeaseDurationSeconds: 600, maxLeaseDurationSeconds: 3600,
            allowsExtensions: true, maxExtensionDurationSeconds: 300);

        Assert.Equal("d", rule.Description);
        Assert.False(rule.Enabled);
        Assert.True(rule.SingleActiveLease);
        Assert.Equal(600, rule.DefaultLeaseDurationSeconds);
        Assert.Equal(3600, rule.MaxLeaseDurationSeconds);
        Assert.True(rule.AllowsExtensions);
        Assert.Equal(300, rule.MaxExtensionDurationSeconds);
    }
}
