using System.Globalization;
using System.Text;
using Bit.Core.Auth.UserFeatures.Devices;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.Devices;

[SutProviderCustomize]
public class DeviceLastActivityCacheServiceTests
{
    // --- IsUpToDateAsync ---

    [Theory, BitAutoData]
    public async Task IsUpToDateAsync_GivenCachedDateAndVersionMatch_ReturnsTrue(
        Guid userId,
        string identifier,
        string clientVersion)
    {
        var sutProvider = new SutProvider<DeviceLastActivityCacheService>()
            .WithFakeTimeProvider()
            .Create();

        var today = sutProvider.GetDependency<FakeTimeProvider>().GetUtcNow().UtcDateTime.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var cachedValue = $"{today}|{clientVersion}";
        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes(cachedValue));

        var result = await sutProvider.Sut.IsUpToDateAsync(userId, identifier, clientVersion);

        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task IsUpToDateAsync_GivenCachedDateIsYesterday_ReturnsFalse(
        Guid userId,
        string identifier,
        string clientVersion)
    {
        var sutProvider = new SutProvider<DeviceLastActivityCacheService>()
            .WithFakeTimeProvider()
            .Create();

        var yesterday = sutProvider.GetDependency<FakeTimeProvider>().GetUtcNow().UtcDateTime.Date.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var cachedValue = $"{yesterday}|{clientVersion}";
        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes(cachedValue));

        var result = await sutProvider.Sut.IsUpToDateAsync(userId, identifier, clientVersion);

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task IsUpToDateAsync_GivenCachedVersionDiffersFromSupplied_ReturnsFalse(
        Guid userId,
        string identifier)
    {
        var sutProvider = new SutProvider<DeviceLastActivityCacheService>()
            .WithFakeTimeProvider()
            .Create();

        var today = sutProvider.GetDependency<FakeTimeProvider>().GetUtcNow().UtcDateTime.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var cachedValue = $"{today}|2025.10.0";
        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes(cachedValue));

        var result = await sutProvider.Sut.IsUpToDateAsync(userId, identifier, "2026.5.1");

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task IsUpToDateAsync_GivenSuppliedVersionNullAndCachedEmpty_ReturnsTrue(
        Guid userId,
        string identifier)
    {
        var sutProvider = new SutProvider<DeviceLastActivityCacheService>()
            .WithFakeTimeProvider()
            .Create();

        var today = sutProvider.GetDependency<FakeTimeProvider>().GetUtcNow().UtcDateTime.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var cachedValue = $"{today}|"; // empty version segment
        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes(cachedValue));

        var result = await sutProvider.Sut.IsUpToDateAsync(userId, identifier, null);

        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task IsUpToDateAsync_GivenCacheMiss_ReturnsFalse(
        SutProvider<DeviceLastActivityCacheService> sutProvider,
        Guid userId,
        string identifier,
        string clientVersion)
    {
        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((byte[])null);

        var result = await sutProvider.Sut.IsUpToDateAsync(userId, identifier, clientVersion);

        Assert.False(result);
    }

    // --- RecordUpdateAsync ---

    [Theory, BitAutoData]
    public async Task RecordUpdateAsync_StoresCorrectCompositeValueAndTtl(
        Guid userId,
        string identifier,
        string clientVersion)
    {
        var sutProvider = new SutProvider<DeviceLastActivityCacheService>()
            .WithFakeTimeProvider()
            .Create();

        var today = sutProvider.GetDependency<FakeTimeProvider>().GetUtcNow().UtcDateTime.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var expectedValue = $"{today}|{clientVersion}";

        await sutProvider.Sut.RecordUpdateAsync(userId, identifier, clientVersion);

        await sutProvider.GetDependency<IDistributedCache>()
            .Received(1)
            .SetAsync(
                Arg.Any<string>(),
                Arg.Is<byte[]>(b => Encoding.UTF8.GetString(b) == expectedValue),
                Arg.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == TimeSpan.FromHours(24)),
                Arg.Any<CancellationToken>());
    }

    [Theory, BitAutoData]
    public async Task RecordUpdateAsync_GivenNullClientVersion_StoresEmptyVersionSegment(
        Guid userId,
        string identifier)
    {
        var sutProvider = new SutProvider<DeviceLastActivityCacheService>()
            .WithFakeTimeProvider()
            .Create();

        var today = sutProvider.GetDependency<FakeTimeProvider>().GetUtcNow().UtcDateTime.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var expectedValue = $"{today}|"; // trailing pipe with empty version

        await sutProvider.Sut.RecordUpdateAsync(userId, identifier, null);

        await sutProvider.GetDependency<IDistributedCache>()
            .Received(1)
            .SetAsync(
                Arg.Any<string>(),
                Arg.Is<byte[]>(b => Encoding.UTF8.GetString(b) == expectedValue),
                Arg.Any<DistributedCacheEntryOptions>(),
                Arg.Any<CancellationToken>());
    }

    [Theory, BitAutoData]
    public async Task RecordUpdateAsync_UsesCacheKeyFormat(
        SutProvider<DeviceLastActivityCacheService> sutProvider,
        string clientVersion)
    {
        var userId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var identifier = "my-device-id";

        await sutProvider.Sut.RecordUpdateAsync(userId, identifier, clientVersion);

        await sutProvider.GetDependency<IDistributedCache>()
            .Received(1)
            .SetAsync(
                "device:last-activity:00000000-0000-0000-0000-000000000001:my-device-id",
                Arg.Any<byte[]>(),
                Arg.Any<DistributedCacheEntryOptions>(),
                Arg.Any<CancellationToken>());
    }
}
