using Bit.Core;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

[SutProviderCustomize]
public class EmailTokenProviderTests
{
    private readonly IDistributedCache _cache;

    public EmailTokenProviderTests()
    {
        _cache = Substitute.For<IDistributedCache>();
    }

    [Theory]
    [BitAutoData]
    public async Task GenerateAsync_GeneratesSixDigitToken_WhenFeatureFlagIsEnabled(User user)
    {
        // Arrange
        var purpose = "test-purpose";
        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.Otp6Digits).Returns(true);
        var tokenProvider = new EmailTokenProvider(_cache, featureService);

        // Act
        var code = await tokenProvider.GenerateAsync(purpose, SubstituteUserManager(), user);

        // Assert
        Assert.Equal(6, code.Length);
    }

    [Theory]
    [BitAutoData]
    public async Task GenerateAsync_GeneratesEightDigitToken_WhenFeatureFlagIsDisabled(User user)
    {
        // Arrange
        var purpose = "test-purpose";
        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.Otp6Digits).Returns(false);
        var tokenProvider = new EmailTokenProvider(_cache, featureService);
        // Act
        var code = await tokenProvider.GenerateAsync(purpose, SubstituteUserManager(), user);

        // Assert
        Assert.Equal(8, code.Length);
    }

    protected static UserManager<User> SubstituteUserManager()
    {
        return new UserManager<User>(Substitute.For<IUserStore<User>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<User>>(),
            Enumerable.Empty<IUserValidator<User>>(),
            Enumerable.Empty<IPasswordValidator<User>>(),
            Substitute.For<ILookupNormalizer>(),
            Substitute.For<IdentityErrorDescriber>(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<User>>>());
    }
}
