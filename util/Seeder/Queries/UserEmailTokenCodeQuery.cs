using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Bit.Core.Repositories;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Seeder.Queries;

/// <summary>
/// Looks up an email-token code that was generated for the user with the given email. Supports both the email
/// two-factor login code and the user-verification OTP (the code generated during UserVerification for users
/// without a master password, e.g. request-otp / new-device verification). Which code is returned is selected
/// by <see cref="Request.CodeType"/>.
/// </summary>
/// <remarks>
/// This is a read-only query: it reads the code the login/verification flow already wrote to the persistent
/// distributed cache (see <c>EmailTokenProvider</c>/<c>EmailTwoFactorTokenProvider</c> in
/// <c>Bit.Core.Auth.Identity.TokenProviders</c>, and <c>UserService.SendOTPAsync</c>) and never removes it, so
/// the real flow can still consume the code. It only succeeds when SeederApi shares the same cache backend as
/// the server that generated the code (Redis or a SQL/EF-backed cache); with the in-memory cache fallback each
/// process has its own cache.
/// </remarks>
public class UserEmailTokenCodeQuery(
    IUserRepository userRepository,
    [FromKeyedServices("persistent")] IDistributedCache distributedCache)
    : IQuery<UserEmailTokenCodeQuery.Request, UserEmailTokenCodeQuery.Response>
{
    // Keep this in sync with EmailTokenProvider: both code kinds use the same cache key format.
    private const string CacheKeyFormat = "EmailToken_{0}_{1}_{2}";

    // Keep these purposes in sync with the providers that generate the codes:
    // - "TwoFactor" is the literal purpose UserManager.GenerateTwoFactorTokenAsync uses (EmailTwoFactorTokenProvider).
    // - The user-verification OTP purpose is "otp:" + user.Email (UserService.SendOTPAsync / TwoFactorEmailService).
    private const string EmailTwoFactorPurpose = "TwoFactor";
    private const string UserVerificationPurposePrefix = "otp:";

    /// <summary>
    /// The kind of email-token code to retrieve. The serialized value is the member name (e.g. "EmailTwoFactor").
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CodeType
    {
        EmailTwoFactor,
        UserVerification,
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
        public required bool Found { get; set; }
    }

    public async Task<Response> Execute(Request request)
    {
        var user = await userRepository.GetByEmailAsync(request.Email);
        if (user == null)
        {
            return new Response { Code = null, Found = false };
        }

        var purpose = request.CodeType switch
        {
            CodeType.EmailTwoFactor => EmailTwoFactorPurpose,
            CodeType.UserVerification => UserVerificationPurposePrefix + user.Email,
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.CodeType, "Unknown code type."),
        };

        var cacheKey = string.Format(CultureInfo.InvariantCulture, CacheKeyFormat, user.Id, user.SecurityStamp, purpose);
        var cachedValue = await distributedCache.GetAsync(cacheKey);
        if (cachedValue == null)
        {
            return new Response { Code = null, Found = false };
        }

        return new Response { Code = Encoding.UTF8.GetString(cachedValue), Found = true };
    }
}