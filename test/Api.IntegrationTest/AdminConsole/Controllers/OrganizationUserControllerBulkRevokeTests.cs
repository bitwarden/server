using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Models.Response;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Providers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUserControllerBulkRevokeTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public OrganizationUserControllerBulkRevokeTests(ApiApplicationFactory apiFactory)
    {
        _factory = apiFactory;
        _factory.SubstituteService<IFeatureService>(featureService =>
        {
            featureService
                .IsEnabled(FeatureFlagKeys.BulkRevokeUsersV2)
                .Returns(true);
        });
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"org-user-bulk-revoke-test-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually2023,
            ownerEmail: _ownerEmail, passwordManagerSeats: 10, paymentMethod: PaymentMethodType.Card);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task BulkRevoke_Success()
    {
        var (ownerEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Owner);

        await _loginHelper.LoginAsync(ownerEmail);

        var (_, orgUser1) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id, OrganizationUserType.User);
        var (_, orgUser2) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id, OrganizationUserType.User);

        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();

        var arrangedUsers = await organizationUserRepository.GetManyAsync([orgUser1.Id, orgUser2.Id]);
        Assert.All(arrangedUsers, u => Assert.Equal(OrganizationUserStatusType.Confirmed, u.Status));

        var request = new OrganizationUserBulkRequestModel
        {
            Ids = [orgUser1.Id, orgUser2.Id]
        };

        var httpResponse = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/revoke", request);
        var content = await httpResponse.Content.ReadFromJsonAsync<ListResponseModel<OrganizationUserBulkResponseModel>>();

        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        Assert.NotNull(content);
        Assert.Equal(2, content.Data.Count());
        Assert.All(content.Data, r => Assert.Empty(r.Error));

        var actualUsers = await organizationUserRepository.GetManyAsync([orgUser1.Id, orgUser2.Id]);
        Assert.All(actualUsers, u => Assert.Equal(OrganizationUserStatusType.Revoked, u.Status));
    }

    [Fact]
    public async Task BulkRevoke_AsAdmin_Success()
    {
        var (adminEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Admin);

        await _loginHelper.LoginAsync(adminEmail);

        var (_, orgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id, OrganizationUserType.User);

        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();

        var arrangedUser = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.Equal(OrganizationUserStatusType.Confirmed, arrangedUser.Status);

        var request = new OrganizationUserBulkRequestModel
        {
            Ids = [orgUser.Id]
        };

        var httpResponse = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/revoke", request);
        var content = await httpResponse.Content.ReadFromJsonAsync<ListResponseModel<OrganizationUserBulkResponseModel>>();

        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        Assert.NotNull(content);
        Assert.Single(content.Data);
        Assert.All(content.Data, r => Assert.Empty(r.Error));

        var actualUser = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.Equal(OrganizationUserStatusType.Revoked, actualUser.Status);
    }

    [Fact]
    public async Task BulkRevoke_CannotRevokeSelf_ReturnsError()
    {
        var (userEmail, orgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Admin);

        await _loginHelper.LoginAsync(userEmail);

        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();

        var arrangedUser = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.Equal(OrganizationUserStatusType.Confirmed, arrangedUser.Status);

        var request = new OrganizationUserBulkRequestModel
        {
            Ids = [orgUser.Id]
        };

        var httpResponse = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/revoke", request);
        var content = await httpResponse.Content.ReadFromJsonAsync<ListResponseModel<OrganizationUserBulkResponseModel>>();

        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        Assert.NotNull(content);
        Assert.Single(content.Data);
        Assert.Contains(content.Data, r => r.Id == orgUser.Id && r.Error == "You cannot revoke yourself.");

        var actualUser = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.Equal(OrganizationUserStatusType.Confirmed, actualUser.Status);
    }

    [Fact]
    public async Task BulkRevoke_AlreadyRevoked_ReturnsError()
    {
        var (ownerEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Owner);

        await _loginHelper.LoginAsync(ownerEmail);

        var (_, orgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id, OrganizationUserType.User);

        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();

        await organizationUserRepository.RevokeAsync(orgUser.Id);
        var arrangedUser = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.Equal(OrganizationUserStatusType.Revoked, arrangedUser.Status);

        var request = new OrganizationUserBulkRequestModel
        {
            Ids = [orgUser.Id]
        };

        var httpResponse = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/revoke", request);
        var content = await httpResponse.Content.ReadFromJsonAsync<ListResponseModel<OrganizationUserBulkResponseModel>>();

        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        Assert.NotNull(content);
        Assert.Single(content.Data);
        Assert.Contains(content.Data, r => r.Id == orgUser.Id && r.Error == "Already revoked.");

        var actualUser = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.Equal(OrganizationUserStatusType.Revoked, actualUser.Status);
    }

    [Fact]
    public async Task BulkRevoke_AdminCannotRevokeOwner_ReturnsError()
    {
        var (adminEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Admin);

        await _loginHelper.LoginAsync(adminEmail);

        var (_, ownerOrgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id, OrganizationUserType.Owner);

        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();

        var arrangedUser = await organizationUserRepository.GetByIdAsync(ownerOrgUser.Id);
        Assert.Equal(OrganizationUserStatusType.Confirmed, arrangedUser.Status);

        var request = new OrganizationUserBulkRequestModel
        {
            Ids = [ownerOrgUser.Id]
        };

        var httpResponse = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/revoke", request);
        var content = await httpResponse.Content.ReadFromJsonAsync<ListResponseModel<OrganizationUserBulkResponseModel>>();

        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        Assert.NotNull(content);
        Assert.Single(content.Data);
        Assert.Contains(content.Data, r => r.Id == ownerOrgUser.Id && r.Error == "Only owners can revoke other owners.");

        var actualUser = await organizationUserRepository.GetByIdAsync(ownerOrgUser.Id);
        Assert.Equal(OrganizationUserStatusType.Confirmed, actualUser.Status);
    }

    [Fact]
    public async Task BulkRevoke_MixedResults()
    {
        var (ownerEmail, requestingOwner) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Owner);

        await _loginHelper.LoginAsync(ownerEmail);

        var (_, validOrgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id, OrganizationUserType.User);
        var (_, alreadyRevokedOrgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id, OrganizationUserType.User);

        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();

        await organizationUserRepository.RevokeAsync(alreadyRevokedOrgUser.Id);

        var arrangedUsers = await organizationUserRepository.GetManyAsync([validOrgUser.Id, alreadyRevokedOrgUser.Id, requestingOwner.Id]);
        Assert.Equal(OrganizationUserStatusType.Confirmed, arrangedUsers.First(u => u.Id == validOrgUser.Id).Status);
        Assert.Equal(OrganizationUserStatusType.Revoked, arrangedUsers.First(u => u.Id == alreadyRevokedOrgUser.Id).Status);

        var request = new OrganizationUserBulkRequestModel
        {
            Ids = [validOrgUser.Id, alreadyRevokedOrgUser.Id, requestingOwner.Id]
        };

        var httpResponse = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/revoke", request);
        var content = await httpResponse.Content.ReadFromJsonAsync<ListResponseModel<OrganizationUserBulkResponseModel>>();

        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
        Assert.NotNull(content);
        Assert.Equal(3, content.Data.Count());

        Assert.Contains(content.Data, r => r.Id == validOrgUser.Id && r.Error == string.Empty);
        Assert.Contains(content.Data, r => r.Id == alreadyRevokedOrgUser.Id && r.Error == "Already revoked.");
        Assert.Contains(content.Data, r => r.Id == requestingOwner.Id && r.Error == "You cannot revoke yourself.");

        var actualUsers = await organizationUserRepository.GetManyAsync([validOrgUser.Id, alreadyRevokedOrgUser.Id, requestingOwner.Id]);
        Assert.Equal(OrganizationUserStatusType.Revoked, actualUsers.First(u => u.Id == validOrgUser.Id).Status);
        Assert.Equal(OrganizationUserStatusType.Revoked, actualUsers.First(u => u.Id == alreadyRevokedOrgUser.Id).Status);
        Assert.Equal(OrganizationUserStatusType.Confirmed, actualUsers.First(u => u.Id == requestingOwner.Id).Status);
    }

    [Theory]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Custom)]
    public async Task BulkRevoke_WithoutManageUsersPermission_ReturnsForbidden(OrganizationUserType organizationUserType)
    {
        var (userEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, organizationUserType, new Permissions { ManageUsers = false });

        await _loginHelper.LoginAsync(userEmail);

        var request = new OrganizationUserBulkRequestModel
        {
            Ids = [Guid.NewGuid()]
        };

        var httpResponse = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/revoke", request);

        Assert.Equal(HttpStatusCode.Forbidden, httpResponse.StatusCode);
    }

    [Fact]
    public async Task BulkRevoke_WithEmptyIds_ReturnsBadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var request = new OrganizationUserBulkRequestModel
        {
            Ids = []
        };

        var httpResponse = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/revoke", request);

        Assert.Equal(HttpStatusCode.BadRequest, httpResponse.StatusCode);
    }

    [Fact]
    public async Task BulkRevoke_WithInvalidOrganizationId_ReturnsForbidden()
    {
        var (ownerEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Owner);

        await _loginHelper.LoginAsync(ownerEmail);

        var (_, orgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id, OrganizationUserType.User);

        var invalidOrgId = Guid.NewGuid();

        var request = new OrganizationUserBulkRequestModel
        {
            Ids = [orgUser.Id]
        };

        var httpResponse = await _client.PutAsJsonAsync($"organizations/{invalidOrgId}/users/revoke", request);

        Assert.Equal(HttpStatusCode.Forbidden, httpResponse.StatusCode);
    }

    [Fact]
    public async Task BulkRevoke_CannotRevokeLastConfirmedOwner_ReturnsBadRequest()
    {
        var providerEmail = $"provider-user{Guid.NewGuid()}@example.com";

        // create user for provider
        await _factory.LoginWithNewAccount(providerEmail);

        // create provider and provider user
        await _factory.GetService<ICreateProviderCommand>()
            .CreateBusinessUnitAsync(
                new Provider
                {
                    Name = "provider",
                    Type = ProviderType.BusinessUnit
                },
                providerEmail,
                PlanType.EnterpriseAnnually2023,
                10);

        await _loginHelper.LoginAsync(providerEmail);

        var providerUserUser = await _factory.GetService<IUserRepository>().GetByEmailAsync(providerEmail);

        var providerUserCollection =
            await _factory.GetService<IProviderUserRepository>().GetManyByUserAsync(providerUserUser.Id);
        var providerUser = providerUserCollection.First();

        await _factory.GetService<IProviderOrganizationRepository>().CreateAsync(new ProviderOrganization
        {
            ProviderId = providerUser.ProviderId,
            OrganizationId = _organization.Id,
            Key = null,
            Settings = null
        });

        var (ownerEmail, ownerOrgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Owner);

        var request = new OrganizationUserBulkRequestModel
        {
            Ids = [ownerOrgUser.Id]
        };

        var httpResponse = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/revoke", request);

        Assert.Equal(HttpStatusCode.BadRequest, httpResponse.StatusCode);
    }
}
