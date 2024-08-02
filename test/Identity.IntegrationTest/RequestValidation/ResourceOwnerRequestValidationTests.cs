using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Identity.Models.Request.Accounts;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Bit.Identity.IntegrationTest.RequestValidation;

public class ResourceOwnerRequestValidationTests : IClassFixture<IdentityApplicationFactory>
{
    private readonly IdentityApplicationFactory _factory;
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;

    public ResourceOwnerRequestValidationTests(IdentityApplicationFactory factory)
    {
        _factory = factory;
        _userRepository = _factory.GetService<IUserRepository>();
        _userService = _factory.GetService<IUserService>();
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_AuthEmailHeaderInvalid_InvalidGrantResponse(string deviceId)
    {
        // Arrange
        var username = "test+2farequired@email.com";
        await CreateUserAsync(username);

        // Act
        var context = await _factory.Server.PostAsync(
            "/connect/token", 
            GetFormUrlEncodedContent(username, deviceId)
        );

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Auth-Email header invalid.", error);
    }


    [Theory, BitAutoData]
    public async Task ValidateAsync_(string deviceId)
    {
        // Arrange
        var username = "test+2farequired@email.com";
        await CreateUserAsync(username);

        // Act
        var context = await _factory.Server.PostAsync(
            "/connect/token",
            GetFormUrlEncodedContent(username, deviceId),
            context => context.SetAuthEmail(username)
        );

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Auth-Email header invalid.", error);
    }

    private async Task CreateUserAsync(string username)
    {
        // Register user
        await _factory.RegisterAsync(new RegisterRequestModel
        {
            Email = username,
            MasterPasswordHash = "master_password_hash"
        });
    }

    private async Task<HttpContext> PostLoginAsync(TestServer server, string username, string deviceId,
        Action<HttpContext> extraConfiguration = null)
    {
        return await server.PostAsync(
            "/connect/token", 
            GetFormUrlEncodedContent(username, deviceId), 
            context => context.SetAuthEmail(username)
        );
    }

    private FormUrlEncodedContent GetFormUrlEncodedContent(string username, string deviceId)
    {
        return new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", DeviceTypeAsString(DeviceType.FirefoxBrowser) },
            { "deviceIdentifier", deviceId },
            { "deviceName", "firefox" },
            { "grant_type", "password" },
            { "username", username },
            { "password", "master_password_hash" },
        });
    }

    private static string DeviceTypeAsString(DeviceType deviceType)
    {
        return ((int)deviceType).ToString();
    }
}
