using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.Exceptions;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Utilities;

public class PolicyDataValidatorTests
{
    [Fact]
    public void ValidateAndSerialize_NullData_ReturnsNull()
    {
        var result = PolicyDataValidator.ValidateAndSerialize(null, PolicyType.MasterPassword);

        Assert.Null(result);
    }

    [Fact]
    public void ValidateAndSerialize_ValidData_ReturnsSerializedJson()
    {
        var data = new Dictionary<string, object>
        {
            { "minLength", 12 },
            { "minComplexity", 4 }
        };

        var result = PolicyDataValidator.ValidateAndSerialize(data, PolicyType.MasterPassword);

        Assert.NotNull(result);
        Assert.Contains("\"minLength\":12", result);
        Assert.Contains("\"minComplexity\":4", result);
    }

    [Fact]
    public void ValidateAndSerialize_InvalidDataType_ThrowsBadRequestException()
    {
        var data = new Dictionary<string, object> { { "minLength", "not a number" } };

        var exception = Assert.Throws<BadRequestException>(() =>
            PolicyDataValidator.ValidateAndSerialize(data, PolicyType.MasterPassword));

        Assert.Contains("Invalid data for MasterPassword policy", exception.Message);
        Assert.Contains("minLength", exception.Message);
    }

    [Fact]
    public void ValidateAndDeserializeMetadata_NullMetadata_ReturnsEmptyMetadataModel()
    {
        var result = PolicyDataValidator.ValidateAndDeserializeMetadata(null, PolicyType.SingleOrg);

        Assert.IsType<EmptyMetadataModel>(result);
    }

    [Fact]
    public void ValidateAndDeserializeMetadata_ValidMetadata_ReturnsModel()
    {
        var metadata = new Dictionary<string, object> { { "defaultUserCollectionName", "collection name" } };

        var result = PolicyDataValidator.ValidateAndDeserializeMetadata(metadata, PolicyType.OrganizationDataOwnership);

        Assert.IsType<OrganizationModelOwnershipPolicyModel>(result);
    }

    [Fact]
    public void ValidateAndSerialize_ExcessiveMinLength_ThrowsBadRequestException()
    {
        var data = new Dictionary<string, object> { { "minLength", 129 } };

        var exception = Assert.Throws<BadRequestException>(() =>
            PolicyDataValidator.ValidateAndSerialize(data, PolicyType.MasterPassword));

        Assert.Contains("Invalid data for MasterPassword policy", exception.Message);
    }

    [Fact]
    public void ValidateAndSerialize_ExcessiveMinComplexity_ThrowsBadRequestException()
    {
        var data = new Dictionary<string, object> { { "minComplexity", 5 } };

        var exception = Assert.Throws<BadRequestException>(() =>
            PolicyDataValidator.ValidateAndSerialize(data, PolicyType.MasterPassword));

        Assert.Contains("Invalid data for MasterPassword policy", exception.Message);
    }

    [Fact]
    public void ValidateAndSerialize_MinLengthAtMinimum_Succeeds()
    {
        var data = new Dictionary<string, object> { { "minLength", 12 } };

        var result = PolicyDataValidator.ValidateAndSerialize(data, PolicyType.MasterPassword);

        Assert.NotNull(result);
        Assert.Contains("\"minLength\":12", result);
    }

    [Fact]
    public void ValidateAndSerialize_MinLengthAtMaximum_Succeeds()
    {
        var data = new Dictionary<string, object> { { "minLength", 128 } };

        var result = PolicyDataValidator.ValidateAndSerialize(data, PolicyType.MasterPassword);

        Assert.NotNull(result);
        Assert.Contains("\"minLength\":128", result);
    }

    [Fact]
    public void ValidateAndSerialize_MinLengthBelowMinimum_ThrowsBadRequestException()
    {
        var data = new Dictionary<string, object> { { "minLength", 11 } };

        var exception = Assert.Throws<BadRequestException>(() =>
            PolicyDataValidator.ValidateAndSerialize(data, PolicyType.MasterPassword));

        Assert.Contains("Invalid data for MasterPassword policy", exception.Message);
    }

    [Fact]
    public void ValidateAndSerialize_MinComplexityAtMinimum_Succeeds()
    {
        var data = new Dictionary<string, object> { { "minComplexity", 0 } };

        var result = PolicyDataValidator.ValidateAndSerialize(data, PolicyType.MasterPassword);

        Assert.NotNull(result);
        Assert.Contains("\"minComplexity\":0", result);
    }

    [Fact]
    public void ValidateAndSerialize_MinComplexityAtMaximum_Succeeds()
    {
        var data = new Dictionary<string, object> { { "minComplexity", 4 } };

        var result = PolicyDataValidator.ValidateAndSerialize(data, PolicyType.MasterPassword);

        Assert.NotNull(result);
        Assert.Contains("\"minComplexity\":4", result);
    }

    [Fact]
    public void ValidateAndSerialize_MinComplexityBelowMinimum_ThrowsBadRequestException()
    {
        var data = new Dictionary<string, object> { { "minComplexity", -1 } };

        var exception = Assert.Throws<BadRequestException>(() =>
            PolicyDataValidator.ValidateAndSerialize(data, PolicyType.MasterPassword));

        Assert.Contains("Invalid data for MasterPassword policy", exception.Message);
    }

    [Fact]
    public void ValidateAndSerialize_NullMinLength_Succeeds()
    {
        var data = new Dictionary<string, object>
        {
            { "minComplexity", 2 }
            // minLength is omitted, should be null
        };

        var result = PolicyDataValidator.ValidateAndSerialize(data, PolicyType.MasterPassword);

        Assert.NotNull(result);
        Assert.Contains("\"minComplexity\":2", result);
    }

    [Fact]
    public void ValidateAndSerialize_MultipleInvalidFields_ThrowsBadRequestException()
    {
        var data = new Dictionary<string, object>
        {
            { "minLength", 200 },
            { "minComplexity", 10 }
        };

        var exception = Assert.Throws<BadRequestException>(() =>
            PolicyDataValidator.ValidateAndSerialize(data, PolicyType.MasterPassword));

        Assert.Contains("Invalid data for MasterPassword policy", exception.Message);
    }
}
