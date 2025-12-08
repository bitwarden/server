using System.Net;
using Bit.Api.AdminConsole.Public.Models;
using Bit.Api.AdminConsole.Public.Models.Request;
using Bit.Api.AdminConsole.Public.Models.Response;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Models.Public.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Public.Controllers;

public class MembersControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    // These will get set in `InitializeAsync` which is ran before all tests
    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public MembersControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        // Create the owner account
        _ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        // Create the organization
        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail, passwordManagerSeats: 10, paymentMethod: PaymentMethodType.Card);

        // Authorize with the organization api key
        await _loginHelper.LoginWithOrganizationApiKeyAsync(_organization.Id);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task List_Member_Success()
    {
        var (userEmail1, orgUser1) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.Custom, new Permissions { AccessImportExport = true, ManagePolicies = true, AccessReports = true });
        var (userEmail2, orgUser2) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.Owner);
        var (userEmail3, orgUser3) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.User);
        var (userEmail4, orgUser4) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.Admin);

        var collection1 = await OrganizationTestHelpers.CreateCollectionAsync(_factory, _organization.Id, "Test Collection 1", users:
        [
            new CollectionAccessSelection { Id = orgUser1.Id, ReadOnly = false, HidePasswords = false, Manage = true },
            new CollectionAccessSelection { Id = orgUser3.Id, ReadOnly = true, HidePasswords = false, Manage = false }
        ]);

        var collection2 = await OrganizationTestHelpers.CreateCollectionAsync(_factory, _organization.Id, "Test Collection 2", users:
        [
            new CollectionAccessSelection { Id = orgUser1.Id, ReadOnly = false, HidePasswords = true, Manage = false }
        ]);

        var response = await _client.GetAsync($"/public/members");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ListResponseModel<MemberResponseModel>>();
        Assert.NotNull(result?.Data);
        Assert.Equal(5, result.Data.Count());

        // The owner
        var ownerResult = result.Data.SingleOrDefault(m => m.Email == _ownerEmail && m.Type == OrganizationUserType.Owner);
        Assert.NotNull(ownerResult);
        Assert.Empty(ownerResult.Collections);

        // The custom user with collections
        var user1Result = result.Data.Single(m => m.Email == userEmail1);
        Assert.Equal(OrganizationUserType.Custom, user1Result.Type);
        AssertHelper.AssertPropertyEqual(
            new PermissionsModel { AccessImportExport = true, ManagePolicies = true, AccessReports = true },
            user1Result.Permissions);
        // Verify collections
        Assert.NotNull(user1Result.Collections);
        Assert.Equal(2, user1Result.Collections.Count());
        var user1Collection1 = user1Result.Collections.Single(c => c.Id == collection1.Id);
        Assert.False(user1Collection1.ReadOnly);
        Assert.False(user1Collection1.HidePasswords);
        Assert.True(user1Collection1.Manage);
        var user1Collection2 = user1Result.Collections.Single(c => c.Id == collection2.Id);
        Assert.False(user1Collection2.ReadOnly);
        Assert.True(user1Collection2.HidePasswords);
        Assert.False(user1Collection2.Manage);

        // The other owner
        var user2Result = result.Data.SingleOrDefault(m => m.Email == userEmail2 && m.Type == OrganizationUserType.Owner);
        Assert.NotNull(user2Result);
        Assert.Empty(user2Result.Collections);

        // The user with one collection
        var user3Result = result.Data.SingleOrDefault(m => m.Email == userEmail3 && m.Type == OrganizationUserType.User);
        Assert.NotNull(user3Result);
        Assert.NotNull(user3Result.Collections);
        Assert.Single(user3Result.Collections);
        var user3Collection1 = user3Result.Collections.Single(c => c.Id == collection1.Id);
        Assert.True(user3Collection1.ReadOnly);
        Assert.False(user3Collection1.HidePasswords);
        Assert.False(user3Collection1.Manage);

        // The admin with no collections
        var user4Result = result.Data.SingleOrDefault(m => m.Email == userEmail4 && m.Type == OrganizationUserType.Admin);
        Assert.NotNull(user4Result);
        Assert.Empty(user4Result.Collections);
    }

    [Fact]
    public async Task Get_CustomMember_Success()
    {
        var (email, orgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.Custom, new Permissions { AccessReports = true, ManageScim = true });

        var response = await _client.GetAsync($"/public/members/{orgUser.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MemberResponseModel>();
        Assert.NotNull(result);
        Assert.Equal(email, result.Email);

        Assert.Equal(OrganizationUserType.Custom, result.Type);
        AssertHelper.AssertPropertyEqual(new PermissionsModel { AccessReports = true, ManageScim = true },
            result.Permissions);
    }

    [Fact]
    public async Task Post_CustomMember_Success()
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        var request = new MemberCreateRequestModel
        {
            Email = email,
            Type = OrganizationUserType.Custom,
            ExternalId = "myCustomUser",
            Collections = [],
            Groups = []
        };

        var response = await _client.PostAsync("/public/members", JsonContent.Create(request));

        // Assert against the response
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MemberResponseModel>();
        Assert.NotNull(result);

        Assert.Equal(email, result.Email);
        Assert.Equal(OrganizationUserType.Custom, result.Type);
        Assert.Equal("myCustomUser", result.ExternalId);
        Assert.Empty(result.Collections);

        // Assert against the database values
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var orgUser = await organizationUserRepository.GetByIdAsync(result.Id);

        Assert.NotNull(orgUser);
        Assert.Equal(email, orgUser.Email);
        Assert.Equal(OrganizationUserType.Custom, orgUser.Type);
        Assert.Equal("myCustomUser", orgUser.ExternalId);
        Assert.Equal(OrganizationUserStatusType.Invited, orgUser.Status);
        Assert.Equal(_organization.Id, orgUser.OrganizationId);
    }

    [Fact]
    public async Task Put_CustomMember_Success()
    {
        var (email, orgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.User);

        var request = new MemberUpdateRequestModel
        {
            Type = OrganizationUserType.Custom,
            Permissions = new PermissionsModel
            {
                DeleteAnyCollection = true,
                EditAnyCollection = true,
                AccessEventLogs = true
            },
            ExternalId = "example",
            Collections = []
        };

        var response = await _client.PutAsync($"/public/members/{orgUser.Id}", JsonContent.Create(request));

        // Assert against the response
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MemberResponseModel>();
        Assert.NotNull(result);

        Assert.Equal(email, result.Email);
        Assert.Equal(OrganizationUserType.Custom, result.Type);
        Assert.Equal("example", result.ExternalId);
        AssertHelper.AssertPropertyEqual(
            new PermissionsModel { DeleteAnyCollection = true, EditAnyCollection = true, AccessEventLogs = true },
            result.Permissions);
        Assert.Empty(result.Collections);

        // Assert against the database values
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var updatedOrgUser = await organizationUserRepository.GetByIdAsync(result.Id);

        Assert.NotNull(updatedOrgUser);
        Assert.Equal(OrganizationUserType.Custom, updatedOrgUser.Type);
        Assert.Equal("example", updatedOrgUser.ExternalId);
        Assert.Equal(OrganizationUserStatusType.Confirmed, updatedOrgUser.Status);
        Assert.Equal(_organization.Id, updatedOrgUser.OrganizationId);
    }

    /// <summary>
    /// The Permissions property is optional and should not overwrite existing Permissions if not provided.
    /// This is to preserve backwards compatibility with existing usage.
    /// </summary>
    [Fact]
    public async Task Put_ExistingCustomMember_NullPermissions_DoesNotOverwritePermissions()
    {
        var (email, orgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.Custom, new Permissions { CreateNewCollections = true, ManageScim = true, ManageGroups = true, ManageUsers = true });

        var request = new MemberUpdateRequestModel
        {
            Type = OrganizationUserType.Custom,
            ExternalId = "example",
            Collections = []
        };

        var response = await _client.PutAsync($"/public/members/{orgUser.Id}", JsonContent.Create(request));

        // Assert against the response
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MemberResponseModel>();
        Assert.NotNull(result);

        Assert.Equal(OrganizationUserType.Custom, result.Type);
        AssertHelper.AssertPropertyEqual(
            new PermissionsModel { CreateNewCollections = true, ManageScim = true, ManageGroups = true, ManageUsers = true },
            result.Permissions);

        // Assert against the database values
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var updatedOrgUser = await organizationUserRepository.GetByIdAsync(result.Id);

        Assert.NotNull(updatedOrgUser);
        Assert.Equal(OrganizationUserType.Custom, updatedOrgUser.Type);
        AssertHelper.AssertPropertyEqual(
            new Permissions { CreateNewCollections = true, ManageScim = true, ManageGroups = true, ManageUsers = true },
            orgUser.GetPermissions());
    }
}
