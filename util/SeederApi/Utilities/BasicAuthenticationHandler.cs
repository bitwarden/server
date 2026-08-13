using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Bit.SeederApi.Utilities;

public class BasicAuthenticationHandler : AuthenticationHandler<BasicAuthenticationOptions>
{
    private static readonly byte[] _dummyPassword = Encoding.UTF8.GetBytes(
        "dummy-password-for-timing-equalization");

    private readonly Dictionary<string, byte[]> _accounts;

    public BasicAuthenticationHandler(
        IOptionsMonitor<BasicAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<SeederSettings> seederSettings)
        : base(options, logger, encoder)
    {
        // Drop half-configured entries
        // Repeated usernames use the first configured password
        _accounts = seederSettings.Value.Accounts
            .Where(a => !string.IsNullOrEmpty(a.Username) && !string.IsNullOrEmpty(a.Password))
            .GroupBy(a => a.Username, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => Encoding.UTF8.GetBytes(g.First().Password), StringComparer.Ordinal);
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var endpoint = Context.GetEndpoint();
        if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (_accounts.Count == 0)
        {
            Logger.LogWarning("Seeder credentials are not configured");
            return Task.FromResult(AuthenticateResult.Fail("Seeder credentials not configured"));
        }

        if (!Request.Headers.TryGetValue("Authorization", out var authHeader) || authHeader.Count != 1)
        {
            Logger.LogWarning("Request received without Authorization header");
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization header"));
        }

        var headerValue = authHeader.ToString();
        if (!headerValue.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization scheme"));
        }

        byte[] decodedBytes;
        try
        {
            decodedBytes = Convert.FromBase64String(headerValue.Substring(6));
        }
        catch (FormatException)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Base64 in Authorization header"));
        }

        var decoded = Encoding.UTF8.GetString(decodedBytes);
        var parts = decoded.Split(':', 2);
        if (parts is not [var username, var password])
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Basic credential format"));
        }

        // Equalize the miss path: if the username isn't found we still run FixedTimeEquals
        // against a dummy so the branch cost matches a hit. Combine with bitwise & so the
        // final decision doesn't short-circuit on the userExists check.
        var userExists = _accounts.TryGetValue(username, out var storedPassword);
        var comparisonTarget = storedPassword ?? _dummyPassword;
        var providedPassword = Encoding.UTF8.GetBytes(password);
        var passwordMatches = CryptographicOperations.FixedTimeEquals(providedPassword, comparisonTarget);

        if (!(userExists & passwordMatches))
        {
            Logger.LogWarning("Invalid credentials provided for SeederApi");
            return Task.FromResult(AuthenticateResult.Fail("Invalid credentials"));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
        };
        var identity = new ClaimsIdentity(claims, nameof(BasicAuthenticationHandler));
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            BasicAuthenticationOptions.DefaultScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
