using Bit.Core.Models.Api.Request.Accounts;
using Bit.IntegrationTestCommon.Factories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bit.Identity.IntegrationTest.Controllers;

public class AccountsControllerTests : IClassFixture<IdentityApplicationFactory>
{
    private readonly IdentityApplicationFactory _factory;

    public AccountsControllerTests(IdentityApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostRegister_Success()
    {
        var context = await _factory.RegisterAsync(new RegisterRequestModel
        {
            Email = "test+register@email.com",
            MasterPasswordHash = "master_password_hash"
        });

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        var database = _factory.GetDatabaseContext();
        var user = await database.Users
            .SingleAsync(u => u.Email == "test+register@email.com");

        Assert.NotNull(user);
    }
}
