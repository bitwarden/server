using System.Globalization;
using System.Text;
using Bit.Core.Billing.Cache;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Billing.Cache;

[SutProviderCustomize]
public class TrialInitiationCacheTests
{
    [Theory, BitAutoData]
    public async Task WriteAsync_StoresValue_InCache(
        string trialInitiationId, int trialLength,
        SutProvider<TrialInitiationCache> sutProvider)
    {
        await sutProvider.Sut.WriteAsync(trialInitiationId, trialLength);

        await sutProvider.GetDependency<IDistributedCache>()
            .Received(1)
            .SetAsync(
                $"trial-initiation:{trialInitiationId}",
                Arg.Any<byte[]>(),
                Arg.Is<DistributedCacheEntryOptions>(o =>
                    o.AbsoluteExpirationRelativeToNow == TimeSpan.FromMinutes(15)));
    }

    [Theory, BitAutoData]
    public async Task GetAndRemoveAsync_CacheHit_ReturnsValueAndRemovesEntry(
        string trialInitiationId, int trialLength,
        SutProvider<TrialInitiationCache> sutProvider)
    {
        var cached = Encoding.UTF8.GetBytes(trialLength.ToString(CultureInfo.InvariantCulture));
        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync($"trial-initiation:{trialInitiationId}")
            .Returns(cached);

        var result = await sutProvider.Sut.GetAndRemoveAsync(trialInitiationId);

        Assert.Equal(trialLength, result);
        await sutProvider.GetDependency<IDistributedCache>()
            .Received(1)
            .RemoveAsync($"trial-initiation:{trialInitiationId}");
    }

    [Theory, BitAutoData]
    public async Task GetAndRemoveAsync_CacheMiss_ReturnsNull(
        string trialInitiationId,
        SutProvider<TrialInitiationCache> sutProvider)
    {
        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync($"trial-initiation:{trialInitiationId}")
            .Returns((byte[])null);

        var result = await sutProvider.Sut.GetAndRemoveAsync(trialInitiationId);

        Assert.Null(result);
        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
    }
}