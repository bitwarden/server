using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.Identity;

public class EmailTwoFactorTokenProviderTests : BaseTwoFactorTokenProviderTests<EmailTwoFactorTokenProvider>
{
    public override TwoFactorProviderType TwoFactorProviderType => TwoFactorProviderType.Email;

    public static IEnumerable<object[]> CanGenerateTwoFactorTokenAsyncData
        => SetupCanGenerateData(
            (
                new Dictionary<string, object>
                {
                    ["Email"] = "test@email.com",
                },
                true
            ),
            (
                new Dictionary<string, object>
                {
                    ["NotEmail"] = "value",
                },
                false
            ),
            (
                new Dictionary<string, object>
                {
                    ["Email"] = "",
                },
                false
            )
        );

    [Theory, BitMemberAutoData(nameof(CanGenerateTwoFactorTokenAsyncData))]
    public override async Task RunCanGenerateTwoFactorTokenAsync(Dictionary<string, object> metaData, bool expectedResponse,
        User user, SutProvider<EmailTwoFactorTokenProvider> sutProvider)
    {
        await base.RunCanGenerateTwoFactorTokenAsync(metaData, expectedResponse, user, sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task GenerateAsync_ShouldReturnSixDigitToken_WithFeatureFlagEnabled(
        User user, SutProvider<EmailTwoFactorTokenProvider> sutProvider)
    {
        // Arrange
        user.TwoFactorProviders = GetTwoFactorEmailProvidersJson();
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.Otp6Digits)
            .Returns(true);

        // Act
        var token = await sutProvider.Sut.GenerateAsync("purpose", SubstituteUserManager(), user);

        // Assert
        Assert.NotNull(token);
        Assert.Equal(6, token.Length);
    }

    [Theory]
    [BitAutoData]
    public async Task GenerateAsync_ShouldReturnSixDigitToken_WithFeatureFlagDisabled(
        User user, SutProvider<EmailTwoFactorTokenProvider> sutProvider)
    {
        // Arrange
        user.TwoFactorProviders = GetTwoFactorEmailProvidersJson();
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.Otp6Digits)
            .Returns(false);

        // Act
        var token = await sutProvider.Sut.GenerateAsync("purpose", SubstituteUserManager(), user);

        // Assert
        Assert.NotNull(token);
        Assert.Equal(6, token.Length);
    }

    private string GetTwoFactorEmailProvidersJson()
    {
        return
            "{\"1\":{\"Enabled\":true,\"MetaData\":{\"Email\":\"test@email.com\"}}}";
    }
}
