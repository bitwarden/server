using System.Text;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Services;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.Services;

[SutProviderCustomize]
public class NewDeviceVerificationOtpStoreTests
{
    [Theory, BitAutoData]
    public async Task IssueAsync_DelegatesToOtpTokenProvider(
        SutProvider<NewDeviceVerificationOtpStore> sutProvider, User user)
    {
        var deviceIdentifier = "device-identifier";
        var code = "123456";

        sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .GenerateTokenAsync("NewDeviceVerification", "NewDeviceVerificationCode",
                $"{user.Id}_{user.SecurityStamp}", deviceIdentifier)
            .Returns(code);

        Assert.Equal(code, await sutProvider.Sut.IssueAsync(user, deviceIdentifier));
    }

    [Theory, BitAutoData]
    public async Task ValidateAndConsumeAsync_DelegatesToOtpTokenProvider(
        SutProvider<NewDeviceVerificationOtpStore> sutProvider, User user)
    {
        var deviceIdentifier = "device-identifier";
        var otp = "123456";

        sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .ValidateTokenAsync(otp, "NewDeviceVerification", "NewDeviceVerificationCode",
                $"{user.Id}_{user.SecurityStamp}", deviceIdentifier)
            .Returns(true);

        Assert.True(await sutProvider.Sut.ValidateAndConsumeAsync(user, deviceIdentifier, otp));
    }

    [Theory, BitAutoData]
    public async Task ValidateAndConsumeAsync_NullOtp_ValidatesEmptyString(
        SutProvider<NewDeviceVerificationOtpStore> sutProvider, User user)
    {
        var deviceIdentifier = "device-identifier";

        await sutProvider.Sut.ValidateAndConsumeAsync(user, deviceIdentifier, null);

        await sutProvider.GetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>()
            .Received(1)
            .ValidateTokenAsync("", "NewDeviceVerification", "NewDeviceVerificationCode",
                $"{user.Id}_{user.SecurityStamp}", deviceIdentifier);
    }

    // TODO: PM-43465 - Delete this test along with GetPendingDeviceIdentifierAsync once mobile sends the
    // Device-Identifier header on the resend request (PM-43467) and that release has aged out of the
    // support window.
    /// <summary>
    /// The pending device record must outlive the code, because the usual reason to press resend is that the
    /// previous code already expired. Asserts the two are written to different keys under different
    /// expirations, so the record survives a read that the code no longer would.
    /// </summary>
    [Theory, BitAutoData]
    public async Task IssueAsync_WritesPendingDeviceUnderItsOwnKeyAndLongerTtl(
        SutProvider<NewDeviceVerificationOtpStore> sutProvider, User user)
    {
        var deviceIdentifier = "device-identifier";
        var uniqueIdentifier = $"{user.Id}_{user.SecurityStamp}";

        await sutProvider.Sut.IssueAsync(user, deviceIdentifier);

        await sutProvider.GetDependency<IDistributedCache>()
            .Received(1)
            .SetAsync(
                $"NewDeviceVerification_PendingDevice_{uniqueIdentifier}",
                Arg.Is<byte[]>(bytes => Encoding.UTF8.GetString(bytes) == deviceIdentifier),
                Arg.Is<DistributedCacheEntryOptions>(options =>
                    options.AbsoluteExpirationRelativeToNow == TimeSpan.FromMinutes(15)),
                Arg.Any<CancellationToken>());
    }

    // TODO: PM-43465 - Delete this test along with GetPendingDeviceIdentifierAsync once mobile sends the
    // Device-Identifier header on the resend request (PM-43467) and that release has aged out of the
    // support window.
    [Theory, BitAutoData]
    public async Task GetPendingDeviceIdentifierAsync_ReadsPendingDeviceKey(
        SutProvider<NewDeviceVerificationOtpStore> sutProvider, User user)
    {
        var deviceIdentifier = "device-identifier";

        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync($"NewDeviceVerification_PendingDevice_{user.Id}_{user.SecurityStamp}",
                Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes(deviceIdentifier));

        Assert.Equal(deviceIdentifier, await sutProvider.Sut.GetPendingDeviceIdentifierAsync(user));
    }

    /// <summary>
    /// Exercises the store against a real <see cref="OtpTokenProvider{TOptions}"/> and in-memory cache, so the
    /// device-binding behavior is verified end to end rather than through a mocked delegation.
    /// </summary>
    [Theory, BitAutoData]
    public async Task IssueAsync_DifferentDevice_SupersedesPriorCode(User user)
    {
        var sutProvider = BuildSutProviderOverRealCache();

        var firstCode = await sutProvider.Sut.IssueAsync(user, "first-device");
        var secondCode = await sutProvider.Sut.IssueAsync(user, "second-device");

        Assert.False(await sutProvider.Sut.ValidateAndConsumeAsync(user, "first-device", firstCode));
        Assert.True(await sutProvider.Sut.ValidateAndConsumeAsync(user, "second-device", secondCode));
    }

    [Theory, BitAutoData]
    public async Task ValidateAndConsumeAsync_SameDevice_IsSingleUse(User user)
    {
        var sutProvider = BuildSutProviderOverRealCache();
        var deviceIdentifier = "device-identifier";

        var code = await sutProvider.Sut.IssueAsync(user, deviceIdentifier);

        Assert.True(await sutProvider.Sut.ValidateAndConsumeAsync(user, deviceIdentifier, code));
        Assert.False(await sutProvider.Sut.ValidateAndConsumeAsync(user, deviceIdentifier, code));
    }

    [Theory, BitAutoData]
    public async Task ValidateAndConsumeAsync_DifferentDevice_ReturnsFalse(User user)
    {
        var sutProvider = BuildSutProviderOverRealCache();

        var code = await sutProvider.Sut.IssueAsync(user, "issued-for-device-identifier");

        Assert.False(
            await sutProvider.Sut.ValidateAndConsumeAsync(user, "submitting-device-identifier", code));
    }

    // TODO: PM-43465 - Delete this test along with GetPendingDeviceIdentifierAsync once mobile sends the
    // Device-Identifier header on the resend request (PM-43467) and that release has aged out of the
    // support window.
    [Theory, BitAutoData]
    public async Task GetPendingDeviceIdentifierAsync_Issued_ReturnsDevice(User user)
    {
        var sutProvider = BuildSutProviderOverRealCache();
        var deviceIdentifier = "device-identifier";

        await sutProvider.Sut.IssueAsync(user, deviceIdentifier);

        Assert.Equal(deviceIdentifier, await sutProvider.Sut.GetPendingDeviceIdentifierAsync(user));
    }

    // TODO: PM-43465 - Delete this test along with GetPendingDeviceIdentifierAsync once mobile sends the
    // Device-Identifier header on the resend request (PM-43467) and that release has aged out of the
    // support window.
    [Theory, BitAutoData]
    public async Task GetPendingDeviceIdentifierAsync_NotIssued_ReturnsNull(User user)
    {
        var sutProvider = BuildSutProviderOverRealCache();

        Assert.Null(await sutProvider.Sut.GetPendingDeviceIdentifierAsync(user));
    }

    /// <summary>
    /// Builds a store backed by a real <see cref="OtpTokenProvider{TOptions}"/> over a real, in-memory
    /// <see cref="IDistributedCache"/>, so issue/validate/get round-trip through actual cache entries instead
    /// of mocked calls. The store and the provider share one cache, matching production, where both resolve
    /// the same "persistent" keyed instance.
    /// </summary>
    private static SutProvider<NewDeviceVerificationOtpStore> BuildSutProviderOverRealCache()
    {
        var distributedCache = new MemoryDistributedCache(
            new OptionsWrapper<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions()));
        var otpTokenProvider = new OtpTokenProvider<DefaultOtpTokenProviderOptions>(
            distributedCache, new OptionsWrapper<DefaultOtpTokenProviderOptions>(new DefaultOtpTokenProviderOptions()));

        var sutProvider = new SutProvider<NewDeviceVerificationOtpStore>().Create();
        return sutProvider
            .SetDependency<IOtpTokenProvider<DefaultOtpTokenProviderOptions>>(otpTokenProvider, "otpTokenProvider")
            .SetDependency<IDistributedCache>(distributedCache, "distributedCache")
            .Create();
    }
}
