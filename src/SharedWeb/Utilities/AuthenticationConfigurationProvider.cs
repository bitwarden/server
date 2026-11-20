using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;

namespace Bit.SharedWeb.Utilities;

/// <summary>
/// Exposes the <c>Authentication</c> configuration section, mirroring the framework provider that is only registered
/// through <c>AddAuthentication</c> and is internal.
/// </summary>
internal sealed class AuthenticationConfigurationProvider : IAuthenticationConfigurationProvider
{
    private readonly IConfiguration _configuration;

    public AuthenticationConfigurationProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IConfiguration AuthenticationConfiguration => _configuration.GetSection("Authentication");
}
