﻿#nullable enable
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture.Attributes;
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

    [Theory]
    [BitAutoData("Payload")]
    [BitAutoData("Type")]
    public void Validate_RequiredFieldNotProvided_Invalid(string requiredField)
    {
        var model = new PushSendRequestModel
        {
            UserId = Guid.NewGuid().ToString(),
            OrganizationId = Guid.NewGuid().ToString(),
            Type = PushType.SyncCiphers,
            Payload = "test"
        };

        var dictionary = new Dictionary<string, object?>();
        foreach (var property in model.GetType().GetProperties())
        {
            if (property.Name == requiredField)
            {
                continue;
            }

            dictionary[property.Name] = property.GetValue(model);
        }

        var serialized = JsonSerializer.Serialize(dictionary, JsonHelpers.IgnoreWritingNull);
        var jsonException =
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PushSendRequestModel>(serialized));
        Assert.Contains($"missing required properties, including the following: {requiredField}",
            jsonException.Message);
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
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }
}