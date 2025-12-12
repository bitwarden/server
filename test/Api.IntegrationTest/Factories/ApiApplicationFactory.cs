using Bit.Core;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Enums;
using Bit.IntegrationTestCommon;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.TestHost;
using Xunit;

#nullable enable

namespace Bit.Api.IntegrationTest.Factories;

public class ApiApplicationFactory : WebApplicationFactoryBase<Startup>
{
    protected IdentityApplicationFactory _identityApplicationFactory;

    public ApiApplicationFactory() : this(new SqliteTestDatabase())
    {
    }

    protected ApiApplicationFactory(ITestDatabase db)
    {
        TestDatabase = db;

        _identityApplicationFactory = new IdentityApplicationFactory();
        _identityApplicationFactory.TestDatabase = TestDatabase;
        _identityApplicationFactory.ManagesDatabase = false;
    }

    public IdentityApplicationFactory Identity => _identityApplicationFactory;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            // Remove scheduled background jobs to prevent errors in parallel test execution
            var jobService = services.First(sd => sd.ServiceType == typeof(IHostedService) && sd.ImplementationType == typeof(Jobs.JobsHostedService));
            services.Remove(jobService);

            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.BackchannelHttpHandler = _identityApplicationFactory.Server.CreateHandler();
            });
        });
    }

    /// <summary>
    /// Helper for registering and logging in to a new account
    /// </summary>
    public async Task<(string Token, string RefreshToken)> LoginWithNewAccount(
        string email = "integration-test@bitwarden.com", string masterPasswordHash = "master_password_hash")
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
                    PublicKey = TestEncryptionConstants.PublicKey,
                    EncryptedPrivateKey = TestEncryptionConstants.AES256_CBC_HMAC_Encstring
                },
                UserSymmetricKey = TestEncryptionConstants.AES256_CBC_HMAC_Encstring,
            });

        return await _identityApplicationFactory.TokenFromPasswordAsync(email, masterPasswordHash);
    }

    /// <summary>
    /// Helper for logging in to an account
    /// </summary>
    public async Task<(string Token, string RefreshToken)> LoginAsync(string email = "integration-test@bitwarden.com", string masterPasswordHash = "master_password_hash")
    {
        return await _identityApplicationFactory.TokenFromPasswordAsync(email, masterPasswordHash);
    }

    /// <summary>
    /// Helper for logging in via client secret.
    /// Currently used for Secrets Manager service accounts
    /// </summary>
    public async Task<string> LoginWithClientSecretAsync(Guid clientId, string clientSecret)
    {
        return await _identityApplicationFactory.TokenFromAccessTokenAsync(clientId, clientSecret);
    }

    /// <summary>
    /// Helper for logging in with an Organization api key.
    /// Currently used for the Public Api
    /// </summary>
    public async Task<string> LoginWithOrganizationApiKeyAsync(string clientId, string clientSecret)
    {
        return await _identityApplicationFactory.TokenFromOrganizationApiKeyAsync(clientId, clientSecret);
    }
}
