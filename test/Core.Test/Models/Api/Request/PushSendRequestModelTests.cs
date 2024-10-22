#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Xunit;

namespace Bit.Core.Test.Models.Api.Request;

public class PushSendRequestModelTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData(null, "")]
    [InlineData(null, " ")]
    [InlineData("", null)]
    [InlineData(" ", null)]
    [InlineData("", "")]
    [InlineData(" ", " ")]
    public void Validate_UserIdOrganizationIdNullOrEmpty_Invalid(string? userId, string? organizationId)
    {
        var model = new PushSendRequestModel
        {
            UserId = userId,
            OrganizationId = organizationId,
            Type = PushType.SyncCiphers,
            Payload = "test"
        };

        var results = Validate(model);

        Assert.Single(results);
        Assert.Contains(results, result => result.ErrorMessage == "UserId or OrganizationId is required.");
    }

    [Fact]
    public void Validate_RequiredPayloadFieldNotProvided_Invalid()
    {
        var model = new PushSendRequestModel
        {
            UserId = Guid.NewGuid().ToString(),
            OrganizationId = Guid.NewGuid().ToString(),
            Type = PushType.SyncCiphers
        };

        var results = Validate(model);

        Assert.Single(results);
        Assert.Contains(results, result => result.ErrorMessage == "The Payload field is required.");
    }

    [Fact]
    public void Validate_AllFieldsPresent_Valid()
    {
        var model = new PushSendRequestModel
        {
            UserId = Guid.NewGuid().ToString(),
            OrganizationId = Guid.NewGuid().ToString(),
            Type = PushType.SyncCiphers,
            Payload = "test payload",
            Identifier = Guid.NewGuid().ToString(),
            ClientType = ClientType.All,
            DeviceId = Guid.NewGuid().ToString()
        };

        var results = Validate(model);

        Assert.Empty(results);
    }

    private static List<ValidationResult> Validate(PushSendRequestModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results);
        return results;
    }
}
