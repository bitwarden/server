using System.Net;
using Bit.Api.AdminConsole.Public.Models.Request;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Errors;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Public.Controllers;

public class ScopedOrganizationApiKeyAdminConsoleTests
    : IClassFixture<ScopedOrganizationApiKeysApiApplicationFactory>, IAsyncLifetime
{
    private readonly ScopedOrganizationApiKeysApiApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly LoginHelper _loginHelper;

    private Organization _organization = null!;

    public ScopedOrganizationApiKeyAdminConsoleTests(ScopedOrganizationApiKeysApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        var ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually,
            ownerEmail: ownerEmail, passwordManagerSeats: 10, paymentMethod: PaymentMethodType.Card);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task MembersReadToken_ListMembers_ReturnsOk()
    {
        await _loginHelper.LoginWithScopedOrganizationApiKeyAsync(_organization.Id, ApiScopes.ApiOrganizationMembersRead);

        var response = await _client.GetAsync("public/members");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MembersReadToken_InviteMember_ReturnsForbidden()
    {
        await _loginHelper.LoginWithScopedOrganizationApiKeyAsync(_organization.Id, ApiScopes.ApiOrganizationMembersRead);

        var response = await _client.PostAsJsonAsync("public/members", NewInvite(OrganizationUserType.User));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MembersWriteToken_InviteUser_CreatesUserMember()
    {
        await _loginHelper.LoginWithScopedOrganizationApiKeyAsync(_organization.Id, ApiScopes.ApiOrganizationMembersWrite);
        var invite = NewInvite(OrganizationUserType.User);

        var response = await _client.PostAsJsonAsync("public/members", invite);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var member = Assert.Single(await GetMembersAsync(), m => m.Email == invite.Email);
        Assert.Equal(OrganizationUserType.User, member.Type);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.Custom)]
    public async Task MembersWriteToken_InviteElevatedRole_IsRejectedAndCreatesNoMember(OrganizationUserType type)
    {
        await _loginHelper.LoginWithScopedOrganizationApiKeyAsync(_organization.Id, ApiScopes.ApiOrganizationMembersWrite);
        var invite = NewInvite(type);

        var response = await _client.PostAsJsonAsync("public/members", invite);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(ScopedApiKeyCanOnlyInviteUsersError.Code, await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(await GetMembersAsync(), m => m.Email == invite.Email);
    }

    [Fact]
    public async Task MembersWriteToken_UpdateUserToAdmin_IsRejectedAndMemberStaysUser()
    {
        var (_, member) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.User);
        await _loginHelper.LoginWithScopedOrganizationApiKeyAsync(_organization.Id, ApiScopes.ApiOrganizationMembersWrite);

        var response = await _client.PutAsJsonAsync($"public/members/{member.Id}", UpdateToType(OrganizationUserType.Admin));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(new ScopedApiKeyCanOnlyManageUsers().Message, await response.Content.ReadAsStringAsync());
        var stored = await _factory.GetService<IOrganizationUserRepository>().GetByIdAsync(member.Id);
        Assert.Equal(OrganizationUserType.User, stored!.Type);
    }

    [Fact]
    public async Task MembersWriteToken_RemoveAdmin_IsRejectedAndMemberRemains()
    {
        var (_, member) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.Admin);
        await _loginHelper.LoginWithScopedOrganizationApiKeyAsync(_organization.Id, ApiScopes.ApiOrganizationMembersWrite);

        var response = await _client.DeleteAsync($"public/members/{member.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(new ScopedApiKeyCanOnlyManageUsers().Message, await response.Content.ReadAsStringAsync());
        Assert.NotNull(await _factory.GetService<IOrganizationUserRepository>().GetByIdAsync(member.Id));
    }

    [Fact]
    public async Task LegacyToken_UpdateUserToAdmin_PromotesMember()
    {
        var (_, member) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.User);
        await _loginHelper.LoginWithOrganizationApiKeyAsync(_organization.Id);

        var response = await _client.PutAsJsonAsync($"public/members/{member.Id}", UpdateToType(OrganizationUserType.Admin));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await _factory.GetService<IOrganizationUserRepository>().GetByIdAsync(member.Id);
        Assert.Equal(OrganizationUserType.Admin, stored!.Type);
    }

    [Fact]
    public async Task PoliciesReadToken_UpdatePolicy_ReturnsForbidden()
    {
        await _loginHelper.LoginWithScopedOrganizationApiKeyAsync(_organization.Id, ApiScopes.ApiOrganizationPoliciesRead);
        var request = new PolicyUpdateRequestModel { Enabled = true };

        var response = await _client.PutAsJsonAsync($"public/policies/{PolicyType.TwoFactorAuthentication}", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MembersWriteTokenWithoutGroupsWrite_Import_ReturnsForbidden()
    {
        await _loginHelper.LoginWithScopedOrganizationApiKeyAsync(_organization.Id, ApiScopes.ApiOrganizationMembersWrite);

        var response = await _client.PostAsJsonAsync("public/organization/import", NewUserImport());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MembersAndGroupsWriteToken_ImportUsers_InvitesUserMember()
    {
        await _loginHelper.LoginWithScopedOrganizationApiKeyAsync(_organization.Id,
            ApiScopes.ApiOrganizationMembersWrite, ApiScopes.ApiOrganizationGroupsWrite);
        var import = NewUserImport();

        var response = await _client.PostAsJsonAsync("public/organization/import", import);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var member = Assert.Single(await GetMembersAsync(), m => m.Email == import.Members.Single().Email);
        Assert.Equal(OrganizationUserType.User, member.Type);
    }

    private static MemberCreateRequestModel NewInvite(OrganizationUserType type) => new()
    {
        Email = $"integration-test{Guid.NewGuid()}@example.com",
        Type = type,
    };

    private static MemberUpdateRequestModel UpdateToType(OrganizationUserType type) => new()
    {
        Type = type,
        Collections = [],
    };

    private static OrganizationImportRequestModel NewUserImport() => new()
    {
        Groups = [],
        Members =
        [
            new OrganizationImportRequestModel.OrganizationImportMemberRequestModel
            {
                Email = $"integration-test{Guid.NewGuid()}@example.com",
                ExternalId = $"external-{Guid.NewGuid()}",
            },
        ],
        OverwriteExisting = false,
    };

    private async Task<ICollection<OrganizationUser>> GetMembersAsync() =>
        await _factory.GetService<IOrganizationUserRepository>().GetManyByOrganizationAsync(_organization.Id, null);
}
