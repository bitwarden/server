using System.Text.Json;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Entities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Auth.Services;

public class UserVerificationOtpStore(
    IOtpTokenProvider<DefaultOtpTokenProviderOptions> otpTokenProvider,
    [FromKeyedServices("persistent")] IDistributedCache distributedCache,
    TimeProvider timeProvider) : IUserVerificationOtpStore
{
    private const string TokenProviderName = "UserVerification";
    private const string Purpose = "EmailOtp";
    private const int MaxFailedAttempts = 5;
    private const int MaxRequestsPerWindow = 5;
    private static readonly TimeSpan _requestWindow = TimeSpan.FromMinutes(15);

    private readonly IOtpTokenProvider<DefaultOtpTokenProviderOptions> _otpTokenProvider = otpTokenProvider;
    private readonly IDistributedCache _distributedCache = distributedCache;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc />
    public async Task<IssueOtpResult> IssueAsync(User user)
    {
        if (!await TryCountRequestAsync(user))
        {
            return new IssueOtpResult.RequestLimitReached();
        }

        // The provider returns null only for an empty provider name, purpose, or unique identifier, and none of
        // these can be empty here.
        var otp = await _otpTokenProvider.GenerateTokenAsync(
                TokenProviderName, Purpose, UniqueIdentifier(user), user.Email)
            ?? throw new InvalidOperationException("The OTP provider did not generate an OTP.");
        return new IssueOtpResult.Issued(otp);
    }

    /// <inheritdoc />
    public Task<bool> ValidateAndConsumeAsync(User user, string? otp)
    {
        return _otpTokenProvider.ValidateTokenAsync(
            otp ?? "", TokenProviderName, Purpose, UniqueIdentifier(user), user.Email);
    }

    /// <inheritdoc />
    public Task<bool> ValidateAndConsumeWithFailureLimitAsync(User user, string? otp)
    {
        return _otpTokenProvider.ValidateTokenWithFailureLimitAsync(
            otp ?? "", TokenProviderName, Purpose, UniqueIdentifier(user), MaxFailedAttempts, user.Email);
    }

    /// <summary>
    /// Adds one to the user's request count for the current window, unless the count has reached the limit.
    /// A request after the window ends starts a new window. The read and the write are separate cache
    /// operations, so parallel requests can read the same count.
    /// </summary>
    /// <returns><see langword="true"/> if the request was counted; <see langword="false"/> at the limit</returns>
    private async Task<bool> TryCountRequestAsync(User user)
    {
        var cacheKey = RequestCounterKey(user);
        var now = _timeProvider.GetUtcNow();

        var counter = await GetRequestCounterAsync(cacheKey);
        if (counter == null || counter.WindowEndsAt <= now)
        {
            counter = new RequestCounter { Count = 0, WindowEndsAt = now + _requestWindow };
        }

        if (counter.Count >= MaxRequestsPerWindow)
        {
            return false;
        }

        counter.Count++;

        // An absolute expiry at the window's end keeps later requests from extending the window.
        await _distributedCache.SetAsync(
            cacheKey,
            JsonSerializer.SerializeToUtf8Bytes(counter),
            new DistributedCacheEntryOptions { AbsoluteExpiration = counter.WindowEndsAt });
        return true;
    }

    private async Task<RequestCounter?> GetRequestCounterAsync(string cacheKey)
    {
        var cachedValue = await _distributedCache.GetAsync(cacheKey);
        if (cachedValue == null || cachedValue.Length == 0)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RequestCounter>(cachedValue);
        }
        catch (JsonException)
        {
            // Start a new window on any cache entry that isn't this JSON shape, rather than fail the request.
            return null;
        }
    }

    /// <summary>
    /// Keyed by user and security stamp, like other email OTP cache keys, so a security-stamp-changing event
    /// (e.g. a password change) invalidates a pending OTP.
    /// </summary>
    private static string UniqueIdentifier(User user)
    {
        return $"{user.Id}_{user.SecurityStamp}";
    }

    /// <summary>
    /// Keyed by user only, so a security stamp change does not reset the request count.
    /// </summary>
    private static string RequestCounterKey(User user)
    {
        return $"UserVerificationOtpRequests_{user.Id}";
    }

    private sealed class RequestCounter
    {
        public int Count { get; set; }
        public DateTimeOffset WindowEndsAt { get; set; }
    }
}
