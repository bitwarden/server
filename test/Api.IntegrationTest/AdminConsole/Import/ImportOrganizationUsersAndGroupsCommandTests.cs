using System.Net;
using Bit.Api.AdminConsole.Public.Models.Request;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Import;

public class ImportOrganizationUsersAndGroupsCommandTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public ImportOrganizationUsersAndGroupsCommandTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
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
    public async Task Import_Existing_Organization_User_Succeeds()
    {
        var (email, ou) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.User);

        var externalId = Guid.NewGuid().ToString();
        var request = new OrganizationImportRequestModel();
        request.LargeImport = false;
        request.OverwriteExisting = false;
        request.Groups = [];
        request.Members = [
            new OrganizationImportRequestModel.OrganizationImportMemberRequestModel
            {
                Email = email,
                ExternalId = externalId,
                Deleted = false
            }
        ];

        var response = await _client.PostAsync($"/public/organization/import", JsonContent.Create(request));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert against the database values
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var orgUser = await organizationUserRepository.GetByIdAsync(ou.Id);

        Assert.NotNull(orgUser);
        Assert.Equal(ou.Id, orgUser.Id);
        Assert.Equal(email, orgUser.Email);
        Assert.Equal(OrganizationUserType.User, orgUser.Type);
        Assert.Equal(externalId, orgUser.ExternalId);
        Assert.Equal(OrganizationUserStatusType.Confirmed, orgUser.Status);
        Assert.Equal(_organization.Id, orgUser.OrganizationId);

    }

    [Fact]
    public async Task Import_New_Organization_User_Succeeds()
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);

        var externalId = Guid.NewGuid().ToString();
        var request = new OrganizationImportRequestModel();
        request.LargeImport = false;
        request.OverwriteExisting = false;
        request.Groups = [];
        request.Members = [
            new OrganizationImportRequestModel.OrganizationImportMemberRequestModel
            {
                Email = email,
                ExternalId = externalId,
                Deleted = false
            }
        ];

        var response = await _client.PostAsync($"/public/organization/import", JsonContent.Create(request));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert against the database values
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var orgUser = await organizationUserRepository.GetByOrganizationEmailAsync(_organization.Id, email);

        Assert.NotNull(orgUser);
        Assert.Equal(email, orgUser.Email);
        Assert.Equal(OrganizationUserType.User, orgUser.Type);
        Assert.Equal(externalId, orgUser.ExternalId);
        Assert.Equal(OrganizationUserStatusType.Invited, orgUser.Status);
        Assert.Equal(_organization.Id, orgUser.OrganizationId);
    }

    [Fact]
    public async Task Import_New_And_Existing_Organization_Users_Succeeds()
    {
        // Existing organization user
        var (existingEmail, ou) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.User);
        var existingExternalId = Guid.NewGuid().ToString();

        // New organization user
        var newEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(newEmail);
        var newExternalId = Guid.NewGuid().ToString();

        var request = new OrganizationImportRequestModel();
        request.LargeImport = false;
        request.OverwriteExisting = false;
        request.Groups = [];
        request.Members = [
            new OrganizationImportRequestModel.OrganizationImportMemberRequestModel
            {
                Email = existingEmail,
                ExternalId = existingExternalId,
                Deleted = false
            },
            new OrganizationImportRequestModel.OrganizationImportMemberRequestModel
            {
                Email = newEmail,
                ExternalId = newExternalId,
                Deleted = false
            }
        ];

        var response = await _client.PostAsync($"/public/organization/import", JsonContent.Create(request));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert against the database values
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();

        // Existing user
        var existingOrgUser = await organizationUserRepository.GetByIdAsync(ou.Id);
        Assert.NotNull(existingOrgUser);
        Assert.Equal(existingEmail, existingOrgUser.Email);
        Assert.Equal(OrganizationUserType.User, existingOrgUser.Type);
        Assert.Equal(existingExternalId, existingOrgUser.ExternalId);
        Assert.Equal(OrganizationUserStatusType.Confirmed, existingOrgUser.Status);
        Assert.Equal(_organization.Id, existingOrgUser.OrganizationId);

        // New User
        var newOrgUser = await organizationUserRepository.GetByOrganizationEmailAsync(_organization.Id, newEmail);
        Assert.NotNull(newOrgUser);
        Assert.Equal(newEmail, newOrgUser.Email);
        Assert.Equal(OrganizationUserType.User, newOrgUser.Type);
        Assert.Equal(newExternalId, newOrgUser.ExternalId);
        Assert.Equal(OrganizationUserStatusType.Invited, newOrgUser.Status);
        Assert.Equal(_organization.Id, newOrgUser.OrganizationId);
    }

    [Fact]
    public async Task Import_Existing_Groups_Succeeds()
    {
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var group = await OrganizationTestHelpers.CreateGroup(_factory, _organization.Id);
        var request = new OrganizationImportRequestModel();
        var addedMember = new OrganizationImportRequestModel.OrganizationImportMemberRequestModel
        {
            Email = "test@test.com",
            ExternalId = "bwtest-externalId",
            Deleted = false
        };

        request.LargeImport = false;
        request.OverwriteExisting = false;
        request.Groups = [
            new OrganizationImportRequestModel.OrganizationImportGroupRequestModel
            {
                Name = "new-name",
                ExternalId = "bwtest-externalId",
                MemberExternalIds = []
            }
        ];
        request.Members = [addedMember];

        var response = await _client.PostAsync($"/public/organization/import", JsonContent.Create(request));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert against the database values
        var groupRepository = _factory.GetService<IGroupRepository>();
        var existingGroups = (await groupRepository.GetManyByOrganizationIdAsync(_organization.Id)).ToArray();

        // Assert that we are actually updating the existing group, not adding a new one.
        Assert.Single(existingGroups);
        Assert.NotNull(existingGroups[0]);
        Assert.Equal(group.Id, existingGroups[0].Id);
        Assert.Equal("new-name", existingGroups[0].Name);
        Assert.Equal(group.ExternalId, existingGroups[0].ExternalId);

        var addedOrgUser = await organizationUserRepository.GetByOrganizationEmailAsync(_organization.Id, addedMember.Email);
        Assert.NotNull(addedOrgUser);
    }

    [Fact]
    public async Task Import_New_Groups_Succeeds()
    {
        var group = new Group
        {
            OrganizationId = _organization.Id,
            ExternalId = new Guid().ToString(),
            Name = "bwtest1"
        };

        var request = new OrganizationImportRequestModel();
        request.LargeImport = false;
        request.OverwriteExisting = false;
        request.Groups = [
            new OrganizationImportRequestModel.OrganizationImportGroupRequestModel
            {
                Name = group.Name,
                ExternalId = group.ExternalId,
                MemberExternalIds = []
            }
        ];
        request.Members = [];

        var response = await _client.PostAsync($"/public/organization/import", JsonContent.Create(request));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert against the database values
        var groupRepository = _factory.GetService<IGroupRepository>();
        var existingGroups = await groupRepository.GetManyByOrganizationIdAsync(_organization.Id);
        var existingGroup = existingGroups.Where(g => g.ExternalId == group.ExternalId).FirstOrDefault();

        Assert.NotNull(existingGroup);
        Assert.Equal(existingGroup.Name, group.Name);
        Assert.Equal(existingGroup.ExternalId, group.ExternalId);
    }

    [Fact]
    public async Task Import_New_And_Existing_Groups_Succeeds()
    {
        var existingGroup = await OrganizationTestHelpers.CreateGroup(_factory, _organization.Id);

        var newGroup = new Group
        {
            OrganizationId = _organization.Id,
            ExternalId = "test",
            Name = "bwtest1"
        };

        var request = new OrganizationImportRequestModel();
        request.LargeImport = false;
        request.OverwriteExisting = false;
        request.Groups = [
            new OrganizationImportRequestModel.OrganizationImportGroupRequestModel
            {
                Name = "new-name",
                ExternalId = existingGroup.ExternalId,
                MemberExternalIds = []
            },
            new OrganizationImportRequestModel.OrganizationImportGroupRequestModel
            {
                Name = newGroup.Name,
                ExternalId = newGroup.ExternalId,
                MemberExternalIds = []
            }
        ];
        request.Members = [];

        var response = await _client.PostAsync($"/public/organization/import", JsonContent.Create(request));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert against the database values
        var groupRepository = _factory.GetService<IGroupRepository>();
        var groups = await groupRepository.GetManyByOrganizationIdAsync(_organization.Id);

        var newGroupInDb = groups.Where(g => g.ExternalId == newGroup.ExternalId).FirstOrDefault();
        Assert.NotNull(newGroupInDb);
        Assert.Equal(newGroupInDb.Name, newGroup.Name);
        Assert.Equal(newGroupInDb.ExternalId, newGroup.ExternalId);

        var existingGroupInDb = groups.Where(g => g.ExternalId == existingGroup.ExternalId).FirstOrDefault();
        Assert.NotNull(existingGroupInDb);
        Assert.Equal(existingGroup.Id, existingGroupInDb.Id);
        Assert.Equal("new-name", existingGroupInDb.Name);
        Assert.Equal(existingGroup.ExternalId, existingGroupInDb.ExternalId);
    }

    [Fact]
    public async Task Import_Remove_Member_Without_Master_Password_Throws_400_Error()
    {
        // ARRANGE: a member without a master password
        await OrganizationTestHelpers.CreateUserWithoutMasterPasswordAsync(_factory, Guid.NewGuid() + "@example.com",
            _organization.Id);

        // ACT: an import request that would remove that member
        var request = new OrganizationImportRequestModel
        {
            LargeImport = false,
            OverwriteExisting = true, // removes all members not in the request
            Groups = [],
            Members = []
        };

        var response = await _client.PostAsync($"/public/organization/import", JsonContent.Create(request));

        // ASSERT: that a 400 error is thrown with the correct error message
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("Sync failed. To proceed, disable the 'Remove and re-add users during next sync' setting and try again.", responseContent);
    }
}
