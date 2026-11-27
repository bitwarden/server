using System.Net;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Seeder.Scenes;
using Duende.IdentityModel.Client;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

public class UserAuthRequestSceneTests : IClassFixture<InPlaySeederApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly InPlaySeederApiApplicationFactory _factory;
    private readonly string _playId = Guid.NewGuid().ToString();

    public UserAuthRequestSceneTests(InPlaySeederApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _client.SetBasicAuthentication(_factory.Username, _factory.Password);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        var deleteResponse = await _client.DeleteAsync($"/seed/{_playId}");
        deleteResponse.EnsureSuccessStatusCode();
        _client.Dispose();
    }

    [Fact]
    public async Task SeedAsync_UnknownUser_ReturnsBadRequestAndWritesNoRequest()
    {
        var unknownUserId = Guid.NewGuid();

        var response = await _client.PostSceneRequestAsync(_playId, nameof(UserAuthRequestScene), new UserAuthRequestScene.Request
        {
            UserId = unknownUserId,
            Type = AuthRequestType.AuthenticateAndUnlock
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await AnyAuthRequestForUserAsync(unknownUserId));
    }

    [Fact]
    public async Task SeedAsync_AdminApprovalWithoutOrganizationId_ReturnsBadRequestAndWritesNoRequest()
    {
        var user = await _client.SeedUserAsync(_playId);

        var response = await _client.PostSceneRequestAsync(_playId, nameof(UserAuthRequestScene), new UserAuthRequestScene.Request
        {
            UserId = user.UserId,
            Type = AuthRequestType.AdminApproval
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await AnyAuthRequestForUserAsync(user.UserId));
    }

    [Fact]
    public async Task SeedAsync_OrganizationIdOnNonAdminApprovalType_ReturnsBadRequestAndWritesNoRequest()
    {
        var organization = await _client.SeedOrganizationAsync(_playId);
        var owner = organization.Owner;

        var response = await _client.PostSceneRequestAsync(_playId, nameof(UserAuthRequestScene), new UserAuthRequestScene.Request
        {
            UserId = owner.UserId,
            Type = AuthRequestType.AuthenticateAndUnlock,
            OrganizationId = organization.OrganizationId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await AnyAuthRequestForUserAsync(owner.UserId));
    }

    [Fact]
    public async Task SeedAsync_AdminApprovalForNonMember_ReturnsBadRequestAndWritesNoRequest()
    {
        var organization = await _client.SeedOrganizationAsync(_playId);
        var user = await _client.SeedUserAsync(_playId);

        var response = await _client.PostSceneRequestAsync(_playId, nameof(UserAuthRequestScene), new UserAuthRequestScene.Request
        {
            UserId = user.UserId,
            Type = AuthRequestType.AdminApproval,
            OrganizationId = organization.OrganizationId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await AnyAuthRequestForUserAsync(user.UserId));
    }

    [Fact]
    public async Task SeedAsync_AuthenticateAndUnlock_StoresPendingRequest()
    {
        var user = await _client.SeedUserAsync(_playId);

        var result = await _client.PostSceneAsync(_playId, nameof(UserAuthRequestScene), new UserAuthRequestScene.Request
        {
            UserId = user.UserId,
            Type = AuthRequestType.AuthenticateAndUnlock
        });

        var authRequestId = result.GetProperty("authRequestId").GetGuid();
        var stored = await _factory.QueryAsync(db => db.AuthRequests.SingleAsync(ar => ar.Id == authRequestId));
        Assert.Equal(user.UserId, stored.UserId);
        Assert.Equal(AuthRequestType.AuthenticateAndUnlock, stored.Type);
        Assert.Null(stored.OrganizationId);
        AssertPending(stored);
    }

    [Fact]
    public async Task SeedAsync_AdminApprovalForConfirmedMember_StoresPendingRequest()
    {
        var organization = await _client.SeedOrganizationAsync(_playId);
        var owner = organization.Owner;

        var result = await _client.PostSceneAsync(_playId, nameof(UserAuthRequestScene), new UserAuthRequestScene.Request
        {
            UserId = owner.UserId,
            Type = AuthRequestType.AdminApproval,
            OrganizationId = organization.OrganizationId
        });

        var authRequestId = result.GetProperty("authRequestId").GetGuid();
        var stored = await _factory.QueryAsync(db => db.AuthRequests.SingleAsync(ar => ar.Id == authRequestId));
        Assert.Equal(owner.UserId, stored.UserId);
        Assert.Equal(AuthRequestType.AdminApproval, stored.Type);
        Assert.Equal(organization.OrganizationId, stored.OrganizationId);
        AssertPending(stored);
    }

    private static void AssertPending(AuthRequest stored)
    {
        Assert.Equal(25, stored.AccessCode.Length);
        Assert.False(string.IsNullOrEmpty(stored.PublicKey));
        Assert.Null(stored.Approved);
        Assert.Null(stored.ResponseDate);
    }

    private Task<bool> AnyAuthRequestForUserAsync(Guid userId) =>
        _factory.QueryAsync(db => db.AuthRequests.AnyAsync(ar => ar.UserId == userId));
}
