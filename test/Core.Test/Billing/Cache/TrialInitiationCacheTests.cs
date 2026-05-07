using System.Globalization;
using System.Text;
using Bit.Core.Billing.Cache;
using Bit.Core.Exceptions;
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
    public async Task ValidateTrialLengthAsync_MatchingValue_RemovesEntry(
        string trialInitiationId, int trialLength,
        SutProvider<TrialInitiationCache> sutProvider)
    {
        var cached = Encoding.UTF8.GetBytes(trialLength.ToString(CultureInfo.InvariantCulture));
        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync($"trial-initiation:{trialInitiationId}")
            .Returns(cached);

        await sutProvider.Sut.ValidateTrialLengthAsync(trialInitiationId, trialLength);

        await sutProvider.GetDependency<IDistributedCache>()
            .Received(1)
            .RemoveAsync($"trial-initiation:{trialInitiationId}");
    }

    [Theory, BitAutoData]
    public async Task ValidateTrialLengthAsync_MismatchedValue_ThrowsBadRequestException(
        string trialInitiationId,
        SutProvider<TrialInitiationCache> sutProvider)
    {
        var cached = Encoding.UTF8.GetBytes("14");
        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync($"trial-initiation:{trialInitiationId}")
            .Returns(cached);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ValidateTrialLengthAsync(trialInitiationId, 7));

        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ValidateTrialLengthAsync_CacheMiss_DoesNotThrow(
        string trialInitiationId, int trialLength,
        SutProvider<TrialInitiationCache> sutProvider)
    {
        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync($"trial-initiation:{trialInitiationId}")
            .Returns((byte[])null);

        await sutProvider.Sut.ValidateTrialLengthAsync(trialInitiationId, trialLength);

        await sutProvider.GetDependency<IDistributedCache>()
            .DidNotReceive()
            .RemoveAsync(Arg.Any<string>());
    }
}
