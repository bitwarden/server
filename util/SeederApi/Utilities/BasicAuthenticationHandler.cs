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
    private readonly SeederSettings _seederSettings;

    public BasicAuthenticationHandler(
        IOptionsMonitor<BasicAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<SeederSettings> seederSettings)
        : base(options, logger, encoder)
    {
        _seederSettings = seederSettings.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var endpoint = Context.GetEndpoint();
        if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (string.IsNullOrEmpty(_seederSettings.Username) || string.IsNullOrEmpty(_seederSettings.Password))
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

        var expectedUsername = Encoding.UTF8.GetBytes(_seederSettings.Username);
        var expectedPassword = Encoding.UTF8.GetBytes(_seederSettings.Password);
        var providedUsername = Encoding.UTF8.GetBytes(username);
        var providedPassword = Encoding.UTF8.GetBytes(password);

        var usernameMatch = CryptographicOperations.FixedTimeEquals(expectedUsername, providedUsername);
        var passwordMatch = CryptographicOperations.FixedTimeEquals(expectedPassword, providedPassword);

        if (!usernameMatch || !passwordMatch)
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
