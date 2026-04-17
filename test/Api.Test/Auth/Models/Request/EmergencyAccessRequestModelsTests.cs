using System.ComponentModel.DataAnnotations;
using Bit.Api.Auth.Models.Request;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Request;

public class EmergencyAccessInviteRequestModelTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_WaitTimeDays_BelowMinimum_Invalid(int waitTimeDays)
    {
        var model = new EmergencyAccessInviteRequestModel
        {
            Email = "test@example.com",
            Type = EmergencyAccessType.View,
            WaitTimeDays = waitTimeDays,
        };
        var result = Validate(model);
        Assert.Contains(result, r => r.MemberNames.Contains("WaitTimeDays"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(90)]
    [InlineData(short.MaxValue)]
    public void Validate_WaitTimeDays_ValidRange_Valid(int waitTimeDays)
    {
        var model = new EmergencyAccessInviteRequestModel
        {
            Email = "test@example.com",
            Type = EmergencyAccessType.View,
            WaitTimeDays = waitTimeDays,
        };
        var result = Validate(model);
        Assert.DoesNotContain(result, r => r.MemberNames.Contains("WaitTimeDays"));
    }

    private static List<ValidationResult> Validate(EmergencyAccessInviteRequestModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }
}

public class EmergencyAccessUpdateRequestModelTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_WaitTimeDays_BelowMinimum_Invalid(int waitTimeDays)
    {
        var model = new EmergencyAccessUpdateRequestModel
        {
            Type = EmergencyAccessType.View,
            WaitTimeDays = waitTimeDays,
            KeyEncrypted = "",
        };
        var result = Validate(model);
        Assert.Contains(result, r => r.MemberNames.Contains("WaitTimeDays"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(90)]
    [InlineData(short.MaxValue)]
    public void Validate_WaitTimeDays_ValidRange_Valid(int waitTimeDays)
    {
        var model = new EmergencyAccessUpdateRequestModel
        {
            Type = EmergencyAccessType.View,
            WaitTimeDays = waitTimeDays,
            KeyEncrypted = "",
        };
        var result = Validate(model);
        Assert.DoesNotContain(result, r => r.MemberNames.Contains("WaitTimeDays"));
    }

    [Fact]
    public void ToEmergencyAccess_BothKeysPresent_UpdatesKey()
    {
        var model = new EmergencyAccessUpdateRequestModel
        {
            Type = EmergencyAccessType.Takeover,
            WaitTimeDays = 7,
            KeyEncrypted = "new-encrypted-key",
        };
        var existing = new EmergencyAccess { KeyEncrypted = "old-encrypted-key" };

        var result = model.ToEmergencyAccess(existing);

        Assert.Equal("new-encrypted-key", result.KeyEncrypted);
    }

    [Theory]
    [InlineData(null, "new-encrypted-key")]
    [InlineData("", "new-encrypted-key")]
    [InlineData("   ", "new-encrypted-key")]
    [InlineData("old-encrypted-key", null)]
    [InlineData("old-encrypted-key", "")]
    [InlineData("old-encrypted-key", "   ")]
    public void ToEmergencyAccess_EitherKeyMissingOrWhitespace_DoesNotUpdateKey(
        string? existingKey, string? newKey)
    {
        var model = new EmergencyAccessUpdateRequestModel
        {
            Type = EmergencyAccessType.Takeover,
            WaitTimeDays = 7,
            KeyEncrypted = newKey,
        };
        var existing = new EmergencyAccess { KeyEncrypted = existingKey };

        var result = model.ToEmergencyAccess(existing);

        Assert.Equal(existingKey, result.KeyEncrypted);
    }

    [Fact]
    public void ToEmergencyAccess_AlwaysUpdatesTypeAndWaitTimeDays()
    {
        var model = new EmergencyAccessUpdateRequestModel
        {
            Type = EmergencyAccessType.Takeover,
            WaitTimeDays = 14,
            KeyEncrypted = "",
        };
        var existing = new EmergencyAccess
        {
            Type = EmergencyAccessType.View,
            WaitTimeDays = 7,
        };

        var result = model.ToEmergencyAccess(existing);

        Assert.Equal(EmergencyAccessType.Takeover, result.Type);
        Assert.Equal(14, result.WaitTimeDays);
    }

    private static List<ValidationResult> Validate(EmergencyAccessUpdateRequestModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }
}
