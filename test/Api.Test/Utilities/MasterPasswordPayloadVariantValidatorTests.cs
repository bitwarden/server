using Bit.Core.Utilities;
using Xunit;

namespace Bit.Api.Test.Utilities;

public class MasterPasswordPayloadVariantValidatorTests
{
    [Fact]
    public void ValidateExclusivity_WhenOnlyNewVariantPresent_ReturnsNoErrors()
    {
        var results = MasterPasswordPayloadVariantValidator
            .ValidateExclusivity(hasNewPayloads: true, hasLegacyPayloads: false)
            .ToList();

        Assert.Empty(results);
    }

    [Fact]
    public void ValidateExclusivity_WhenOnlyLegacyVariantPresent_ReturnsNoErrors()
    {
        var results = MasterPasswordPayloadVariantValidator
            .ValidateExclusivity(hasNewPayloads: false, hasLegacyPayloads: true)
            .ToList();

        Assert.Empty(results);
    }

    [Fact]
    public void ValidateExclusivity_WhenBothVariantsPresent_ReturnsBothPresentError()
    {
        var results = MasterPasswordPayloadVariantValidator
            .ValidateExclusivity(hasNewPayloads: true, hasLegacyPayloads: true)
            .ToList();

        Assert.Single(results);
        Assert.Equal(
            "Cannot provide both new payloads (UnlockData/AuthenticationData) and legacy payloads (NewMasterPasswordHash/Key).",
            results[0].ErrorMessage);
    }

    [Fact]
    public void ValidateExclusivity_WhenNeitherVariantPresent_ReturnsMissingVariantError()
    {
        var results = MasterPasswordPayloadVariantValidator
            .ValidateExclusivity(hasNewPayloads: false, hasLegacyPayloads: false)
            .ToList();

        Assert.Single(results);
        Assert.Equal(
            "Must provide either new payloads (UnlockData/AuthenticationData) or legacy payloads (NewMasterPasswordHash/Key).",
            results[0].ErrorMessage);
    }

    [Fact]
    public void ValidateExclusivity_ValidationResultIncludesExpectedMemberNames()
    {
        var results = MasterPasswordPayloadVariantValidator
            .ValidateExclusivity(hasNewPayloads: false, hasLegacyPayloads: false)
            .ToList();

        Assert.Single(results);
        Assert.Equal(
            new[] { "AuthenticationData", "UnlockData", "NewMasterPasswordHash", "Key" },
            results[0].MemberNames);
    }
}
