using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.Repositories;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Seeder.Queries;

/// <summary>
/// Looks up a login or verification code that was generated for the user with the given email. Supports the
/// email two-factor login code, the user-verification OTP (the code generated during UserVerification for
/// users without a master password, e.g. request-otp), and the new device verification code. Which code is
/// returned is selected by <see cref="Request.CodeType"/>.
/// </summary>
/// <remarks>
/// This is a read-only query: it reads the code the login/verification flow already wrote to the persistent
/// distributed cache (see <c>EmailTokenProvider</c>/<c>EmailTwoFactorTokenProvider</c> in
/// <c>Bit.Core.Auth.Identity.TokenProviders</c>, <c>UserService.SendOTPAsync</c>, and
/// <c>NewDeviceVerificationOtpStore</c>) and never removes it, so the real flow can still consume the code.
/// It only succeeds when SeederApi shares the same cache backend as the server that generated the code (Redis
/// or a SQL/EF-backed cache); with the in-memory cache fallback each process has its own cache.
/// </remarks>
public class UserEmailTokenCodeQuery(
    IUserRepository userRepository,
    [FromKeyedServices("persistent")] IDistributedCache distributedCache)
    : IQuery<UserEmailTokenCodeQuery.Request, UserEmailTokenCodeQuery.Response>
{
    // Keep this in sync with EmailTokenProvider: both email-token code kinds use the same cache key format,
    // and both store the code as a bare UTF-8 string.
    private const string EmailTokenCacheKeyFormat = "EmailToken_{0}_{1}_{2}";

    // Keep these purposes in sync with the providers that generate the codes:
    // - "TwoFactor" is the literal purpose UserManager.GenerateTwoFactorTokenAsync uses (EmailTwoFactorTokenProvider).
    // - The user-verification OTP purpose is "otp:" + user.Email (UserService.SendOTPAsync).
    private const string EmailTwoFactorPurpose = "TwoFactor";
    private const string UserVerificationPurposePrefix = "otp:";

    // Keep this in sync with NewDeviceVerificationOtpStore, which owns this key. Restated here rather than
    // referenced, so that reading a code for a test never widens the production API of the code that issues
    // it. UserEmailTokenCodeQueryTests round-trips a real issued code through this key, so a drift from the
    // owner fails a test rather than silently returning "no code".
    private const string NewDeviceVerificationCacheKeyFormat = "NewDeviceVerification_NewDeviceVerificationCode_{0}_{1}";

    /// <summary>
    /// The kind of code to retrieve. The serialized value is the member name (e.g. "EmailTwoFactor").
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CodeType
    {
        EmailTwoFactor,
        UserVerification,
        NewDeviceVerification,
    }

    public class Request
    {
        [Required]
        public required string Email { get; set; }

        [Required]
        public required CodeType CodeType { get; set; }
    }

    public class Response
    {
        public string? Code { get; set; }

        /// <summary>
        /// The device the code is bound to, for code kinds that scope a code to one device. A caller
        /// redeeming such a code has to present this same identifier, so it is returned alongside the code.
        /// Null for code kinds that are not device-scoped.
        /// </summary>
        public string? DeviceIdentifier { get; set; }

        public required bool Found { get; set; }
    }

    public async Task<Response> Execute(Request request)
    {
        var user = await userRepository.GetByEmailAsync(request.Email);
        if (user == null)
        {
            return NotFound();
        }

        var cacheKey = request.CodeType switch
        {
            CodeType.EmailTwoFactor => EmailTokenCacheKey(user.Id, user.SecurityStamp, EmailTwoFactorPurpose),
            CodeType.UserVerification => EmailTokenCacheKey(
                user.Id, user.SecurityStamp, UserVerificationPurposePrefix + user.Email),
            CodeType.NewDeviceVerification => string.Format(
                CultureInfo.InvariantCulture, NewDeviceVerificationCacheKeyFormat, user.Id, user.SecurityStamp),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.CodeType, "Unknown code type."),
        };

        var cachedValue = await distributedCache.GetAsync(cacheKey);
        if (cachedValue == null)
        {
            return NotFound();
        }

        return request.CodeType == CodeType.NewDeviceVerification
            ? ReadEnvelope(cachedValue)
            : new Response { Code = Encoding.UTF8.GetString(cachedValue), Found = true };
    }

    private static string EmailTokenCacheKey(Guid userId, string securityStamp, string purpose)
    {
        return string.Format(CultureInfo.InvariantCulture, EmailTokenCacheKeyFormat, userId, securityStamp, purpose);
    }

    /// <summary>
    /// Reports a cache entry that does not carry the expected envelope as simply absent, matching how the
    /// generating provider treats one, so an unreadable entry does not surface as a query failure.
    /// </summary>
    private static Response ReadEnvelope(byte[] cachedValue)
    {
        OtpCacheEntry? entry;
        try
        {
            entry = JsonSerializer.Deserialize<OtpCacheEntry>(cachedValue);
        }
        catch (JsonException)
        {
            return NotFound();
        }

        if (entry?.Token == null)
        {
            return NotFound();
        }

        return new Response { Code = entry.Token, DeviceIdentifier = entry.BoundValue, Found = true };
    }

    private static Response NotFound()
    {
        return new Response { Code = null, DeviceIdentifier = null, Found = false };
    }

    /// <summary>
    /// Mirrors the envelope the token provider writes: unlike the two email-token kinds, a new device
    /// verification entry holds the code alongside the device it is bound to. The provider keeps its own
    /// envelope type private, so this restates the shape — and the property names must match the serialized
    /// form exactly, since deserialization is case-sensitive.
    /// </summary>
    private sealed record OtpCacheEntry(string? Token, string? BoundValue);
}
