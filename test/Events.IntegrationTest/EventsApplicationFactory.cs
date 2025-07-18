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

    public EventsApplicationFactory() : this(new SqliteTestDatabase())
    {
    }

    protected EventsApplicationFactory(ITestDatabase db)
    {
        TestDatabase = db;

        _identityApplicationFactory = new IdentityApplicationFactory();
        _identityApplicationFactory.TestDatabase = TestDatabase;
        _identityApplicationFactory.ManagesDatabase = false;
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
        // This might be the first action in a test and since it forwards to the Identity server, we need to ensure that
        // this server is initialized since it's responsible for seeding the database.
        Assert.NotNull(Services);

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
