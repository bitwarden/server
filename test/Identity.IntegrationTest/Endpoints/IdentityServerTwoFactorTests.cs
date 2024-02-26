using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Bit.Identity.IntegrationTest.Endpoints;

public class IdentityServerTwoFactorTests : IClassFixture<IdentityApplicationFactory>
{
    private readonly IdentityApplicationFactory _factory;
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;

    public IdentityServerTwoFactorTests(IdentityApplicationFactory factory)
    {
        _factory = factory;
        _userRepository = _factory.GetService<IUserRepository>();
        _userService = _factory.GetService<IUserService>();
    }

    [Theory, BitAutoData]
    public async Task TokenEndpoint_UserTwoFactorRequired_NoTwoFactorProvided_Fails(string deviceId)
    {
        // Arrange
        var username = "test+2farequired@email.com";
        var twoFactor = """{"1": { "Enabled": true, "MetaData": { "Email": "test+2farequired@email.com"}}}""";

        await CreateUserAsync(_factory.Server, username, deviceId, async () =>
        {
            var user = await _userRepository.GetByEmailAsync(username);
            user.TwoFactorProviders = twoFactor;
            await _userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.Email);
        });

        // Act
        var context = await PostLoginAsync(_factory.Server, username, deviceId);

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);
    }

    [Theory, BitAutoData]
    public async Task TokenEndpoint_OrgTwoFactorRequired_NoTwoFactorProvided_Fails(string deviceId)
    {
        // Arrange
        var username = "test+org2farequired@email.com";
        // use valid keys so DuoWeb.SignRequest doesn't throw
        var orgTwoFactor =
            """{"6":{"Enabled":true,"MetaData":{"IKey":"DIEFB13LB49IEB3459N2","SKey":"0ZnsZHav0KcNPBZTS6EOUwqLPoB0sfMd5aJeWExQ","Host":"api-example.duosecurity.com"}}}""";

        var server = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("globalSettings:Duo:AKey", "WJHB374KM3N5hglO9hniwbkibg$789EfbhNyLpNq1");
        }).Server;


        await CreateUserAsync(server, username, deviceId, async () =>
        {
            var user = await _userRepository.GetByEmailAsync(username);

            var organizationRepository = _factory.Services.GetService<IOrganizationRepository>();
            var organization = await organizationRepository.CreateAsync(new Organization
            {
                Name = "Test Org", Use2fa = true, TwoFactorProviders = orgTwoFactor,
            });

            await _factory.Services.GetService<IOrganizationUserRepository>()
                .CreateAsync(new OrganizationUser
                {
                    UserId = user.Id,
                    OrganizationId = organization.Id,
                    Status = OrganizationUserStatusType.Confirmed,
                    Type = OrganizationUserType.User,
                });
        });

        // Act
        var context = await PostLoginAsync(server, username, deviceId);

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);
    }

    private async Task CreateUserAsync(TestServer server, string username, string deviceId,
        Func<Task> twoFactorSetup)
    {
        // Register user
        await _factory.RegisterAsync(new RegisterRequestModel
        {
            Email = username, MasterPasswordHash = "master_password_hash"
        });

        // Add two factor
        if (twoFactorSetup != null)
        {
            await twoFactorSetup();
        }
    }

    private async Task<HttpContext> PostLoginAsync(TestServer server, string username, string deviceId,
        Action<HttpContext> extraConfiguration = null)
    {
        return await server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", DeviceTypeAsString(DeviceType.FirefoxBrowser) },
            { "deviceIdentifier", deviceId },
            { "deviceName", "firefox" },
            { "grant_type", "password" },
            { "username", username },
            { "password", "master_password_hash" },
        }), context => context.SetAuthEmail(username));
    }

    private static string DeviceTypeAsString(DeviceType deviceType)
    {
        return ((int)deviceType).ToString();
    }
}
