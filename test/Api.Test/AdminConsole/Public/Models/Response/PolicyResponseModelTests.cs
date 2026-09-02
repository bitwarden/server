using System.Text.Json;
using Bit.Api.AdminConsole.Public.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Public.Models.Response;

public class PolicyResponseModelTests
{
    public static IEnumerable<object[]> PolicyDataByType => new List<object[]>
    {
        new object[] { PolicyType.TwoFactorAuthentication, null },
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
        new object[] { PolicyType.SingleOrg, null },
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
    };

    [Theory]
    [MemberData(nameof(PolicyDataByType))]
    public void Constructor_SerializesIdenticallyToDictionaryDeserializeApproach(PolicyType type, string? data)
    {
        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Type = type,
            Enabled = true,
            Data = data,
        };

        var dictionaryJson = JsonSerializer.Serialize(new DictionaryDeserializePolicyResponseModel(policy));
        var currentJson = JsonSerializer.Serialize(new PolicyResponseModel(policy));

        Assert.Equal(dictionaryJson, currentJson);
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
    /// A <see cref="PolicyResponseModel"/> equivalent that deserializes <see cref="Policy.Data"/> into a
    /// <see cref="Dictionary{TKey,TValue}"/> before serializing, used as a baseline for JSON output comparison.
    /// </summary>
    private class DictionaryDeserializePolicyResponseModel
    {
        public DictionaryDeserializePolicyResponseModel(Policy policy)
        {
            Id = policy.Id;
            Type = policy.Type;
            Enabled = policy.Enabled;
            if (!string.IsNullOrWhiteSpace(policy.Data))
            {
                Data = JsonSerializer.Deserialize<Dictionary<string, object>>(policy.Data);
            }
        }

        public string Object => "policy";
        public Guid Id { get; set; }
        public PolicyType? Type { get; set; }
        public Dictionary<string, object> Data { get; set; }

        public bool? Enabled { get; set; }
    }
}
