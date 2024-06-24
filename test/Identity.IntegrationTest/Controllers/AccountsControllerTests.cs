using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Identity.Models.Request.Accounts;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
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

    [Theory]
    [BitAutoData("invalidEmail")]
    [BitAutoData("")]
    public async Task PostRegisterSendEmailVerification_InvalidRequestModel_ThrowsBadRequestException(string email, string name, bool receiveMarketingEmails)
    {

        var model = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails
        };

        var context = await _factory.PostRegisterSendEmailVerificationAsync(model);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task PostRegisterSendEmailVerification_WhenGivenNewOrExistingUser_ReturnsNoContent(bool shouldPreCreateUser, string name, bool receiveMarketingEmails)
    {
        var email = $"test+register+{name}@email.com";
        if (shouldPreCreateUser)
        {
            await CreateUserAsync(email, name);
        }

        var model = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails
        };

        var context = await _factory.PostRegisterSendEmailVerificationAsync(model);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
    }

    private async Task<User> CreateUserAsync(string email, string name)
    {
        var userRepository = _factory.Services.GetRequiredService<IUserRepository>();

        var user = new User
        {
            Email = email,
            Id = Guid.NewGuid(),
            Name = name,
            SecurityStamp = Guid.NewGuid().ToString(),
            ApiKey = "test_api_key",
        };

        await userRepository.CreateAsync(user);

        return user;
    }
}
