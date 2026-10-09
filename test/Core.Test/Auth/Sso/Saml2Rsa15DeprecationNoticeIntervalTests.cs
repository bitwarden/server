using System.Globalization;
using System.Text;
using Bit.Core.Auth.Sso;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.Sso;

[SutProviderCustomize]
public class Saml2Rsa15DeprecationNoticeIntervalTests
{
    private static SutProvider<Saml2Rsa15DeprecationNoticeInterval> BuildSut(int intervalInDays)
    {
        var globalSettings = Substitute.For<IGlobalSettings>();
        var ssoSettings = Substitute.For<ISsoSettings>();
        ssoSettings.Rsa15DeprecationEmailIntervalInDays = intervalInDays;
        globalSettings.Sso.Returns(ssoSettings);

        return new SutProvider<Saml2Rsa15DeprecationNoticeInterval>()
            .SetDependency(globalSettings)
            .WithFakeTimeProvider()
            .Create();
    }

    private static byte[] Encode(long ticks) =>
        Encoding.UTF8.GetBytes(ticks.ToString(CultureInfo.InvariantCulture));

    private static void SetStoredValue(
        SutProvider<Saml2Rsa15DeprecationNoticeInterval> sutProvider, string key, byte[]? value) =>
        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(key, Arg.Any<CancellationToken>())
            .Returns(value);

    private static string KeyFor(Guid organizationId) => $"sso:saml2:rsa15-deprecation-email:{organizationId}";

    [Theory, BitAutoData]
    public async Task TryClaimIntervalAsync_NoEntry_ReturnsTrueAndRecordsSendTime(Guid organizationId)
    {
        var sutProvider = BuildSut(14);
        var nowTicks = sutProvider.GetDependency<FakeTimeProvider>().GetUtcNow().UtcTicks;
        SetStoredValue(sutProvider, KeyFor(organizationId), null);

        var result = await sutProvider.Sut.TryClaimIntervalAsync(organizationId);

        Assert.True(result);
        await sutProvider.GetDependency<IDistributedCache>().Received(1).SetAsync(
            KeyFor(organizationId),
            Arg.Is<byte[]>(b => b.SequenceEqual(Encode(nowTicks))),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Theory, BitAutoData]
    public async Task TryClaimIntervalAsync_EntryWithinInterval_ReturnsFalseAndDoesNotWrite(Guid organizationId)
    {
        var sutProvider = BuildSut(14);
        var now = sutProvider.GetDependency<FakeTimeProvider>().GetUtcNow();
        SetStoredValue(sutProvider, KeyFor(organizationId), Encode(now.AddDays(-13).UtcTicks));

        var result = await sutProvider.Sut.TryClaimIntervalAsync(organizationId);

        Assert.False(result);
        await sutProvider.GetDependency<IDistributedCache>().DidNotReceiveWithAnyArgs().SetAsync(
            default!, default!, default!, default);
    }

    [Theory, BitAutoData]
    public async Task TryClaimIntervalAsync_EntryOlderThanInterval_ReturnsTrueAndOverwrites(Guid organizationId)
    {
        var sutProvider = BuildSut(14);
        var now = sutProvider.GetDependency<FakeTimeProvider>().GetUtcNow();
        SetStoredValue(sutProvider, KeyFor(organizationId), Encode(now.AddDays(-15).UtcTicks));

        var result = await sutProvider.Sut.TryClaimIntervalAsync(organizationId);

        Assert.True(result);
        await sutProvider.GetDependency<IDistributedCache>().Received(1).SetAsync(
            KeyFor(organizationId),
            Arg.Is<byte[]>(b => b.SequenceEqual(Encode(now.UtcTicks))),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Theory, BitAutoData]
    public async Task TryClaimIntervalAsync_IntervalShortenedBelowElapsedTime_ReturnsTrue(Guid organizationId)
    {
        var sutProvider = BuildSut(7);
        var now = sutProvider.GetDependency<FakeTimeProvider>().GetUtcNow();
        SetStoredValue(sutProvider, KeyFor(organizationId), Encode(now.AddDays(-10).UtcTicks));

        var result = await sutProvider.Sut.TryClaimIntervalAsync(organizationId);

        Assert.True(result);
        await sutProvider.GetDependency<IDistributedCache>().Received(1).SetAsync(
            KeyFor(organizationId),
            Arg.Is<byte[]>(b => b.SequenceEqual(Encode(now.UtcTicks))),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task TryClaimIntervalAsync_IntervalZeroOrLess_ReturnsFalseWithoutCacheCall(int intervalInDays)
    {
        var sutProvider = BuildSut(intervalInDays);

        var result = await sutProvider.Sut.TryClaimIntervalAsync(Guid.NewGuid());

        Assert.False(result);
        var cache = sutProvider.GetDependency<IDistributedCache>();
        await cache.DidNotReceiveWithAnyArgs().GetAsync(default!, default);
        await cache.DidNotReceiveWithAnyArgs().SetAsync(default!, default!, default!, default);
    }

    [Theory, BitAutoData]
    public async Task TryClaimIntervalAsync_UnparseableEntry_ReturnsTrue(Guid organizationId)
    {
        var sutProvider = BuildSut(14);
        SetStoredValue(sutProvider, KeyFor(organizationId), Encoding.UTF8.GetBytes("not-a-number"));

        var result = await sutProvider.Sut.TryClaimIntervalAsync(organizationId);

        Assert.True(result);
        await sutProvider.GetDependency<IDistributedCache>().Received(1).SetAsync(
            KeyFor(organizationId),
            Arg.Any<byte[]>(),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(14)]
    [InlineData(7)]
    public async Task TryClaimIntervalAsync_WritesExpiryEqualToIntervalAndKeyWithOrganizationId(int intervalInDays)
    {
        var organizationId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var sutProvider = BuildSut(intervalInDays);

        await sutProvider.Sut.TryClaimIntervalAsync(organizationId);

        await sutProvider.GetDependency<IDistributedCache>().Received(1).SetAsync(
            "sso:saml2:rsa15-deprecation-email:00000000-0000-0000-0000-000000000001",
            Arg.Any<byte[]>(),
            Arg.Is<DistributedCacheEntryOptions>(o =>
                o.AbsoluteExpirationRelativeToNow == TimeSpan.FromDays(intervalInDays)),
            Arg.Any<CancellationToken>());
    }
}
