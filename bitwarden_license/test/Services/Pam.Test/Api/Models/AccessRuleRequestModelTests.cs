using System.Text.Json;
using System.Text.Json.Nodes;
using Bit.Services.Pam.Api.Models.Request;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Models;

public class AccessRuleRequestModelTests
{
    [Fact]
    public void ToAccessRule_CopiesTheEditableFieldsOntoTheRouteOrganization()
    {
        var organizationId = Guid.NewGuid();
        var model = new AccessRuleRequestModel
        {
            Name = "Production database",
            Description = "Requires an approver",
            Enabled = false,
            Conditions = Parse("[]"),
            SingleActiveLease = true,
            DefaultLeaseDurationSeconds = 900,
            MaxLeaseDurationSeconds = 3600,
            AllowsExtensions = true,
            MaxExtensionDurationSeconds = 300,
            Collections = [Guid.NewGuid()],
        };

        var rule = model.ToAccessRule(organizationId);

        Assert.Equal(organizationId, rule.OrganizationId);
        Assert.Equal("Production database", rule.Name);
        Assert.Equal("Requires an approver", rule.Description);
        Assert.False(rule.Enabled);
        Assert.True(rule.SingleActiveLease);
        Assert.Equal(900, rule.DefaultLeaseDurationSeconds);
        Assert.Equal(3600, rule.MaxLeaseDurationSeconds);
        Assert.True(rule.AllowsExtensions);
        Assert.Equal(300, rule.MaxExtensionDurationSeconds);
    }

    /// <summary>
    /// The conditions document is persisted as the client sent it rather than round-tripped through the condition
    /// types, so anything this version does not model still reaches whoever reads the rule back.
    /// </summary>
    [Fact]
    public void ToAccessRule_StoresTheConditionsDocumentVerbatim()
    {
        const string conditions =
            """[{"kind":"ip_allowlist","cidrs":["10.0.0.0/8"],"unmodelled":"kept"}]""";

        var rule = NewModel(Parse(conditions)).ToAccessRule(Guid.NewGuid());

        Assert.Equal(JsonNode.Parse(conditions)!.ToJsonString(), JsonNode.Parse(rule.Conditions)!.ToJsonString());
        Assert.Contains("unmodelled", rule.Conditions, StringComparison.Ordinal);
    }

    /// <summary>
    /// Conditions is bound as <c>object</c>, which is a <see cref="JsonElement"/> over the wire but an ordinary CLR
    /// value for anything constructing the model in process. The fallback has to serialize that value rather than
    /// store its <c>ToString()</c>.
    /// </summary>
    [Fact]
    public void ToAccessRule_SerializesConditionsThatAreNotAJsonElement()
    {
        var rule = NewModel(new[] { new { kind = "human_approval" } }).ToAccessRule(Guid.NewGuid());

        Assert.Equal("""[{"kind":"human_approval"}]""", rule.Conditions);
    }

    private static AccessRuleRequestModel NewModel(object conditions) => new()
    {
        Name = "Production database",
        Conditions = conditions,
        Collections = [],
    };

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;
}
