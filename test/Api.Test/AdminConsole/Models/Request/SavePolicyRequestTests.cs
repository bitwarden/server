
using System.Text.Json;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.Context;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Request;

[SutProviderCustomize]
public class SavePolicyRequestTests
{
    [Theory, BitAutoData]
    public async Task ToSavePolicyModelAsync_WithValidData_ReturnsCorrectSavePolicyModel(
        Guid organizationId,
        Guid userId)
    {
        // Arrange
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(userId);
        currentContext.OrganizationOwner(organizationId).Returns(true);

        var testData = new Dictionary<string, object> { { "test", "value" } };
        var policyType = PolicyType.TwoFactorAuthentication;
        var model = new SavePolicyRequest
        {
            Policy = new PolicyRequestModel
            {
                Enabled = true,
                Data = testData
            },
            Metadata = new Dictionary<string, object>()
        };

        // Act
        var result = await model.ToSavePolicyModelAsync(organizationId, policyType, currentContext);

        // Assert
        Assert.Equal(PolicyType.TwoFactorAuthentication, result.PolicyUpdate.Type);
        Assert.Equal(organizationId, result.PolicyUpdate.OrganizationId);
        Assert.True(result.PolicyUpdate.Enabled);
        Assert.NotNull(result.PolicyUpdate.Data);

        var deserializedData = JsonSerializer.Deserialize<Dictionary<string, object>>(result.PolicyUpdate.Data);
        Assert.Equal("value", deserializedData["test"].ToString());

        Assert.Equal(userId, result!.PerformedBy.UserId);
        Assert.True(result!.PerformedBy.IsOrganizationOwnerOrProvider);

        Assert.IsType<EmptyMetadataModel>(result.Metadata);
    }

    [Theory, BitAutoData]
    public async Task ToSavePolicyModelAsync_WithEmptyData_HandlesCorrectly(
        Guid organizationId,
        Guid userId)
    {
        // Arrange
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(userId);
        currentContext.OrganizationOwner(organizationId).Returns(false);

        var policyType = PolicyType.SingleOrg;
        var model = new SavePolicyRequest
        {
            Policy = new PolicyRequestModel
            {
                Enabled = false
            }
        };

        // Act
        var result = await model.ToSavePolicyModelAsync(organizationId, policyType, currentContext);

        // Assert
        Assert.Null(result.PolicyUpdate.Data);
        Assert.False(result.PolicyUpdate.Enabled);

        Assert.Equal(userId, result!.PerformedBy.UserId);
        Assert.False(result!.PerformedBy.IsOrganizationOwnerOrProvider);
    }

    [Theory, BitAutoData]
    public async Task ToSavePolicyModelAsync_WithNonOrganizationOwner_HandlesCorrectly(
        Guid organizationId,
        Guid userId)
    {
        // Arrange
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(userId);
        currentContext.OrganizationOwner(organizationId).Returns(true);

        var policyType = PolicyType.SingleOrg;
        var model = new SavePolicyRequest
        {
            Policy = new PolicyRequestModel
            {
                Enabled = false
            }
        };

        // Act
        var result = await model.ToSavePolicyModelAsync(organizationId, policyType, currentContext);

        // Assert
        Assert.Null(result.PolicyUpdate.Data);
        Assert.False(result.PolicyUpdate.Enabled);

        Assert.Equal(userId, result!.PerformedBy.UserId);
        Assert.True(result!.PerformedBy.IsOrganizationOwnerOrProvider);
    }

    [Theory, BitAutoData]
    public async Task ToSavePolicyModelAsync_OrganizationDataOwnership_WithValidMetadata_ReturnsCorrectMetadata(
        Guid organizationId,
        Guid userId,
        string defaultCollectionName)
    {
        // Arrange
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(userId);
        currentContext.OrganizationOwner(organizationId).Returns(true);

        var policyType = PolicyType.OrganizationDataOwnership;
        var model = new SavePolicyRequest
        {
            Policy = new PolicyRequestModel
            {
                Enabled = true
            },
            Metadata = new Dictionary<string, object>
            {
                { "defaultUserCollectionName", defaultCollectionName }
            }
        };

        // Act
        var result = await model.ToSavePolicyModelAsync(organizationId, policyType, currentContext);

        // Assert
        Assert.IsType<OrganizationModelOwnershipPolicyModel>(result.Metadata);
        var metadata = (OrganizationModelOwnershipPolicyModel)result.Metadata;
        Assert.Equal(defaultCollectionName, metadata.DefaultUserCollectionName);
    }

    [Theory, BitAutoData]
    public async Task ToSavePolicyModelAsync_OrganizationDataOwnership_WithEmptyMetadata_ReturnsEmptyMetadata(
        Guid organizationId,
        Guid userId)
    {
        // Arrange
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(userId);
        currentContext.OrganizationOwner(organizationId).Returns(true);

        var policyType = PolicyType.OrganizationDataOwnership;
        var model = new SavePolicyRequest
        {
            Policy = new PolicyRequestModel
            {
                Enabled = true
            }
        };

        // Act
        var result = await model.ToSavePolicyModelAsync(organizationId, policyType, currentContext);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<EmptyMetadataModel>(result.Metadata);
    }

    private static readonly Dictionary<string, object> _complexData = new Dictionary<string,
     object>
      {
          { "stringValue", "test" },
          { "numberValue", 42 },
          { "boolValue", true },
          { "arrayValue", new[] { "item1", "item2" } },
          { "nestedObject", new Dictionary<string, object> { { "nested", "value" } } }
      };

    [Theory, BitAutoData]
    public async Task ToSavePolicyModelAsync_ComplexData_SerializesCorrectly(
        Guid organizationId,
        Guid userId)
    {
        // Arrange
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(userId);
        currentContext.OrganizationOwner(organizationId).Returns(true);

        var policyType = PolicyType.ResetPassword;
        var model = new SavePolicyRequest
        {
            Policy = new PolicyRequestModel
            {
                Enabled = true,
                Data = _complexData
            },
            Metadata = new Dictionary<string, object>()
        };

        // Act
        var result = await model.ToSavePolicyModelAsync(organizationId, policyType, currentContext);

        // Assert
        var deserializedData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(result.PolicyUpdate.Data);
        Assert.Equal("test", deserializedData["stringValue"].GetString());
        Assert.Equal(42, deserializedData["numberValue"].GetInt32());
        Assert.True(deserializedData["boolValue"].GetBoolean());
        Assert.Equal(2, deserializedData["arrayValue"].GetArrayLength());
        var array = deserializedData["arrayValue"].EnumerateArray()
            .Select(e => e.GetString())
            .ToArray();
        Assert.Contains("item1", array);
        Assert.Contains("item2", array);
        Assert.True(deserializedData["nestedObject"].TryGetProperty("nested", out var nestedValue));
        Assert.Equal("value", nestedValue.GetString());
    }


    [Theory, BitAutoData]
    public async Task MapToPolicyMetadata_UnknownPolicyType_ReturnsEmptyMetadata(
        Guid organizationId,
        Guid userId)
    {
        // Arrange
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(userId);
        currentContext.OrganizationOwner(organizationId).Returns(true);

        var policyType = PolicyType.MaximumVaultTimeout;
        var model = new SavePolicyRequest
        {
            Policy = new PolicyRequestModel
            {
                Enabled = true
            },
            Metadata = new Dictionary<string, object>
            {
                { "someProperty", "someValue" }
            }
        };

        // Act
        var result = await model.ToSavePolicyModelAsync(organizationId, policyType, currentContext);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<EmptyMetadataModel>(result.Metadata);
    }

    [Theory, BitAutoData]
    public async Task MapToPolicyMetadata_JsonSerializationException_ReturnsEmptyMetadata(
        Guid organizationId,
        Guid userId)
    {
        // Arrange
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(userId);
        currentContext.OrganizationOwner(organizationId).Returns(true);

        var errorDictionary = BuildErrorDictionary();
        var policyType = PolicyType.OrganizationDataOwnership;
        var model = new SavePolicyRequest
        {
            Policy = new PolicyRequestModel
            {
                Enabled = true
            },
            Metadata = errorDictionary
        };

        // Act
        var result = await model.ToSavePolicyModelAsync(organizationId, policyType, currentContext);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<EmptyMetadataModel>(result.Metadata);
    }

    private static Dictionary<string, object> BuildErrorDictionary()
    {
        var circularDict = new Dictionary<string, object>();
        circularDict["self"] = circularDict;
        return circularDict;
    }
}
