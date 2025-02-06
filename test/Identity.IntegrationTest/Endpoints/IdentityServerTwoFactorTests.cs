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
using NSubstitute;
using Xunit;

// #nullable enable

namespace Bit.Identity.IntegrationTest.Endpoints;

public class IdentityServerTwoFactorTests : IClassFixture<IdentityApplicationFactory>
{
    const string _organizationTwoFactor = """{"6":{"Enabled":true,"MetaData":{"ClientId":"DIEFB13LB49IEB3459N2","ClientSecret":"0ZnsZHav0KcNPBZTS6EOUwqLPoB0sfMd5aJeWExQ","Host":"api-example.duosecurity.com"}}}""";
    const string _testEmail = "test+2farequired@email.com";
    const string _testPassword = "master_password_hash";
    const string _userEmailTwoFactor = """{"1": { "Enabled": true, "MetaData": { "Email": "test+2farequired@email.com"}}}""";

    private readonly IdentityApplicationFactory _factory;

    public IdentityServerTwoFactorTests(IdentityApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_UserTwoFactorRequired_NoTwoFactorProvided_Fails()
    {
        // Arrange
        await CreateUserAsync(_factory, _testEmail, _userEmailTwoFactor);

        // Act
        var context = await _factory.ContextFromPasswordAsync(_testEmail, _testPassword);

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);
    }

    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_UserTwoFactorRequired_TwoFactorProvided_Success()
    {
        // Arrange
        // we can't use the class factory here.
        var factory = new IdentityApplicationFactory();

        string emailToken = null;
        factory.SubstituteService<IMailService>(mailService =>
        {
            mailService.SendTwoFactorEmailAsync(Arg.Any<string>(), Arg.Do<string>(t => emailToken = t))
                .Returns(Task.CompletedTask);
        });

        // Create Test User
        await CreateUserAsync(factory, _testEmail, _userEmailTwoFactor);

        // Act
        var failedTokenContext = await factory.ContextFromPasswordAsync(_testEmail, _testPassword);

        Assert.Equal(StatusCodes.Status400BadRequest, failedTokenContext.Response.StatusCode);
        Assert.NotNull(emailToken);

        var twoFactorProvidedContext = await factory.ContextFromPasswordWithTwoFactorAsync(
            _testEmail,
            _testPassword,
            twoFactorToken: emailToken);

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(twoFactorProvidedContext);
        var root = body.RootElement;

        var result = AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String).GetString();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_InvalidTwoFactorToken_Fails()
    {
        // Arrange
        await CreateUserAsync(_factory, _testEmail, _userEmailTwoFactor);

        // Act
        var context = await _factory.ContextFromPasswordWithTwoFactorAsync(
                                _testEmail, _testPassword, twoFactorProviderType: "Email");

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var errorModel = AssertHelper.AssertJsonProperty(root, "ErrorModel", JsonValueKind.Object);
        var errorMessage = AssertHelper.AssertJsonProperty(errorModel, "Message", JsonValueKind.String).GetString();
        Assert.Equal("Two-step token is invalid. Try again.", errorMessage);

        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("invalid_username_or_password", error);
    }

    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypePassword_OrgDuoTwoFactorRequired_NoTwoFactorProvided_Fails(string deviceId)
    {
        // Arrange
        var challenge = new string('c', 50);
        var ssoConfigData = new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.MasterPassword,
        };
        await CreateSsoOrganizationAndUserAsync(
            _factory, ssoConfigData, challenge, _testEmail, orgTwoFactor: _organizationTwoFactor);

        // Act
        var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", "12" },
            { "deviceIdentifier", deviceId },
            { "deviceName", "edge" },
            { "grant_type", "password" },
            { "username", _testEmail },
            { "password", _testPassword },
        }), context => context.Request.Headers.Append("Auth-Email", CoreHelpers.Base64UrlEncodeString(_testEmail)));

        // Assert
        using var responseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = responseBody.RootElement;
        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);
    }

    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_RememberTwoFactorType_InvalidTwoFactorToken_Fails()
    {
        // Arrange
        await CreateUserAsync(_factory, _testEmail, _userEmailTwoFactor);

        // Act
        var context = await _factory.ContextFromPasswordWithTwoFactorAsync(
                                _testEmail, _testPassword, twoFactorProviderType: "Remember");

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);
    }

    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypeClientCredential_OrgTwoFactorRequired_Success(Organization organization, OrganizationApiKey organizationApiKey)
    {
        // Arrange
        organization.Enabled = true;
        organization.UseApi = true;
        organization.Use2fa = true;
        organization.TwoFactorProviders = _organizationTwoFactor;

        var orgRepo = _factory.Services.GetRequiredService<IOrganizationRepository>();
        organization = await orgRepo.CreateAsync(organization);

        organizationApiKey.OrganizationId = organization.Id;
        organizationApiKey.Type = OrganizationApiKeyType.Default;

        var orgApiKeyRepo = _factory.Services.GetRequiredService<IOrganizationApiKeyRepository>();
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
        Assert.NotNull(token);
    }

    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypeClientCredential_IndvTwoFactorRequired_Success(string deviceId)
    {
        // Arrange
        await CreateUserAsync(_factory, _testEmail, _userEmailTwoFactor);

        var database = _factory.GetDatabaseContext();
        var user = await database.Users.FirstAsync(u => u.Email == _testEmail);

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
        Assert.NotNull(token);
    }

    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypeAuthCode_OrgTwoFactorRequired_IndvTwoFactor_NoTwoFactorProvided_Fails(string deviceId)
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        var challenge = new string('c', 50);
        var ssoConfigData = new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.MasterPassword,
        };
        await CreateSsoOrganizationAndUserAsync(
            localFactory, ssoConfigData, challenge, _testEmail, userTwoFactor: _userEmailTwoFactor);

        // Act
        var context = await localFactory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
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
        }), context => context.Request.Headers.Append("Auth-Email", CoreHelpers.Base64UrlEncodeString(_testEmail)));

        // Assert
        using var responseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = responseBody.RootElement;
        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);
    }

    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypeAuthCode_OrgTwoFactorRequired_IndvTwoFactor_TwoFactorProvided_Success(string deviceId)
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        string emailToken = null;
        localFactory.SubstituteService<IMailService>(mailService =>
        {
            mailService.SendTwoFactorEmailAsync(Arg.Any<string>(), Arg.Do<string>(t => emailToken = t))
                .Returns(Task.CompletedTask);
        });

        // Create Test User
        var challenge = new string('c', 50);
        var ssoConfigData = new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.MasterPassword,
        };
        await CreateSsoOrganizationAndUserAsync(
            localFactory, ssoConfigData, challenge, _testEmail, userTwoFactor: _userEmailTwoFactor);

        // Act
        var failedTokenContext = await localFactory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
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
        }), context => context.Request.Headers.Append("Auth-Email", CoreHelpers.Base64UrlEncodeString(_testEmail)));

        Assert.Equal(StatusCodes.Status400BadRequest, failedTokenContext.Response.StatusCode);
        Assert.NotNull(emailToken);

        var twoFactorProvidedContext = await localFactory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", "12" },
            { "deviceIdentifier", deviceId },
            { "deviceName", "edge" },
            { "twoFactorToken", emailToken},
            { "twoFactorProvider", "1" },
            { "twoFactorRemember", "0" },
            { "grant_type", "authorization_code" },
            { "code", "test_code" },
            { "code_verifier", challenge },
            { "redirect_uri", "https://localhost:8080/sso-connector.html" }
        }), context => context.Request.Headers.Append("Auth-Email", CoreHelpers.Base64UrlEncodeString(_testEmail)));


        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(twoFactorProvidedContext);
        var root = body.RootElement;

        var result = AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String).GetString();
        Assert.NotNull(result);
    }

    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypeAuthCode_OrgTwoFactorRequired_OrgDuoTwoFactor_NoTwoFactorProvided_Fails(string deviceId)
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        var challenge = new string('c', 50);
        var ssoConfigData = new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.MasterPassword,
        };

        await CreateSsoOrganizationAndUserAsync(
            localFactory, ssoConfigData, challenge, _testEmail, orgTwoFactor: _organizationTwoFactor);

        // Act
        var context = await localFactory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
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
        }), context => context.Request.Headers.Append("Auth-Email", CoreHelpers.Base64UrlEncodeString(_testEmail)));

        // Assert
        using var responseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = responseBody.RootElement;
        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);
    }

    private async Task CreateUserAsync(
        IdentityApplicationFactory factory,
        string testEmail,
        string userTwoFactor = null)
    {
        // Create Test User
        await factory.RegisterAsync(new RegisterRequestModel
        {
            Email = testEmail,
            MasterPasswordHash = _testPassword,
        });

        var userRepository = factory.Services.GetRequiredService<IUserRepository>();
        var user = await userRepository.GetByEmailAsync(testEmail);
        Assert.NotNull(user);

        var userService = factory.GetService<IUserService>();
        if (userTwoFactor != null)
        {
            user.TwoFactorProviders = userTwoFactor;
            await userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.Email);
            user = await userRepository.GetByEmailAsync(testEmail);
            Assert.NotNull(user.TwoFactorProviders);
        }
    }

    private async Task<IdentityApplicationFactory> CreateSsoOrganizationAndUserAsync(
        IdentityApplicationFactory factory,
        SsoConfigurationData ssoConfigurationData,
        string challenge,
        string testEmail,
        string orgTwoFactor = null,
        string userTwoFactor = null,
        Permissions permissions = null)
    {
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
            MasterPasswordHash = _testPassword,
        });

        var userRepository = factory.Services.GetRequiredService<IUserRepository>();
        var user = await userRepository.GetByEmailAsync(testEmail);
        Assert.NotNull(user);

        var userService = factory.GetService<IUserService>();
        if (userTwoFactor != null)
        {
            user.TwoFactorProviders = userTwoFactor;
            await userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.Email);
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

        if (orgTwoFactor != null)
        {
            factory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("globalSettings:Duo:AKey", "WJHB374KM3N5hglO9hniwbkibg$789EfbhNyLpNq1");
            });
        }

        // Register User to Organization
        var organizationUserRepository = factory.Services.GetRequiredService<IOrganizationUserRepository>();
        var orgUserPermissions =
            (permissions == null) ? null : JsonSerializer.Serialize(permissions, JsonHelpers.CamelCase);
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
