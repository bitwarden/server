using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Entities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Auth.Services;

public class NewDeviceVerificationOtpStore(
    IOtpTokenProvider<DefaultOtpTokenProviderOptions> otpTokenProvider,
    [FromKeyedServices("persistent")] IDistributedCache distributedCache) : INewDeviceVerificationOtpStore
{
    private const string TokenProviderName = "NewDeviceVerification";
    private const string Purpose = "NewDeviceVerificationCode";

    // TODO: PM-43465 - Delete this, along with every other pending-device member in this file, once mobile
    // sends the Device-Identifier header on the resend request (PM-43467) and that release has aged out of
    // the support window.
    /// <summary>
    /// How long a header-less resend can still identify the device a code was issued to. Deliberately longer
    /// than the code's own lifetime, because the usual reason to press resend is that the previous code
    /// already expired — a record that died with the code would leave that resend nothing to scope to. The
    /// record names a device, which is not a secret, and it does not extend the code's validity.
    /// </summary>
    private static readonly DistributedCacheEntryOptions _pendingDeviceCacheEntryOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
    };

    private readonly IOtpTokenProvider<DefaultOtpTokenProviderOptions> _otpTokenProvider = otpTokenProvider;

    // TODO: PM-43465 - Delete this, along with every other pending-device member in this file, once mobile
    // sends the Device-Identifier header on the resend request (PM-43467) and that release has aged out of
    // the support window.
    private readonly IDistributedCache _distributedCache = distributedCache;

    /// <inheritdoc />
    public async Task<string> IssueAsync(User user, string deviceIdentifier)
    {
        var code = await _otpTokenProvider.GenerateTokenAsync(
            TokenProviderName, Purpose, UniqueIdentifier(user), deviceIdentifier);

        // TODO: PM-43465 - Delete this write, along with every other pending-device member in this file,
        // once mobile sends the Device-Identifier header on the resend request (PM-43467) and that release
        // has aged out of the support window.
        await _distributedCache.SetStringAsync(
            PendingDeviceKey(user), deviceIdentifier, _pendingDeviceCacheEntryOptions);

        return code!;
    }

    /// <inheritdoc />
    public Task<bool> ValidateAndConsumeAsync(User user, string deviceIdentifier, string? otp)
    {
        return _otpTokenProvider.ValidateTokenAsync(
            otp ?? "", TokenProviderName, Purpose, UniqueIdentifier(user), deviceIdentifier);
    }

    // TODO: PM-43465 - Delete this method, along with every other pending-device member in this file, once
    // mobile sends the Device-Identifier header on the resend request (PM-43467) and that release has aged
    // out of the support window. It exists only to let that resend fall back to the device a pending code
    // was issued to.
    /// <inheritdoc />
    public Task<string?> GetPendingDeviceIdentifierAsync(User user)
    {
        return _distributedCache.GetStringAsync(PendingDeviceKey(user));
    }

    /// <summary>
    /// Keyed by user and security stamp, exactly like <see cref="EmailTokenProvider"/>'s own cache key, so a
    /// security-stamp-changing event (e.g. a password change) invalidates any pending code the same way it
    /// would for other email tokens.
    /// </summary>
    private static string UniqueIdentifier(User user)
    {
        return $"{user.Id}_{user.SecurityStamp}";
    }

    // TODO: PM-43465 - Delete this method, along with every other pending-device member in this file, once
    // mobile sends the Device-Identifier header on the resend request (PM-43467) and that release has aged
    // out of the support window.
    /// <summary>
    /// Deliberately a different key from the one holding the code, so the two expire independently.
    /// </summary>
    private static string PendingDeviceKey(User user)
    {
        return $"{TokenProviderName}_PendingDevice_{UniqueIdentifier(user)}";
    }
}
