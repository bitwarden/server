using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using Bit.Core.Repositories;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Seeder.Queries;

/// <summary>
/// Looks up the email two-factor authentication code that was generated for the user with the given email.
/// </summary>
/// <remarks>
/// This is a read-only query: it reads the code the login flow already wrote to the persistent distributed
/// cache (see <c>EmailTokenProvider</c>/<c>EmailTwoFactorTokenProvider</c> in
/// <c>Bit.Core.Auth.Identity.TokenProviders</c>) and never removes it, so the real login can still consume
/// the code. It only succeeds when SeederApi shares the same cache backend as the server that generated the
/// code (Redis or a SQL/EF-backed cache); with the in-memory cache fallback each process has its own cache.
/// </remarks>
public class UserEmailTwoFactorCodeQuery(
    IUserRepository userRepository,
    [FromKeyedServices("persistent")] IDistributedCache distributedCache)
    : IQuery<UserEmailTwoFactorCodeQuery.Request, UserEmailTwoFactorCodeQuery.Response>
{
    // Keep these in sync with EmailTokenProvider: the cache key format and the literal purpose
    // ("TwoFactor") that UserManager.GenerateTwoFactorTokenAsync uses when generating the code.
    private const string CacheKeyFormat = "EmailToken_{0}_{1}_{2}";
    private const string Purpose = "TwoFactor";

    public class Request
    {
        [Required]
        public required string Email { get; set; }
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

        var cacheKey = string.Format(CultureInfo.InvariantCulture, CacheKeyFormat, user.Id, user.SecurityStamp, Purpose);
        var cachedValue = await distributedCache.GetAsync(cacheKey);
        if (cachedValue == null)
        {
            return new Response { Code = null, Found = false };
        }

        return new Response { Code = Encoding.UTF8.GetString(cachedValue), Found = true };
    }
}
