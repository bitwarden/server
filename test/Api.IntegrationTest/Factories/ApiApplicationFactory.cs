using Bit.Core.Models.Api.Request.Accounts;
using Bit.IntegrationTestCommon.Factories;
using IdentityServer4.AccessTokenValidation;
using Microsoft.AspNetCore.TestHost;

namespace Bit.Api.IntegrationTest.Factories;

public class ApiApplicationFactory : WebApplicationFactoryBase<Startup>
{
    private readonly IdentityApplicationFactory _identityApplicationFactory;

    public ApiApplicationFactory()
    {
        _identityApplicationFactory = new IdentityApplicationFactory();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<IdentityServerAuthenticationOptions>(IdentityServerAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.JwtBackChannelHandler = _identityApplicationFactory.Server.CreateHandler();
            });
        });
    }

    /// <summary>
    /// Helper for registering and logging in to a new account
    /// </summary>
    public async Task<(string Token, string RefreshToken)> LoginWithNewAccount(string email = "integration-test@bitwarden.com", string masterPasswordHash = "master_password_hash")
    {
        await _identityApplicationFactory.RegisterAsync(new RegisterRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
        });

        return await _identityApplicationFactory.TokenFromPasswordAsync(email, masterPasswordHash);
    }
}
