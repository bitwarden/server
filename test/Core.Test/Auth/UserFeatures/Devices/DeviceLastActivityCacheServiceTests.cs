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
    [Theory, BitAutoData]
    public async Task HasBeenBumpedTodayAsync_GivenCachedDateIsToday_ReturnsTrue(Guid userId, string identifier)
    {
        var sutProvider = new SutProvider<DeviceLastActivityCacheService>()
            .WithFakeTimeProvider()
            .Create();

        var today = sutProvider.GetDependency<FakeTimeProvider>().GetUtcNow().UtcDateTime.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes(today));

        var result = await sutProvider.Sut.HasBeenBumpedTodayAsync(userId, identifier);

        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task HasBeenBumpedTodayAsync_GivenCachedDateIsYesterday_ReturnsFalse(Guid userId, string identifier)
    {
        var sutProvider = new SutProvider<DeviceLastActivityCacheService>()
            .WithFakeTimeProvider()
            .Create();

        var yesterday = sutProvider.GetDependency<FakeTimeProvider>().GetUtcNow().UtcDateTime.Date.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes(yesterday));

        var result = await sutProvider.Sut.HasBeenBumpedTodayAsync(userId, identifier);

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task HasBeenBumpedTodayAsync_GivenCacheMiss_ReturnsFalse(
        SutProvider<DeviceLastActivityCacheService> sutProvider,
        Guid userId,
        string identifier)
    {
        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((byte[])null);

        var result = await sutProvider.Sut.HasBeenBumpedTodayAsync(userId, identifier);

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task RecordBumpAsync_StoresCorrectDateAndTtl(Guid userId, string identifier)
    {
        var sutProvider = new SutProvider<DeviceLastActivityCacheService>()
            .WithFakeTimeProvider()
            .Create();

        var today = sutProvider.GetDependency<FakeTimeProvider>().GetUtcNow().UtcDateTime.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        await sutProvider.Sut.RecordBumpAsync(userId, identifier);

        await sutProvider.GetDependency<IDistributedCache>()
            .Received(1)
            .SetAsync(
                Arg.Any<string>(),
                Arg.Is<byte[]>(b => Encoding.UTF8.GetString(b) == today),
                Arg.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == TimeSpan.FromHours(48)),
                Arg.Any<CancellationToken>());
    }

    [Theory, BitAutoData]
    public async Task RecordBumpAsync_UsesCacheKeyFormat(
        SutProvider<DeviceLastActivityCacheService> sutProvider)
    {
        var userId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var identifier = "my-device-id";

        await sutProvider.Sut.RecordBumpAsync(userId, identifier);

        await sutProvider.GetDependency<IDistributedCache>()
            .Received(1)
            .SetAsync(
                "device:last-activity:00000000-0000-0000-0000-000000000001:my-device-id",
                Arg.Any<byte[]>(),
                Arg.Any<DistributedCacheEntryOptions>(),
                Arg.Any<CancellationToken>());
    }
}
