using System.Text.Json;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Response;

public class PolicyStatusResponseModelTests
{
    // Note: only non-null/empty Data payloads are covered here. For null/empty Data, the legacy
    // implementation had a bug where it defaulted to an empty dictionary ("data":{}) instead of
    // null; that behavior is intentionally NOT preserved (see the SerializesDataAsNull test below).
    public static IEnumerable<object[]> PolicyDataByType => new List<object[]>
    {
        new object[] { PolicyType.MasterPassword, "{\"minComplexity\":3,\"minLength\":14,\"requireLower\":true,\"requireUpper\":true,\"requireNumbers\":false,\"requireSpecial\":false,\"enforceOnLogin\":true}" },
        new object[] { PolicyType.PasswordGenerator, "{\"defaultType\":\"password\",\"minLength\":16}" },
        new object[] { PolicyType.SendOptions, "{\"disableHideEmail\":true}" },
        new object[] { PolicyType.ResetPassword, "{\"autoEnrollEnabled\":true}" },
    };

    [Theory]
    [MemberData(nameof(PolicyDataByType))]
    public void Constructor_SerializesIdenticallyToLegacyDictionaryApproach(PolicyType type, string data)
    {
        var organizationId = Guid.NewGuid();
        var policyStatus = new PolicyStatus(organizationId, type)
        {
            Enabled = true,
            Data = data,
        };

        var legacyJson = JsonSerializer.Serialize(new LegacyPolicyStatusResponseModel(policyStatus));
        var currentJson = JsonSerializer.Serialize(new PolicyStatusResponseModel(policyStatus));

        Assert.Equal(legacyJson, currentJson);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceData_SerializesDataAsNull_NotEmptyObject(string? data)
    {
        var policyStatus = new PolicyStatus(Guid.NewGuid(), PolicyType.SingleOrg)
        {
            Enabled = true,
            Data = data,
        };

        var model = new PolicyStatusResponseModel(policyStatus);
        var json = JsonSerializer.Serialize(model);

        Assert.Null(model.Data);
        Assert.Contains("\"Data\":null", json);
        Assert.DoesNotContain("\"Data\":{}", json);
    }

    /// <summary>
    /// Mirrors the pre-refactor implementation (Dictionary&lt;string, object&gt; deserialize/serialize round trip,
    /// defaulting to an empty dictionary) so we can prove the raw string pass-through produces byte-for-byte
    /// identical JSON for non-null Data.
    /// </summary>
    private class LegacyPolicyStatusResponseModel
    {
        public LegacyPolicyStatusResponseModel(PolicyStatus policy, bool canToggleState = true)
        {
            OrganizationId = policy.OrganizationId;
            Type = policy.Type;

            if (!string.IsNullOrWhiteSpace(policy.Data))
            {
                Data = JsonSerializer.Deserialize<Dictionary<string, object>>(policy.Data) ?? new();
            }

            Enabled = policy.Enabled;
            CanToggleState = canToggleState;
        }

        public Guid OrganizationId { get; init; }
        public PolicyType Type { get; init; }
        public Dictionary<string, object> Data { get; init; } = new();
        public bool Enabled { get; init; }
        public bool CanToggleState { get; init; }

        // Inherited from ResponseModel; System.Text.Json reflection places inherited base
        // members after the derived type's own declared members.
        public string Object => "policy";
    }
}
