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
        var data = new Dictionary<string, object> { { "minLength", 12 } };

        var result = PolicyDataValidator.ValidateAndSerialize(data, PolicyType.MasterPassword);

        Assert.NotNull(result);
        Assert.Contains("\"minLength\":12", result);
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
        var metadata = new Dictionary<string, object> { { "minLength", 2 } };

        var result = PolicyDataValidator.ValidateAndDeserializeMetadata(metadata, PolicyType.MasterPassword);

        Assert.IsType<OrganizationModelOwnershipPolicyModel>(result);
    }
}
