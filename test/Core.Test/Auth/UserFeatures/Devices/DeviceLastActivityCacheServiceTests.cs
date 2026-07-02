using Bit.Core.Auth.UserFeatures.Devices;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.Devices;

[SutProviderCustomize]
public class DeviceLastActivityCacheServiceTests
{
    // AbsoluteExpirationRelativeToNow rejects zero, so the IGlobalSettings mock must be
    // registered via SetDependency BEFORE Create() — Create() materializes the SUT eagerly.

    private const int DefaultTtlHours = 120;

    private static SutProvider<DeviceLastActivityCacheService> BuildSut(int ttlHours = DefaultTtlHours)
    {
        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.DeviceLastActivityCacheTtlHours = ttlHours;

        return new SutProvider<DeviceLastActivityCacheService>()
            .SetDependency(globalSettings)
            .Create();
    }

    // --- IsUpToDateAsync ---

    [Theory, BitAutoData]
    public async Task IsUpToDateAsync_GivenCacheHit_ReturnsTrue(
        Guid userId,
        string identifier)
    {
        var sutProvider = BuildSut();
        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new byte[] { 1 });

        var result = await sutProvider.Sut.IsUpToDateAsync(userId, identifier);

        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task IsUpToDateAsync_GivenCacheMiss_ReturnsFalse(
        Guid userId,
        string identifier)
    {
        var sutProvider = BuildSut();
        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((byte[])null);

        var result = await sutProvider.Sut.IsUpToDateAsync(userId, identifier);

        Assert.False(result);
    }

    [Fact]
    public async Task IsUpToDateAsync_UsesCacheKeyFormat()
    {
        var sutProvider = BuildSut();
        var userId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var identifier = "my-device-id";

        await sutProvider.Sut.IsUpToDateAsync(userId, identifier);

        await sutProvider.GetDependency<IDistributedCache>()
            .Received(1)
            .GetAsync(
                "device:last-activity:00000000-0000-0000-0000-000000000001:my-device-id",
                Arg.Any<CancellationToken>());
    }

    // --- RecordUpdateAsync ---

    [Fact]
    public async Task RecordUpdateAsync_UsesCacheKeyFormat()
    {
        var sutProvider = BuildSut();
        var userId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var identifier = "my-device-id";

        await sutProvider.Sut.RecordUpdateAsync(userId, identifier);

        await sutProvider.GetDependency<IDistributedCache>()
            .Received(1)
            .SetAsync(
                "device:last-activity:00000000-0000-0000-0000-000000000001:my-device-id",
                Arg.Any<byte[]>(),
                Arg.Any<DistributedCacheEntryOptions>(),
                Arg.Any<CancellationToken>());
    }

    [Theory, BitAutoData]
    public async Task RecordUpdateAsync_WritesNonEmptySentinelBytes(
        Guid userId,
        string identifier)
    {
        var sutProvider = BuildSut();

        await sutProvider.Sut.RecordUpdateAsync(userId, identifier);

        await sutProvider.GetDependency<IDistributedCache>()
            .Received(1)
            .SetAsync(
                Arg.Any<string>(),
                Arg.Is<byte[]>(b => b != null && b.Length > 0),
                Arg.Any<DistributedCacheEntryOptions>(),
                Arg.Any<CancellationToken>());
    }

    // Parameterized to prove the IGlobalSettings value actually flows into the cache options;
    // a single value could pass by coincidence if the SUT hardcoded the same number.
    [Theory]
    [InlineData(120)]
    [InlineData(24)]
    [InlineData(1)]
    public async Task RecordUpdateAsync_AbsoluteExpirationMatchesGlobalSettingsTtl(int ttlHours)
    {
        var sutProvider = BuildSut(ttlHours);
        var userId = Guid.NewGuid();
        var identifier = "device-id";

        await sutProvider.Sut.RecordUpdateAsync(userId, identifier);

        await sutProvider.GetDependency<IDistributedCache>()
            .Received(1)
            .SetAsync(
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == TimeSpan.FromHours(ttlHours)),
                Arg.Any<CancellationToken>());
    }
}
