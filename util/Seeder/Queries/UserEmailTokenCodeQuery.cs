using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.Repositories;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Seeder.Queries;

/// <summary>
/// Looks up a login or verification code that was generated for the user with the given email. Supports the
/// email two-factor login code, the email two-factor setup code (send-email), the user-verification OTP (the code generated during UserVerification for
/// users without a master password, e.g. request-otp), and the new device verification code. Which code is
/// returned is selected by <see cref="Request.CodeType"/>.
/// </summary>
/// <remarks>
/// This is a read-only query: it reads the code the login/verification flow already wrote to the persistent
/// distributed cache (see <c>TwoFactorEmailService</c>, <c>UserVerificationOtpStore</c>, and
/// <c>NewDeviceVerificationOtpStore</c>) and never removes it, so the real flow can still consume the code.
/// It only succeeds when SeederApi shares the same cache backend as the server that generated the code (Redis
/// or a SQL/EF-backed cache); with the in-memory cache fallback each process has its own cache.
/// </remarks>
public class UserEmailTokenCodeQuery(
    IUserRepository userRepository,
    [FromKeyedServices("persistent")] IDistributedCache distributedCache)
    : IQuery<UserEmailTokenCodeQuery.Request, UserEmailTokenCodeQuery.Response>
{
    // Keep this in sync with UserVerificationOtpStore, which owns this key. Restated here rather than
    // referenced, so that reading a code for a test never widens the production API of the code that issues
    // it. UserEmailTokenCodeQueryTests round-trips a real issued code through this key.
    private const string UserVerificationCacheKeyFormat = "UserVerification_EmailOtp_{0}_{1}";

    // Keep this in sync with TwoFactorEmailService, which owns this key for the email two-factor login code.
    // Restated rather than referenced, so that reading a code for a test never widens the production API of the
    // code that issues it.
    private const string EmailTwoFactorLoginCacheKeyFormat = "TwoFactorEmail_LoginCode_{0}_{1}";

    // Keep this in sync with TwoFactorEmailService, which issues the setup code (POST /two-factor/send-email)
    // under the same provider name as the login code but a different purpose.
    private const string EmailTwoFactorSetupCacheKeyFormat = "TwoFactorEmail_SetupCode_{0}_{1}";

    // Keep this in sync with NewDeviceVerificationOtpStore, which owns this key. Restated here rather than
    // referenced, so that reading a code for a test never widens the production API of the code that issues
    // it. UserEmailTokenCodeQueryTests round-trips a real issued code through this key, so a drift from the
    // owner fails a test rather than silently returning "no code".
    private const string NewDeviceVerificationCacheKeyFormat = "NewDeviceVerification_NewDeviceVerificationCode_{0}_{1}";

    /// <summary>
    /// The kind of code to retrieve. The serialized value is the member name.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CodeType
    {
        EmailTwoFactor,
        EmailTwoFactorSetup,
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
            CodeType.EmailTwoFactor => string.Format(
                CultureInfo.InvariantCulture, EmailTwoFactorLoginCacheKeyFormat, user.Id, user.SecurityStamp),
            CodeType.EmailTwoFactorSetup => string.Format(
                CultureInfo.InvariantCulture, EmailTwoFactorSetupCacheKeyFormat, user.Id, user.SecurityStamp),
            CodeType.UserVerification => string.Format(
                CultureInfo.InvariantCulture, UserVerificationCacheKeyFormat, user.Id, user.SecurityStamp),
            CodeType.NewDeviceVerification => string.Format(
                CultureInfo.InvariantCulture, NewDeviceVerificationCacheKeyFormat, user.Id, user.SecurityStamp),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.CodeType, "Unknown code type."),
        };

        var cachedValue = await distributedCache.GetAsync(cacheKey);
        if (cachedValue == null)
        {
            return NotFound();
        }

        return ReadEnvelope(cachedValue, includeBoundDevice: request.CodeType != CodeType.UserVerification);
    }

    /// <summary>
    /// Reports a cache entry that does not carry the expected envelope as simply absent, matching how the
    /// generating provider treats one, so an unreadable entry does not surface as a query failure.
    /// </summary>
    /// <param name="cachedValue">The cached envelope.</param>
    /// <param name="includeBoundDevice">
    /// Whether the envelope's bound value is a device identifier to return. A user verification OTP is bound to
    /// the user's email instead, which is not returned.
    /// </param>
    private static Response ReadEnvelope(byte[] cachedValue, bool includeBoundDevice)
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

        return new Response
        {
            Code = entry.Token,
            DeviceIdentifier = includeBoundDevice ? entry.BoundValue : null,
            Found = true,
        };
    }

    private static Response NotFound()
    {
        return new Response { Code = null, DeviceIdentifier = null, Found = false };
    }

    /// <summary>
    /// Mirrors the envelope the token provider writes, which holds the code alongside the value it is bound to.
    /// The provider keeps its own envelope type private, so this restates the shape — and the property names must
    /// match the serialized form exactly, since deserialization is case-sensitive.
    /// </summary>
    private sealed record OtpCacheEntry(string? Token, string? BoundValue);
}
