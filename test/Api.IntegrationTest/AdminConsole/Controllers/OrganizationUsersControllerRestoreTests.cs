using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUsersControllerRestoreTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public OrganizationUsersControllerRestoreTests(ApiApplicationFactory apiFactory)
    {
        _factory = apiFactory;
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"org-user-restore-test-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseMonthly,
            ownerEmail: _ownerEmail, passwordManagerSeats: 10, paymentMethod: PaymentMethodType.Card);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Restore_RevokedStagedUser_ReturnsOkAndRestoresToStaged()
    {
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var stagedUser = await CreateRevokedStagedUserAsync(organizationUserRepository);

        await _loginHelper.LoginAsync(_ownerEmail);

        var request = new OrganizationUserRestoreRequest { DefaultUserCollectionName = null };
        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/{stagedUser.Id}/restore/vnext", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var restoredUser = await organizationUserRepository.GetByIdAsync(stagedUser.Id);
        Assert.NotNull(restoredUser);
        Assert.Equal(OrganizationUserStatusType.Staged, restoredUser.Status);
    }

    [Fact]
    public async Task BulkRestore_RevokedStagedUser_ReturnsOkWithoutErrorAndRestoresToStaged()
    {
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var stagedUser = await CreateRevokedStagedUserAsync(organizationUserRepository);

        await _loginHelper.LoginAsync(_ownerEmail);

        var request = new OrganizationUserBulkRequestModel { Ids = [stagedUser.Id] };
        var response = await _client.PutAsJsonAsync($"organizations/{_organization.Id}/users/restore", request);
        var content = await response.Content.ReadFromJsonAsync<ListResponseModel<OrganizationUserBulkResponseModel>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
        Assert.Single(content.Data);
        Assert.All(content.Data, r => Assert.Empty(r.Error));

        var restoredUser = await organizationUserRepository.GetByIdAsync(stagedUser.Id);
        Assert.NotNull(restoredUser);
        Assert.Equal(OrganizationUserStatusType.Staged, restoredUser.Status);
    }

    /// <summary>
    /// Creates a Staged OrganizationUser (no linked user account) and then revokes it, mirroring the
    /// production flow that snapshots the prior Staged status onto the revoked row.
    /// </summary>
    private async Task<OrganizationUser> CreateRevokedStagedUserAsync(IOrganizationUserRepository organizationUserRepository)
    {
        var stagedUser = new OrganizationUser
        {
            OrganizationId = _organization.Id,
            UserId = null,
            Email = $"staged-{Guid.NewGuid()}@bitwarden.com",
            Key = null,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Staged
        };
        await organizationUserRepository.CreateAsync(stagedUser);

        await organizationUserRepository.RevokeAsync(stagedUser.Id, RevocationReason.Manual);

        var revokedUser = await organizationUserRepository.GetByIdAsync(stagedUser.Id);
        Assert.NotNull(revokedUser);
        Assert.Equal(OrganizationUserStatusType.Revoked, revokedUser.Status);

        return stagedUser;
    }
}
