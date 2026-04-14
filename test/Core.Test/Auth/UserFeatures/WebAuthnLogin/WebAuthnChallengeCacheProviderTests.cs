using Bit.Core.Auth.UserFeatures.WebAuthnLogin.Implementations;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.WebAuthnLogin;

[SutProviderCustomize]
public class WebAuthnChallengeCacheProviderTests
{
    [Theory, BitAutoData]
    internal async Task StoreChallengeAsync_WritesCacheEntry(
        SutProvider<WebAuthnChallengeCacheProvider> sutProvider)
    {
        // Arrange
        var challenge = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        // Act
        await sutProvider.Sut.StoreChallengeAsync(challenge);

        // Assert
        var expectedKey = $"WebAuthnLoginAssertion_{CoreHelpers.Base64UrlEncode(challenge)}";
        await sutProvider.GetDependency<IDistributedCache>()
            .Received(1)
            .SetAsync(
                expectedKey,
                Arg.Is<byte[]>(b => b.Length == 1 && b[0] == 1),
                Arg.Is<DistributedCacheEntryOptions>(o =>
                    o.AbsoluteExpirationRelativeToNow == TimeSpan.FromMinutes(17)),
                Arg.Any<CancellationToken>());
    }

    [Theory, BitAutoData]
    internal async Task ConsumeChallengeAsync_CacheHit_RemovesEntryAndReturnsTrue(
        SutProvider<WebAuthnChallengeCacheProvider> sutProvider)
    {
        // Arrange
        var challenge = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var expectedKey = $"WebAuthnLoginAssertion_{CoreHelpers.Base64UrlEncode(challenge)}";

        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(expectedKey, Arg.Any<CancellationToken>())
            .Returns(new byte[] { 1 });

        // Act
        var result = await sutProvider.Sut.ConsumeChallengeAsync(challenge);

        // Assert
        Assert.True(result);
        await sutProvider.GetDependency<IDistributedCache>()
            .Received(1)
            .RemoveAsync(expectedKey, Arg.Any<CancellationToken>());
    }

    [Theory, BitAutoData]
    internal async Task ConsumeChallengeAsync_CacheMiss_ReturnsFalseAndDoesNotRemove(
        SutProvider<WebAuthnChallengeCacheProvider> sutProvider)
    {
        // Arrange
        var challenge = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var expectedKey = $"WebAuthnLoginAssertion_{CoreHelpers.Base64UrlEncode(challenge)}";

        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(expectedKey, Arg.Any<CancellationToken>())
            .Returns((byte[])null);

        // Act
        var result = await sutProvider.Sut.ConsumeChallengeAsync(challenge);

        // Assert
        Assert.False(result);
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
