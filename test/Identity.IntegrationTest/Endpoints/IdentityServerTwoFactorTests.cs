using System.Security.Claims;
using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Identity.Models.Request.Accounts;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using IdentityModel;
using LinqToDB;
using Microsoft.AspNetCore.TestHost;
using NSubstitute;
using Xunit;

#nullable enable

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
    public async Task TokenEndpoint_GrantTypePassword_UserTwoFactorRequired_NoTwoFactorProvided_Fails(string deviceId)
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
        var context = await _factory.ContextFromPasswordAsync(username, "master_password_hash");

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);
    }

    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypePassword_InvalidTwoFactorToken_Fails(string deviceId)
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
        var context = await _factory.ContextFromPasswordWithTwoFactorAsync(
                                username, "master_password_hash", twoFactorProviderType: "Email");

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var errorModel = AssertHelper.AssertJsonProperty(root, "ErrorModel", JsonValueKind.Object);
        var errorMessage = AssertHelper.AssertJsonProperty(errorModel, "Message", JsonValueKind.String).GetString();
        Assert.Equal("Two-step token is invalid. Try again.", errorMessage);

        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("invalid_username_or_password", error);
    }

    //! Fails when ran with other tests may need to re init the Fixure?
    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypePassword_OrgDuoTwoFactorRequired_NoTwoFactorProvided_Fails(string deviceId)
    {
        // Arrange
        var username = "test+org2farequired@email.com";
        // use valid length keys so DuoWeb.SignRequest doesn't throw
        // ikey: 20, skey: 40, akey: 40
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
                Name = "Test Org",
                Use2fa = true,
                TwoFactorProviders = orgTwoFactor,
                BillingEmail = "billing-email@example.com",
                Plan = "Enterprise",
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
        var context = await _factory.ContextFromPasswordAsync(username, "master_password_hash");

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);
    }

    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypePassword_RememberTwoFactorType_InvalidTwoFactorToken_Fails(string deviceId)
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
        var context = await _factory.ContextFromPasswordWithTwoFactorAsync(
                                username, "master_password_hash", twoFactorProviderType: "Remember");

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);
    }

    /// client_credential grant type should not require two factor even if it's enabled for either Org or individual user
    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypeClientCredential_OrgTwoFactorRequired_Success(Organization organization, OrganizationApiKey organizationApiKey)
    {
        // Arrange        
        var orgRepo = _factory.Services.GetRequiredService<IOrganizationRepository>();
        // use valid length keys so DuoWeb.SignRequest doesn't throw
        // ikey: 20, skey: 40, akey: 40
        var orgTwoFactor =
            """{"6":{"Enabled":true,"MetaData":{"IKey":"DIEFB13LB49IEB3459N2","SKey":"0ZnsZHav0KcNPBZTS6EOUwqLPoB0sfMd5aJeWExQ","Host":"api-example.duosecurity.com"}}}""";
        organization.Enabled = true;
        organization.UseApi = true;
        organization.Use2fa = true;
        organization.TwoFactorProviders = orgTwoFactor;
        organization = await orgRepo.CreateAsync(organization);

        var orgApiKeyRepo = _factory.Services.GetRequiredService<IOrganizationApiKeyRepository>();
        organizationApiKey.OrganizationId = organization.Id;
        organizationApiKey.Type = OrganizationApiKeyType.Default;

        await orgApiKeyRepo.CreateAsync(organizationApiKey);

        // Act
        var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", $"organization.{organization.Id}" },
            { "client_secret", organizationApiKey.ApiKey },
            { "scope", "api.organization" },
        }));

        // Assert
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;
        var token = AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String).GetString();
        Assert.NotEmpty(token);
    }

    /// client_credential grant type should not require two factor even if it's enabled for either Org or individual user
    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypeClientCredential_IndvTwoFactorRequired_Success(string deviceId)
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

        var database = _factory.GetDatabaseContext();
        var user = await database.Users.FirstAsync(u => u.Email == username);

        // Act
        var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", $"user.{user.Id}" },
            { "client_secret", user.ApiKey },
            { "scope", "api" },
            { "DeviceIdentifier", deviceId },
            { "DeviceType",  ((int)DeviceType.FirefoxBrowser).ToString() },
            { "DeviceName", "firefox" },
        }));

        // Assert
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;
        var token = AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String).GetString();
        Assert.NotEmpty(token);
    }

    //! User with enabled TwoFactor is not saved for some reason...
    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypeAuthCode_OrgTwoFactorRequired_IndvTwoFactor_NoTwoFactorProvided_Fails(string deviceId)
    {
        // Arrange
        var username = "test+org2farequired@email.com";
        var twoFactor = """{"1": { "Enabled": true, "MetaData": { "Email": "test+2farequired@email.com"}}}""";
        var challenge = new string('c', 50);
        var factory = await CreateSsoOrganizationAndUserAsync(new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.MasterPassword,
        }, challenge, username, twoFactor);

        // Act
        var context = await factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", "12" },
            { "deviceIdentifier", deviceId },
            { "deviceName", "edge" },
            { "grant_type", "authorization_code" },
            { "code", "test_code" },
            { "code_verifier", challenge },
            { "redirect_uri", "https://localhost:8080/sso-connector.html" }
        }), context => context.Request.Headers.Append("Auth-Email", CoreHelpers.Base64UrlEncodeString(username)));

        // Assert 
        using var responseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);

        var root = responseBody.RootElement;
        var errorModel = AssertHelper.AssertJsonProperty(root, "ErrorModel", JsonValueKind.Object);
        var errorMessage = AssertHelper.AssertJsonProperty(errorModel, "Message", JsonValueKind.String).GetString();
        Assert.Equal("Two-step token is invalid. Try again.", errorMessage);

        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);
    }

    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypeAuthCode_OrgTwoFactorRequired_OrgDuoTwoFactor_NoTwoFactorProvided_Fails(string deviceId)
    {
        // Arrange
        var username = "test+org2farequired@email.com";

        // use valid length keys so DuoWeb.SignRequest doesn't throw
        // ikey: 20, skey: 40, akey: 40
        var orgTwoFactor =
            """{"6":{"Enabled":true,"MetaData":{"IKey":"DIEFB13LB49IEB3459N2","SKey":"0ZnsZHav0KcNPBZTS6EOUwqLPoB0sfMd5aJeWExQ","Host":"api-example.duosecurity.com"}}}""";

        var challenge = new string('c', 50);
        var factory = await CreateSsoOrganizationAndUserAsync(new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.MasterPassword,
        }, challenge, username, orgTwoFactor: orgTwoFactor);

        // Act
        var context = await factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", "12" },
            { "deviceIdentifier", deviceId },
            { "deviceName", "edge" },
            { "grant_type", "authorization_code" },
            { "code", "test_code" },
            { "code_verifier", challenge },
            { "redirect_uri", "https://localhost:8080/sso-connector.html" }
        }), context => context.Request.Headers.Append("Auth-Email", CoreHelpers.Base64UrlEncodeString(username)));

        // Assert 
        using var responseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = responseBody.RootElement;
        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);
    }

    private async Task CreateUserAsync(TestServer server, string username, string deviceId,
        Func<Task> twoFactorSetup)
    {
        // Register user
        await _factory.RegisterAsync(new RegisterRequestModel
        {
            Email = username,
            MasterPasswordHash = "master_password_hash"
        });

        // Add two factor
        if (twoFactorSetup != null)
        {
            await twoFactorSetup();
        }
    }

    private async Task<IdentityApplicationFactory> CreateSsoOrganizationAndUserAsync(
        SsoConfigurationData ssoConfigurationData,
        string challenge,
        string testEmail,
        string? orgTwoFactor = null,
        string? userTwoFactor = null,
        Permissions? permissions = null)
    {
        var factory = new IdentityApplicationFactory();

        var authorizationCode = new AuthorizationCode
        {
            ClientId = "web",
            CreationTime = DateTime.UtcNow,
            Lifetime = (int)TimeSpan.FromMinutes(5).TotalSeconds,
            RedirectUri = "https://localhost:8080/sso-connector.html",
            RequestedScopes = ["api", "offline_access"],
            CodeChallenge = challenge.Sha256(),
            CodeChallengeMethod = "plain",
            Subject = null!, // Temporarily set it to null
        };

        factory.SubstituteService<IAuthorizationCodeStore>(service =>
        {
            service.GetAuthorizationCodeAsync("test_code")
                .Returns(authorizationCode);
        });

        // Create Test User
        var registerResponse = await factory.RegisterAsync(new RegisterRequestModel
        {
            Email = testEmail,
            MasterPasswordHash = "master_password_hash",
        });

        var userRepository = factory.Services.GetRequiredService<IUserRepository>();
        var user = await userRepository.GetByEmailAsync(testEmail);
        Assert.NotNull(user);

        if(userTwoFactor != null)
        {
            user.TwoFactorProviders = userTwoFactor;
            await _userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.Email);
        }

        // Create Organization
        var organizationRepository = factory.Services.GetRequiredService<IOrganizationRepository>();
        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = "billing-email@example.com",
            Plan = "Enterprise",
            UsePolicies = true,
            UseSso = true,
            Use2fa = !string.IsNullOrEmpty(userTwoFactor) || !string.IsNullOrEmpty(orgTwoFactor),
            TwoFactorProviders = orgTwoFactor,
        });

        if(orgTwoFactor != null)
        {
            factory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("globalSettings:Duo:AKey", "WJHB374KM3N5hglO9hniwbkibg$789EfbhNyLpNq1");
            });
        }

        var organizationUserRepository = factory.Services.GetRequiredService<IOrganizationUserRepository>();

        var orgUserPermissions =
            (permissions == null) ? null : JsonSerializer.Serialize(permissions, JsonHelpers.CamelCase);

        // Register User to Organization
        var organizationUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
            Permissions = orgUserPermissions
        });

        // Configure SSO
        var ssoConfigRepository = factory.Services.GetRequiredService<ISsoConfigRepository>();
        await ssoConfigRepository.CreateAsync(new SsoConfig
        {
            OrganizationId = organization.Id,
            Enabled = true,
            Data = JsonSerializer.Serialize(ssoConfigurationData, JsonHelpers.CamelCase),
        });

        var subject = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(JwtClaimTypes.Subject, user.Id.ToString()), // Get real user id
            new Claim(JwtClaimTypes.Name, testEmail),
            new Claim(JwtClaimTypes.IdentityProvider, "sso"),
            new Claim("organizationId", organization.Id.ToString()),
            new Claim(JwtClaimTypes.SessionId, "SOMETHING"),
            new Claim(JwtClaimTypes.AuthenticationMethod, "external"),
            new Claim(JwtClaimTypes.AuthenticationTime, DateTime.UtcNow.AddMinutes(-1).ToEpochTime().ToString())
        ], "Duende.IdentityServer", JwtClaimTypes.Name, JwtClaimTypes.Role));

        authorizationCode.Subject = subject;

        return factory;
    }
}

#region scratch pad
/*
Bunch of options for form body when logging in via SSO
var ssoContext = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
{
    { "scope", "api offline_access" },
    { "client_id", "web" },
    { "deviceType", "12" },
    { "deviceIdentifier", deviceId },
    { "deviceName", "firefox" },
    { "grant_type", "authorization_code" },
    { "username", username },
    { "password", "master_password_hash" },
    { "redirect_uri", "https://localhost:8080/sso-connector.html" },
    { "scope", "api offline_access" },
    { "client_id", "web" },
    { "deviceType", "10" },
    { "deviceIdentifier", deviceId },
    { "deviceName", "firefox" },
    { "grant_type", "authorization_code" },
    { "code", "test_code" },
    { "code_verifier", challenge },
    { "redirect_uri", "https://localhost:8080/sso-connector.html" }
}), context => context.Request.Headers.Append("Auth-Email", CoreHelpers.Base64UrlEncodeString(username)));
*/
#endregion
