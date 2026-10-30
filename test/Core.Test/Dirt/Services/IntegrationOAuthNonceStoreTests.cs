using System.Text;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Services.Implementations;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Dirt.Services;

[SutProviderCustomize]
public class IntegrationOAuthNonceStoreTests
{
    private readonly IntegrationOAuthNonceStore _sut = new(
        new MemoryDistributedCache(new OptionsWrapper<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions())));

    [Theory, BitAutoData]
    public async Task IssueAsync_StoresNonceWithStateLifetime(
        SutProvider<IntegrationOAuthNonceStore> sutProvider,
        Guid integrationId)
    {
        var nonce = await sutProvider.Sut.IssueAsync(integrationId);

        Assert.False(string.IsNullOrEmpty(nonce));
        await sutProvider.GetDependency<IDistributedCache>()
            .Received(1)
            .SetAsync(
                $"IntegrationOAuthNonce_{integrationId:N}",
                Arg.Is<byte[]>(b => Encoding.UTF8.GetString(b) == nonce),
                Arg.Is<DistributedCacheEntryOptions>(o =>
                    o.AbsoluteExpirationRelativeToNow == IntegrationOAuthStateTokenable.Lifetime),
                Arg.Any<CancellationToken>());
    }

    [Theory, BitAutoData]
    public async Task IssueAsync_GeneratesUniqueNonces(Guid integrationId)
    {
        var first = await _sut.IssueAsync(integrationId);
        var second = await _sut.IssueAsync(integrationId);

        Assert.NotEqual(first, second);
    }

    [Theory, BitAutoData]
    public async Task TryConsumeAsync_ActiveNonce_ReturnsTrueOnce(Guid integrationId)
    {
        var nonce = await _sut.IssueAsync(integrationId);

        Assert.True(await _sut.TryConsumeAsync(integrationId, nonce));
        Assert.False(await _sut.TryConsumeAsync(integrationId, nonce));
    }

    [Theory, BitAutoData]
    public async Task TryConsumeAsync_NonceReplacedByNewerIssue_ReturnsFalse(Guid integrationId)
    {
        var staleNonce = await _sut.IssueAsync(integrationId);
        var activeNonce = await _sut.IssueAsync(integrationId);

        Assert.False(await _sut.TryConsumeAsync(integrationId, staleNonce));
        Assert.True(await _sut.TryConsumeAsync(integrationId, activeNonce));
    }

    [Theory, BitAutoData]
    public async Task TryConsumeAsync_WrongNonce_DoesNotConsumeActiveNonce(Guid integrationId)
    {
        var activeNonce = await _sut.IssueAsync(integrationId);

        Assert.False(await _sut.TryConsumeAsync(integrationId, "not-the-active-nonce"));
        Assert.True(await _sut.TryConsumeAsync(integrationId, activeNonce));
    }

    [Theory, BitAutoData]
    public async Task TryConsumeAsync_NonceForDifferentIntegration_ReturnsFalse(
        Guid integrationId,
        Guid otherIntegrationId)
    {
        var nonce = await _sut.IssueAsync(integrationId);

        Assert.False(await _sut.TryConsumeAsync(otherIntegrationId, nonce));
        Assert.True(await _sut.TryConsumeAsync(integrationId, nonce));
    }

    [Theory, BitAutoData]
    public async Task TryConsumeAsync_NoActiveNonce_ReturnsFalse(Guid integrationId, string nonce)
    {
        Assert.False(await _sut.TryConsumeAsync(integrationId, nonce));
    }

    [Theory]
    [BitAutoData("")]
    [BitAutoData((string)null)]
    public async Task TryConsumeAsync_EmptyNonce_ReturnsFalse(string nonce, Guid integrationId)
    {
        await _sut.IssueAsync(integrationId);

        Assert.False(await _sut.TryConsumeAsync(integrationId, nonce));
    }
}
