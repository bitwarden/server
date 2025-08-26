
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
        var model = new SavePolicyRequest
        {
            Policy = new PolicyRequestModel
            {
                Type = PolicyType.TwoFactorAuthentication,
                Enabled = true,
                Data = testData
            },
            Metadata = new Dictionary<string, object>()
        };

        // Act
        var result = await model.ToSavePolicyModelAsync(organizationId, currentContext);

        // Assert
        Assert.Equal(PolicyType.TwoFactorAuthentication, result.PolicyUpdate.Type);
        Assert.Equal(organizationId, result.PolicyUpdate.OrganizationId);
        Assert.True(result.PolicyUpdate.Enabled);
        Assert.NotNull(result.PolicyUpdate.Data);

        var deserializedData = JsonSerializer.Deserialize<Dictionary<string, object>>(result.PolicyUpdate.Data);
        Assert.Equal("value", deserializedData["test"].ToString());

        Assert.Equal(userId, result!.PerformedBy.UserId);
        Assert.False(result!.PerformedBy.IsOrganizationOwnerOrProvider);

        Assert.IsType<EmptyMetadataModel>(result.Metadata);
    }

    [Theory, BitAutoData]
    public async Task ToSavePolicyModelAsync_WithNullData_HandlesCorrectly(
        Guid organizationId,
        Guid userId)
    {
        // Arrange
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(userId);
        currentContext.OrganizationOwner(organizationId).Returns(false);

        var model = new SavePolicyRequest
        {
            Policy = new PolicyRequestModel
            {
                Type = PolicyType.SingleOrg,
                Enabled = false,
                Data = null
            },
            Metadata = null
        };

        // Act
        var result = await model.ToSavePolicyModelAsync(organizationId, currentContext);

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

        var model = new SavePolicyRequest
        {
            Policy = new PolicyRequestModel
            {
                Type = PolicyType.SingleOrg,
                Enabled = false,
                Data = null
            },
            Metadata = null
        };

        // Act
        var result = await model.ToSavePolicyModelAsync(organizationId, currentContext);

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

        var model = new SavePolicyRequest
        {
            Policy = new PolicyRequestModel
            {
                Type = PolicyType.OrganizationDataOwnership,
                Enabled = true,
                Data = null
            },
            Metadata = new Dictionary<string, object>
            {
                { "defaultUserCollectionName", defaultCollectionName }
            }
        };

        // Act
        var result = await model.ToSavePolicyModelAsync(organizationId, currentContext);

        // Assert
        Assert.IsType<OrganizationModelOwnershipPolicyModel>(result.Metadata);
        var metadata = (OrganizationModelOwnershipPolicyModel)result.Metadata;
        Assert.Equal(defaultCollectionName, metadata.DefaultUserCollectionName);
    }

    // Jimmy todo: fix test
    // [Theory, BitAutoData]
    // public async Task ToSavePolicyModelAsync_OrganizationDataOwnership_WithInvalidMetadata_ReturnsEmptyMetadata(
    //     Guid organizationId,
    //     Guid userId)
    // {
    //     // Arrange
    //     var currentContext = Substitute.For<ICurrentContext>();
    //     currentContext.UserId.Returns(userId);
    //     currentContext.OrganizationOwner(organizationId).Returns(true);
    //
    //     var model = new SavePolicyRequest
    //     {
    //         Policy = new PolicyRequestModel
    //         {
    //             Type = PolicyType.OrganizationDataOwnership,
    //             Enabled = true,
    //             Data = null
    //         },
    //         Metadata = new Dictionary<string, object>
    //         {
    //             { "invalidProperty", "value" }
    //         }
    //     };
    //
    //     // Act
    //     var result = await model.ToSavePolicyModelAsync(organizationId, currentContext);
    //
    //     // Assert
    //     Assert.NotNull(result);
    //     Assert.IsType<EmptyMetadataModel>(result.Metadata);
    // }

    [Theory, BitAutoData]
    public async Task ToSavePolicyModelAsync_OrganizationDataOwnership_WithNullMetadata_ReturnsEmptyMetadata(
        Guid organizationId,
        Guid userId)
    {
        // Arrange
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(userId);
        currentContext.OrganizationOwner(organizationId).Returns(true);

        var model = new SavePolicyRequest
        {
            Policy = new PolicyRequestModel
            {
                Type = PolicyType.OrganizationDataOwnership,
                Enabled = true,
                Data = null
            },
            Metadata = null
        };

        // Act
        var result = await model.ToSavePolicyModelAsync(organizationId, currentContext);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<EmptyMetadataModel>(result.Metadata);
    }

    [Theory, BitAutoData]
    public async Task ToSavePolicyModelAsync_ComplexData_SerializesCorrectly(
        Guid organizationId,
        Guid userId)
    {
        // Arrange
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.UserId.Returns(userId);
        currentContext.OrganizationOwner(organizationId).Returns(true);

        var complexData = new Dictionary<string, object>
        {
            { "stringValue", "test" },
            { "numberValue", 42 },
            { "boolValue", true },
            { "arrayValue", new[] { "item1", "item2" } },
            { "nestedObject", new Dictionary<string, object> { { "nested", "value" } } }
        };

        var model = new SavePolicyRequest
        {
            Policy = new PolicyRequestModel
            {
                Type = PolicyType.ResetPassword,
                Enabled = true,
                Data = complexData
            },
            Metadata = new Dictionary<string, object>()
        };

        // Act
        var result = await model.ToSavePolicyModelAsync(organizationId, currentContext);

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

        var model = new SavePolicyRequest
        {
            Policy = new PolicyRequestModel
            {
                Type = PolicyType.MaximumVaultTimeout,
                Enabled = true,
                Data = null
            },
            Metadata = new Dictionary<string, object>
            {
                { "someProperty", "someValue" }
            }
        };

        // Act
        var result = await model.ToSavePolicyModelAsync(organizationId, currentContext);

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

        var model = new SavePolicyRequest
        {
            Policy = new PolicyRequestModel
            {
                Type = PolicyType.OrganizationDataOwnership,
                Enabled = true,
                Data = null
            },
            Metadata = errorDictionary
        };

        // Act
        var result = await model.ToSavePolicyModelAsync(organizationId, currentContext);

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
