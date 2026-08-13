using System.Text.Json;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Response;

public class PolicyResponseModelTests
{
    public static IEnumerable<object[]> PolicyDataByType => new List<object[]>
    {
        new object[] { PolicyType.TwoFactorAuthentication, null },
        new object[] { PolicyType.MasterPassword, "{\"minComplexity\":3,\"minLength\":14,\"requireLower\":true,\"requireUpper\":true,\"requireNumbers\":false,\"requireSpecial\":false,\"enforceOnLogin\":true}" },
        new object[] { PolicyType.PasswordGenerator, "{\"defaultType\":\"password\",\"minLength\":16}" },
        new object[] { PolicyType.SingleOrg, null },
        new object[] { PolicyType.SendOptions, "{\"disableHideEmail\":true}" },
        new object[] { PolicyType.ResetPassword, "{\"autoEnrollEnabled\":true}" },
        new object[] { PolicyType.MaximumVaultTimeout, "{\"minutes\":120}" },
        new object[] { PolicyType.SendControls, "{\"allowedAccessControl\":[\"email\",\"password\"],\"disableHideEmail\":false}" },
    };

    [Theory]
    [MemberData(nameof(PolicyDataByType))]
    public void Constructor_SerializesIdenticallyToLegacyDictionaryApproach(PolicyType type, string? data)
    {
        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Type = type,
            Enabled = true,
            Data = data,
            RevisionDate = DateTime.UtcNow,
        };

        var legacyJson = JsonSerializer.Serialize(new LegacyPolicyResponseModel(policy));
        var currentJson = JsonSerializer.Serialize(new PolicyResponseModel(policy));

        Assert.Equal(legacyJson, currentJson);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceData_SerializesDataAsNull(string? data)
    {
        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Type = PolicyType.SingleOrg,
            Enabled = true,
            Data = data,
        };

        var model = new PolicyResponseModel(policy);

        Assert.Null(model.Data);
        Assert.Contains("\"Data\":null", JsonSerializer.Serialize(model));
    }

    [Fact]
    public void Constructor_NullPolicy_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new PolicyResponseModel(null));
    }

    /// <summary>
    /// Mirrors the pre-refactor implementation (Dictionary&lt;string, object&gt; deserialize/serialize round trip)
    /// so we can prove the raw string pass-through produces byte-for-byte identical JSON.
    /// </summary>
    private class LegacyPolicyResponseModel
    {
        public LegacyPolicyResponseModel(Policy policy)
        {
            Id = policy.Id;
            OrganizationId = policy.OrganizationId;
            Type = policy.Type;
            Enabled = policy.Enabled;
            if (!string.IsNullOrWhiteSpace(policy.Data))
            {
                Data = JsonSerializer.Deserialize<Dictionary<string, object>>(policy.Data);
            }
            RevisionDate = policy.RevisionDate;
        }

        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public PolicyType Type { get; set; }
        public Dictionary<string, object> Data { get; set; }
        public bool Enabled { get; set; }
        public DateTime RevisionDate { get; set; }

        // Inherited from ResponseModel; System.Text.Json reflection places inherited base
        // members after the derived type's own declared members.
        public string Object => "policy";
    }
}
