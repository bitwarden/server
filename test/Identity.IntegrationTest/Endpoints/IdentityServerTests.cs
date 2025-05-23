using System.Text.Json;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Platform.Installations;
using Bit.Core.Repositories;
using Bit.Core.Test.Auth.AutoFixture;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bit.Identity.IntegrationTest.Endpoints;

[SutProviderCustomize]
public class IdentityServerTests : IClassFixture<IdentityApplicationFactory>
{
    private const int SecondsInMinute = 60;
    private const int MinutesInHour = 60;
    private const int SecondsInHour = SecondsInMinute * MinutesInHour;
    private readonly IdentityApplicationFactory _factory;

    public IdentityServerTests(IdentityApplicationFactory factory)
    {
        _factory = factory;
        ReinitializeDbForTests(_factory);
    }

    [Fact]
    public async Task WellKnownEndpoint_Success()
    {
        var context = await _factory.Server.GetAsync("/.well-known/openid-configuration");

        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var endpointRoot = body.RootElement;

        // WARNING: Edits to this file should NOT just be made to "get the test to work" they should be made when intentional
        // changes were made to this endpoint and proper testing will take place to ensure clients are backwards compatible
        // or loss of functionality is properly noted.
        await using var fs = File.OpenRead("openid-configuration.json");
        using var knownConfiguration = await JsonSerializer.DeserializeAsync<JsonDocument>(fs);
        var knownConfigurationRoot = knownConfiguration.RootElement;

        AssertHelper.AssertEqualJson(endpointRoot, knownConfigurationRoot);
    }

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_GrantTypePassword_Success(RegisterFinishRequestModel requestModel)
    {
        var localFactory = new IdentityApplicationFactory();
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        var context = await PostLoginAsync(localFactory.Server, user, requestModel.MasterPasswordHash,
            context => context.SetAuthEmail(user.Email));

        using var body = await AssertDefaultTokenBodyAsync(context);
        var root = body.RootElement;
        AssertRefreshTokenExists(root);
        AssertHelper.AssertJsonProperty(root, "ForcePasswordReset", JsonValueKind.False);
        AssertHelper.AssertJsonProperty(root, "ResetMasterPassword", JsonValueKind.False);
        var kdf = AssertHelper.AssertJsonProperty(root, "Kdf", JsonValueKind.Number).GetInt32();
        Assert.Equal(0, kdf);
        var kdfIterations = AssertHelper.AssertJsonProperty(root, "KdfIterations", JsonValueKind.Number).GetInt32();
        Assert.Equal(AuthConstants.PBKDF2_ITERATIONS.Default, kdfIterations);
        AssertUserDecryptionOptions(root);
    }

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_GrantTypePassword_NoAuthEmailHeader_Fails(
        RegisterFinishRequestModel requestModel)
    {
        requestModel.Email = "test+noauthemailheader@email.com";

        var localFactory = new IdentityApplicationFactory();
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        var context = await PostLoginAsync(localFactory.Server, user, requestModel.MasterPasswordHash, null);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var error = AssertHelper.AssertJsonProperty(root, "error", JsonValueKind.String).GetString();
        Assert.Equal("invalid_grant", error);
        AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String);
    }

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_GrantTypePassword_InvalidBase64AuthEmailHeader_Fails(
        RegisterFinishRequestModel requestModel)
    {
        requestModel.Email = "test+badauthheader@email.com";

        var localFactory = new IdentityApplicationFactory();
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        var context = await PostLoginAsync(localFactory.Server, user, requestModel.MasterPasswordHash,
            context => context.Request.Headers.Append("Auth-Email", "bad_value"));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var error = AssertHelper.AssertJsonProperty(root, "error", JsonValueKind.String).GetString();
        Assert.Equal("invalid_grant", error);
        AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String);
    }

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_GrantTypePassword_WrongAuthEmailHeader_Fails(
        RegisterFinishRequestModel requestModel)
    {
        requestModel.Email = "test+badauthheader@email.com";

        var localFactory = new IdentityApplicationFactory();
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        var context = await PostLoginAsync(localFactory.Server, user, requestModel.MasterPasswordHash,
            context => context.SetAuthEmail("bad_value"));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var error = AssertHelper.AssertJsonProperty(root, "error", JsonValueKind.String).GetString();
        Assert.Equal("invalid_grant", error);
        AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String);
    }

    [Theory, RegisterFinishRequestModelCustomize]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task TokenEndpoint_GrantTypePassword_WithAllUserTypes_WithSsoPolicyDisabled_WithEnforceSsoPolicyForAllUsersTrue_Success(
       OrganizationUserType organizationUserType, RegisterFinishRequestModel requestModel, Guid organizationId, int generatedUsername)
    {
        requestModel.Email = $"{generatedUsername}@example.com";

        var localFactory = new IdentityApplicationFactory();
        var server = localFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("globalSettings:sso:enforceSsoPolicyForAllUsers", "true");
        }).Server;
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        await CreateOrganizationWithSsoPolicyAsync(localFactory,
            organizationId, user.Email, organizationUserType, ssoPolicyEnabled: false);

        var context = await PostLoginAsync(server, user, requestModel.MasterPasswordHash,
                context => context.SetAuthEmail(user.Email));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Theory, RegisterFinishRequestModelCustomize]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task TokenEndpoint_GrantTypePassword_WithAllUserTypes_WithSsoPolicyDisabled_WithEnforceSsoPolicyForAllUsersFalse_Success(
        OrganizationUserType organizationUserType, RegisterFinishRequestModel requestModel, Guid organizationId, int generatedUsername)
    {
        requestModel.Email = $"{generatedUsername}@example.com";

        var localFactory = new IdentityApplicationFactory();
        var server = localFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("globalSettings:sso:enforceSsoPolicyForAllUsers", "false");

        }).Server;
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        await CreateOrganizationWithSsoPolicyAsync(
            localFactory, organizationId, user.Email, organizationUserType, ssoPolicyEnabled: false);

        var context = await PostLoginAsync(server, user, requestModel.MasterPasswordHash,
                context => context.SetAuthEmail(user.Email));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Theory, RegisterFinishRequestModelCustomize]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task TokenEndpoint_GrantTypePassword_WithAllUserTypes_WithSsoPolicyEnabled_WithEnforceSsoPolicyForAllUsersTrue_Throw(
        OrganizationUserType organizationUserType, RegisterFinishRequestModel requestModel, Guid organizationId, int generatedUsername)
    {
        requestModel.Email = $"{generatedUsername}@example.com";

        var localFactory = new IdentityApplicationFactory();
        var server = localFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("globalSettings:sso:enforceSsoPolicyForAllUsers", "true");
        }).Server;
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        await CreateOrganizationWithSsoPolicyAsync(localFactory, organizationId, user.Email, organizationUserType, ssoPolicyEnabled: true);

        var context = await PostLoginAsync(server, user, requestModel.MasterPasswordHash,
                context => context.SetAuthEmail(user.Email));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        await AssertRequiredSsoAuthenticationResponseAsync(context);
    }

    [Theory, RegisterFinishRequestModelCustomize]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task TokenEndpoint_GrantTypePassword_WithOwnerOrAdmin_WithSsoPolicyEnabled_WithEnforceSsoPolicyForAllUsersFalse_Success(
        OrganizationUserType organizationUserType, RegisterFinishRequestModel requestModel, Guid organizationId, int generatedUsername)
    {
        requestModel.Email = $"{generatedUsername}@example.com";

        var localFactory = new IdentityApplicationFactory();
        var server = localFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("globalSettings:sso:enforceSsoPolicyForAllUsers", "false");

        }).Server;
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        await CreateOrganizationWithSsoPolicyAsync(localFactory, organizationId, user.Email, organizationUserType, ssoPolicyEnabled: true);

        var context = await PostLoginAsync(server, user, requestModel.MasterPasswordHash,
            context => context.SetAuthEmail(user.Email));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Theory, RegisterFinishRequestModelCustomize]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task TokenEndpoint_GrantTypePassword_WithNonOwnerOrAdmin_WithSsoPolicyEnabled_WithEnforceSsoPolicyForAllUsersFalse_Throws(
        OrganizationUserType organizationUserType, RegisterFinishRequestModel requestModel, Guid organizationId, int generatedUsername)
    {
        requestModel.Email = $"{generatedUsername}@example.com";

        var localFactory = new IdentityApplicationFactory();
        var server = localFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("globalSettings:sso:enforceSsoPolicyForAllUsers", "false");

        }).Server;
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        await CreateOrganizationWithSsoPolicyAsync(localFactory, organizationId, user.Email, organizationUserType, ssoPolicyEnabled: true);

        var context = await PostLoginAsync(server, user, requestModel.MasterPasswordHash,
                context => context.SetAuthEmail(user.Email));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        await AssertRequiredSsoAuthenticationResponseAsync(context);
    }

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_GrantTypeRefreshToken_Success(RegisterFinishRequestModel requestModel)
    {
        var localFactory = new IdentityApplicationFactory();

        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        var (_, refreshToken) = await localFactory.TokenFromPasswordAsync(
            requestModel.Email, requestModel.MasterPasswordHash);

        var context = await localFactory.Server.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "client_id", "web" },
                { "refresh_token", refreshToken },
            }));

        using var body = await AssertDefaultTokenBodyAsync(context);
        AssertRefreshTokenExists(body.RootElement);
    }

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_GrantTypeClientCredentials_Success(RegisterFinishRequestModel model)
    {
        var localFactory = new IdentityApplicationFactory();
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(model);

        var context = await localFactory.Server.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "client_id", $"user.{user.Id}" },
                { "client_secret", user.ApiKey },
                { "scope", "api" },
                { "DeviceIdentifier", IdentityApplicationFactory.DefaultDeviceIdentifier },
                { "DeviceType", DeviceTypeAsString(DeviceType.FirefoxBrowser) },
                { "DeviceName", "firefox" },
            })
        );

        await AssertDefaultTokenBodyAsync(context, "api");
    }

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_GrantTypeClientCredentials_AsLegacyUser_NotOnWebClient_Fails(
        RegisterFinishRequestModel model,
        string deviceId)
    {
        var localFactory = new IdentityApplicationFactory();
        var server = localFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("globalSettings:launchDarkly:flagValues:block-legacy-users", "true");
        }).Server;

        model.Email = "test+tokenclientcredentials@email.com";
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(model);

        // Modify user to be legacy user. We have to fetch the user again to put it in the ef-context
        // so when we modify change tracking will save the changes.
        var database = localFactory.GetDatabaseContext();
        user = await database.Users
            .FirstAsync(u => u.Email == model.Email);
        user.Key = null;
        await database.SaveChangesAsync();

        var context = await server.PostAsync("/connect/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                { "scope", "api offline_access" },
                { "client_id", "browser" },
                { "deviceType", DeviceTypeAsString(DeviceType.ChromeBrowser) },
                { "deviceIdentifier", deviceId },
                { "deviceName", "chrome" },
                { "grant_type", "password" },
                { "username", model.Email },
                { "password", model.MasterPasswordHash },
            }), context => context.SetAuthEmail(model.Email));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        var errorBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var error = AssertHelper.AssertJsonProperty(errorBody.RootElement, "ErrorModel", JsonValueKind.Object);
        var message = AssertHelper.AssertJsonProperty(error, "Message", JsonValueKind.String).GetString();
        Assert.StartsWith("Encryption key migration is required.", message);
    }


    // TODO: use this as example to call send token endpoint
    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypeClientCredentials_AsOrganization_Success(Organization organization, Bit.Core.Entities.OrganizationApiKey organizationApiKey)
    {
        var orgRepo = _factory.Services.GetRequiredService<IOrganizationRepository>();
        organization.Enabled = true;
        organization.UseApi = true;
        organization = await orgRepo.CreateAsync(organization);
        organizationApiKey.OrganizationId = organization.Id;
        organizationApiKey.Type = OrganizationApiKeyType.Default;

        var orgApiKeyRepo = _factory.Services.GetRequiredService<IOrganizationApiKeyRepository>();
        await orgApiKeyRepo.CreateAsync(organizationApiKey);

        var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", $"organization.{organization.Id}" },
            { "client_secret", organizationApiKey.ApiKey },
            { "scope", "api.organization" },
        }));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        await AssertDefaultTokenBodyAsync(context, "api.organization");
    }

    [Fact]
    public async Task TokenEndpoint_GrantTypeClientCredentials_AsOrganization_BadOrgId_Fails()
    {
        var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", "organization.bad_guid_zz&" },
            { "client_secret", "something" },
            { "scope", "api.organization" },
        }));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        var errorBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var error = AssertHelper.AssertJsonProperty(errorBody.RootElement, "error", JsonValueKind.String).GetString();
        Assert.Equal("invalid_client", error);
    }

    /// <summary>
    /// This test currently does not test any code that is not covered by other tests but
    /// it shows that we probably have some dead code in <see cref="ClientStore"/>
    /// for installation, organization, and user they split on a <c>'.'</c> but have already checked that at least one
    /// <c>'.'</c> exists in the <c>client_id</c> by checking it with <see cref="string.StartsWith(string)"/>
    /// I believe that idParts.Length > 1 will ALWAYS return true
    /// </summary>
    [Fact]
    public async Task TokenEndpoint_GrantTypeClientCredentials_AsOrganization_NoIdPart_Fails()
    {
        var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", "organization." },
            { "client_secret", "something" },
            { "scope", "api.organization" },
        }));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        var errorBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var error = AssertHelper.AssertJsonProperty(errorBody.RootElement, "error", JsonValueKind.String).GetString();
        Assert.Equal("invalid_client", error);
    }

    [Fact]
    public async Task TokenEndpoint_GrantTypeClientCredentials_AsOrganization_OrgDoesNotExist_Fails()
    {
        var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", $"organization.{Guid.NewGuid()}" },
            { "client_secret", "something" },
            { "scope", "api.organization" },
        }));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        var errorBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var error = AssertHelper.AssertJsonProperty(errorBody.RootElement, "error", JsonValueKind.String).GetString();
        Assert.Equal("invalid_client", error);
    }

    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypeClientCredentials_AsInstallation_InstallationExists_Succeeds(Installation installation)
    {
        var installationRepo = _factory.Services.GetRequiredService<IInstallationRepository>();
        installation = await installationRepo.CreateAsync(installation);

        var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", $"installation.{installation.Id}" },
            { "client_secret", installation.Key },
            { "scope", "api.push" },
        }));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        await AssertDefaultTokenBodyAsync(context, "api.push", 24 * SecondsInHour);
    }

    [Fact]
    public async Task TokenEndpoint_GrantTypeClientCredentials_AsInstallation_InstallationDoesNotExist_Fails()
    {
        var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", $"installation.{Guid.NewGuid()}" },
            { "client_secret", "something" },
            { "scope", "api.push" },
        }));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        var errorBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var error = AssertHelper.AssertJsonProperty(errorBody.RootElement, "error", JsonValueKind.String).GetString();
        Assert.Equal("invalid_client", error);
    }

    [Fact]
    public async Task TokenEndpoint_GrantTypeClientCredentials_AsInstallation_BadInstallationId_Fails()
    {
        var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", "organization.bad_guid_zz&" },
            { "client_secret", "something" },
            { "scope", "api.organization" },
        }));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        var errorBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var error = AssertHelper.AssertJsonProperty(errorBody.RootElement, "error", JsonValueKind.String).GetString();
        Assert.Equal("invalid_client", error);
    }

    /// <inheritdoc cref="TokenEndpoint_GrantTypeClientCredentials_AsOrganization_NoIdPart_Fails"/>
    [Fact]
    public async Task TokenEndpoint_GrantTypeClientCredentials_AsInstallation_NoIdPart_Fails()
    {
        var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", "installation." },
            { "client_secret", "something" },
            { "scope", "api.push" },
        }));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        var errorBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var error = AssertHelper.AssertJsonProperty(errorBody.RootElement, "error", JsonValueKind.String).GetString();
        Assert.Equal("invalid_client", error);
    }

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_TooQuickInOneSecond_BlockRequest(
        RegisterFinishRequestModel requestModel)
    {
        const int AmountInOneSecondAllowed = 10;

        // The rule we are testing is 10 requests in 1 second
        requestModel.Email = "test+ratelimiting@email.com";

        var localFactory = new IdentityApplicationFactory();
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        var database = localFactory.GetDatabaseContext();
        user = await database.Users
            .FirstAsync(u => u.Email == user.Email);

        var tasks = new Task<HttpContext>[AmountInOneSecondAllowed + 1];

        for (var i = 0; i < AmountInOneSecondAllowed + 1; i++)
        {
            // Queue all the amount of calls allowed plus 1
            tasks[i] = MakeRequest();
        }

        var responses = (await Task.WhenAll(tasks)).ToList();
        var blockResponses = responses.Where(c => c.Response.StatusCode == StatusCodes.Status429TooManyRequests);

        Assert.True(blockResponses.Count() > 0);

        Task<HttpContext> MakeRequest()
        {
            return _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "scope", "api offline_access" },
                { "client_id", "web" },
                { "deviceType", DeviceTypeAsString(DeviceType.FirefoxBrowser) },
                { "deviceIdentifier", IdentityApplicationFactory.DefaultDeviceIdentifier },
                { "deviceName", "firefox" },
                { "grant_type", "password" },
                { "username", user.Email},
                { "password", "master_password_hash" },
            }), context => context.SetAuthEmail(user.Email).SetIp("1.1.1.2"));
        }
    }

    private async Task<HttpContext> PostLoginAsync(
        TestServer server, User user, string MasterPasswordHash, Action<HttpContext> extraConfiguration)
    {
        return await server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", DeviceTypeAsString(DeviceType.FirefoxBrowser) },
            { "deviceIdentifier", IdentityApplicationFactory.DefaultDeviceIdentifier },
            { "deviceName", "firefox" },
            { "grant_type", "password" },
            { "username", user.Email },
            { "password", MasterPasswordHash },
        }), extraConfiguration);
    }

    private async Task CreateOrganizationWithSsoPolicyAsync(
        IdentityApplicationFactory localFactory,
        Guid organizationId,
        string username, OrganizationUserType organizationUserType, bool ssoPolicyEnabled)
    {
        var userRepository = localFactory.Services.GetService<IUserRepository>();
        var organizationRepository = localFactory.Services.GetService<IOrganizationRepository>();
        var organizationUserRepository = localFactory.Services.GetService<IOrganizationUserRepository>();
        var policyRepository = localFactory.Services.GetService<IPolicyRepository>();

        var organization = new Organization
        {
            Id = organizationId,
            Name = $"Org Name | {organizationId}",
            Enabled = true,
            UseSso = ssoPolicyEnabled,
            Plan = "Enterprise",
            UsePolicies = true,
            BillingEmail = $"billing-email+{organizationId}@example.com",
        };
        await organizationRepository.CreateAsync(organization);

        var user = await userRepository.GetByEmailAsync(username);
        var organizationUser = new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = organizationUserType
        };
        await organizationUserRepository.CreateAsync(organizationUser);

        var ssoPolicy = new Policy { OrganizationId = organization.Id, Type = PolicyType.RequireSso, Enabled = ssoPolicyEnabled };
        await policyRepository.CreateAsync(ssoPolicy);
    }

    private static string DeviceTypeAsString(DeviceType deviceType)
    {
        return ((int)deviceType).ToString();
    }

    private static async Task<JsonDocument> AssertDefaultTokenBodyAsync(HttpContext httpContext, string expectedScope = "api offline_access", int expectedExpiresIn = SecondsInHour * 1)
    {
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(httpContext);
        var root = body.RootElement;

        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        AssertAccessTokenExists(root);
        AssertExpiresIn(root, expectedExpiresIn);
        AssertTokenType(root);
        AssertScope(root, expectedScope);
        return body;
    }

    private static void AssertTokenType(JsonElement tokenResponse)
    {
        var tokenTypeProperty = AssertHelper.AssertJsonProperty(tokenResponse, "token_type", JsonValueKind.String).GetString();
        Assert.Equal("Bearer", tokenTypeProperty);
    }

    private static int AssertExpiresIn(JsonElement tokenResponse, int expectedExpiresIn = 3600)
    {
        var expiresIn = AssertHelper.AssertJsonProperty(tokenResponse, "expires_in", JsonValueKind.Number).GetInt32();
        Assert.Equal(expectedExpiresIn, expiresIn);
        return expiresIn;
    }

    private static string AssertAccessTokenExists(JsonElement tokenResponse)
    {
        return AssertHelper.AssertJsonProperty(tokenResponse, "access_token", JsonValueKind.String).GetString();
    }

    private static string AssertRefreshTokenExists(JsonElement tokenResponse)
    {
        return AssertHelper.AssertJsonProperty(tokenResponse, "refresh_token", JsonValueKind.String).GetString();
    }

    private static string AssertScopeExists(JsonElement tokenResponse)
    {
        return AssertHelper.AssertJsonProperty(tokenResponse, "scope", JsonValueKind.String).GetString();
    }

    private static void AssertScope(JsonElement tokenResponse, string expectedScope)
    {
        var actualScope = AssertScopeExists(tokenResponse);
        Assert.Equal(expectedScope, actualScope);
    }

    private static async Task AssertRequiredSsoAuthenticationResponseAsync(HttpContext context)
    {
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var error = AssertHelper.AssertJsonProperty(root, "error", JsonValueKind.String).GetString();
        Assert.Equal("invalid_grant", error);
        var errorDescription = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.StartsWith("sso authentication", errorDescription.ToLowerInvariant());
    }

    private static void AssertUserDecryptionOptions(JsonElement tokenResponse)
    {
        var userDecryptionOptions = AssertHelper.AssertJsonProperty(tokenResponse, "UserDecryptionOptions", JsonValueKind.Object)
            .EnumerateObject();

        Assert.Collection(userDecryptionOptions,
            (prop) => { Assert.Equal("HasMasterPassword", prop.Name); Assert.Equal(JsonValueKind.True, prop.Value.ValueKind); },
            (prop) => { Assert.Equal("Object", prop.Name); Assert.Equal("userDecryptionOptions", prop.Value.GetString()); });
    }

    private void ReinitializeDbForTests(IdentityApplicationFactory factory)
    {
        var databaseContext = factory.GetDatabaseContext();
        databaseContext.Policies.RemoveRange(databaseContext.Policies);
        databaseContext.OrganizationUsers.RemoveRange(databaseContext.OrganizationUsers);
        databaseContext.Organizations.RemoveRange(databaseContext.Organizations);
        databaseContext.Users.RemoveRange(databaseContext.Users);
        databaseContext.SaveChanges();
    }
}
