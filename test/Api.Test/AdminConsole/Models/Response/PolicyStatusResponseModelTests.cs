using System.Text.Json;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Response;

public class PolicyStatusResponseModelTests
{
    public static IEnumerable<object[]> PolicyDataByType => new List<object[]>
    {
        new object[]
        {
            PolicyType.MasterPassword,
            """{"minComplexity":3,"minLength":14,"requireLower":true,"requireUpper":true,"requireNumbers":false,"requireSpecial":false,"enforceOnLogin":true}"""
        },
        new object[]
        {
            PolicyType.PasswordGenerator,
            """{"defaultType":"password","minLength":16}"""
        },
        new object[]
        {
            PolicyType.SendOptions,
            """{"disableHideEmail":true}"""
        },
        new object[]
        {
            PolicyType.ResetPassword,
            """{"autoEnrollEnabled":true}"""
        },
        new object[] { PolicyType.SingleOrg, null },
    };

    [Theory]
    [MemberData(nameof(PolicyDataByType))]
    public void Constructor_SerializesIdenticallyToDictionaryDeserializeApproach(PolicyType type, string? data)
    {
        var organizationId = Guid.NewGuid();
        var policyStatus = new PolicyStatus(organizationId, type)
        {
            Enabled = true,
            Data = data,
        };

        var dictionaryJson = JsonSerializer.Serialize(new DictionaryDeserializePolicyStatusResponseModel(policyStatus));
        var currentJson = JsonSerializer.Serialize(new PolicyStatusResponseModel(policyStatus));

        Assert.Equal(dictionaryJson, currentJson);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceData_SerializesDataAsEmptyObject(string? data)
    {
        var policyStatus = new PolicyStatus(Guid.NewGuid(), PolicyType.SingleOrg)
        {
            Enabled = true,
            Data = data,
        };

        var model = new PolicyStatusResponseModel(policyStatus);
        var json = JsonSerializer.Serialize(model);

        // Serializes as an empty object rather than null to support non-null data expectations.
        Assert.Equal("{}", model.Data);
        Assert.Contains("\"Data\":{}", json);
    }

    /// <summary>
    /// A <see cref="PolicyStatusResponseModel"/> equivalent that deserializes <see cref="PolicyStatus.Data"/> into a
    /// <see cref="Dictionary{TKey,TValue}"/> before serializing, used as a baseline for JSON output comparison.
    /// </summary>
    private class DictionaryDeserializePolicyStatusResponseModel
    {
        public DictionaryDeserializePolicyStatusResponseModel(PolicyStatus policy, bool canToggleState = true)
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

        public string Object => "policy";
    }
}
