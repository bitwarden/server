using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Test.Auth.AutoFixture;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.TokenEndpointTestHelpers;

namespace Bit.Identity.IntegrationTest.Login.RequireSso;

[SutProviderCustomize]
public class RequireSsoLoginTests
{
    [Theory, RegisterFinishRequestModelCustomize]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task TokenEndpoint_GrantTypePassword_WithAllUserTypes_WithSsoPolicyDisabled_WithEnforceSsoPolicyForAllUsersTrue_Success(
       OrganizationUserType organizationUserType, RegisterFinishRequestModel requestModel, Guid organizationId, int generatedUsername)
    {
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        requestModel.Email = $"{generatedUsername}@example.com";

        var localFactory = new IdentityApplicationFactory();
        var server = localFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("globalSettings:sso:enforceSsoPolicyForAllUsers", "true");
        }).Server;
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        await CreateOrganizationWithSsoPolicyAsync(localFactory,
            organizationId, user.Email, organizationUserType, ssoPolicyEnabled: false);

        var context = await PostLoginAsync(server, user, requestModel.MasterPasswordHash);

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
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        requestModel.Email = $"{generatedUsername}@example.com";

        var localFactory = new IdentityApplicationFactory();
        var server = localFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("globalSettings:sso:enforceSsoPolicyForAllUsers", "false");

        }).Server;
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        await CreateOrganizationWithSsoPolicyAsync(
            localFactory, organizationId, user.Email, organizationUserType, ssoPolicyEnabled: false);

        var context = await PostLoginAsync(server, user, requestModel.MasterPasswordHash);

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
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        requestModel.Email = $"{generatedUsername}@example.com";

        var localFactory = new IdentityApplicationFactory();
        var server = localFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("globalSettings:sso:enforceSsoPolicyForAllUsers", "true");
        }).Server;
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        await CreateOrganizationWithSsoPolicyAsync(localFactory, organizationId, user.Email, organizationUserType, ssoPolicyEnabled: true);

        var context = await PostLoginAsync(server, user, requestModel.MasterPasswordHash);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        await AssertRequiredSsoAuthenticationResponseAsync(context);
    }

    [Theory, RegisterFinishRequestModelCustomize]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task TokenEndpoint_GrantTypePassword_WithOwnerOrAdmin_WithSsoPolicyEnabled_WithEnforceSsoPolicyForAllUsersFalse_Success(
        OrganizationUserType organizationUserType, RegisterFinishRequestModel requestModel, Guid organizationId, int generatedUsername)
    {
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        requestModel.Email = $"{generatedUsername}@example.com";

        var localFactory = new IdentityApplicationFactory();
        var server = localFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("globalSettings:sso:enforceSsoPolicyForAllUsers", "false");

        }).Server;
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        await CreateOrganizationWithSsoPolicyAsync(localFactory, organizationId, user.Email, organizationUserType, ssoPolicyEnabled: true);

        var context = await PostLoginAsync(server, user, requestModel.MasterPasswordHash);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Theory, RegisterFinishRequestModelCustomize]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task TokenEndpoint_GrantTypePassword_WithNonOwnerOrAdmin_WithSsoPolicyEnabled_WithEnforceSsoPolicyForAllUsersFalse_Throws(
        OrganizationUserType organizationUserType, RegisterFinishRequestModel requestModel, Guid organizationId, int generatedUsername)
    {
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        requestModel.Email = $"{generatedUsername}@example.com";

        var localFactory = new IdentityApplicationFactory();
        var server = localFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("globalSettings:sso:enforceSsoPolicyForAllUsers", "false");

        }).Server;
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        await CreateOrganizationWithSsoPolicyAsync(localFactory, organizationId, user.Email, organizationUserType, ssoPolicyEnabled: true);

        var context = await PostLoginAsync(server, user, requestModel.MasterPasswordHash);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        await AssertRequiredSsoAuthenticationResponseAsync(context);
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

    private static async Task AssertRequiredSsoAuthenticationResponseAsync(HttpContext context)
    {
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var error = AssertHelper.AssertJsonProperty(root, "error", JsonValueKind.String).GetString();
        Assert.Equal("invalid_grant", error);
        var errorDescription = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.StartsWith("sso authentication", errorDescription.ToLowerInvariant());
    }
}
