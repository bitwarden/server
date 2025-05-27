using Bit.Core;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Enums;
using Bit.IntegrationTestCommon;
using Bit.IntegrationTestCommon.Factories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Events.IntegrationTest;

public class EventsApplicationFactory : WebApplicationFactoryBase<Startup>
{
    private readonly IdentityApplicationFactory _identityApplicationFactory;

    public EventsApplicationFactory() : this(new SqlServerTestDatabase())
    {
    }

    protected EventsApplicationFactory(ITestDatabase db)
    {
        TestDatabase = db;
        HandleDbDisposal = true;

        _identityApplicationFactory = new IdentityApplicationFactory();
        _identityApplicationFactory.TestDatabase = TestDatabase;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.BackchannelHttpHandler = _identityApplicationFactory.Server.CreateHandler();
            });
        });
    }

    /// <summary>
    /// Helper for registering and logging in to a new account
    /// </summary>
    public async Task<(string Token, string RefreshToken)> LoginWithNewAccount(string email = "integration-test@bitwarden.com", string masterPasswordHash = "master_password_hash")
    {
        await _identityApplicationFactory.RegisterNewIdentityFactoryUserAsync(
            new RegisterFinishRequestModel
            {
                Email = email,
                MasterPasswordHash = masterPasswordHash,
                Kdf = KdfType.PBKDF2_SHA256,
                KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
                UserAsymmetricKeys = new KeysRequestModel()
                {
                    PublicKey = "public_key",
                    EncryptedPrivateKey = "private_key"
                },
                UserSymmetricKey = "sym_key",
            });

        return await _identityApplicationFactory.TokenFromPasswordAsync(email, masterPasswordHash);
    }
}
