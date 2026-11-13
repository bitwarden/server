using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Kdf;
using Bit.Core.Repositories;
using Bit.IntegrationTestCommon;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Constants;
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

        // Identity owns the database so that it builds the Entity Framework model, which is cached for the whole
        // process. The model keeps the data protector of whichever service builds it first, and Events' protector
        // throws when used, so Identity couldn't save users if Events built it.
        ManagesDatabase = false;
        _identityApplicationFactory = new IdentityApplicationFactory();
        _identityApplicationFactory.TestDatabase = TestDatabase;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _identityApplicationFactory.Dispose();
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
        // Identity is initialized first since it's responsible for seeding the database.
        Assert.NotNull(_identityApplicationFactory.Services);
        Assert.NotNull(Services);

        await _identityApplicationFactory.RegisterNewIdentityFactoryUserAsync(
            new RegisterFinishRequestModel
            {
                Email = email,
                MasterPasswordHash = masterPasswordHash,
                Kdf = KdfType.PBKDF2_SHA256,
                KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
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
    /// Gets the id of a user registered through <see cref="LoginWithNewAccount"/>. Users are read through Identity
    /// because Events does not support Data Protection, which reading a user requires.
    /// </summary>
    public async Task<Guid> GetUserIdAsync(string email)
    {
        var user = await _identityApplicationFactory.GetService<IUserRepository>().GetByEmailAsync(email);
        Assert.NotNull(user);
        return user.Id;
    }
}
