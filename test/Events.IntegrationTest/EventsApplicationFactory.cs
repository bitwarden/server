using Bit.Identity.Models.Request.Accounts;
using Bit.IntegrationTestCommon.Factories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Events.IntegrationTest;

public class EventsApplicationFactory : WebApplicationFactoryBase<Startup>
{
    private readonly IdentityApplicationFactory _identityApplicationFactory;
    private const string _connectionString = "DataSource=:memory:";

    public EventsApplicationFactory()
    {
        SqliteConnection = new SqliteConnection(_connectionString);
        SqliteConnection.Open();

        _identityApplicationFactory = new IdentityApplicationFactory();
        _identityApplicationFactory.SqliteConnection = SqliteConnection;
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
        await _identityApplicationFactory.RegisterAsync(new RegisterRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
        });

        return await _identityApplicationFactory.TokenFromPasswordAsync(email, masterPasswordHash);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection!.Dispose();
    }
}
