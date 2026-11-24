using System.Net;
using Bit.Core.Enums;
using Bit.Seeder.Scenes;
using Duende.IdentityModel.Client;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

/// <summary>
/// Exercises OrganizationUserScene through POST /seed: a Staged member is recorded the way SCIM/Directory
/// Connector provisioning records it (email only, no linked user, no key), so the invite link accept flow
/// can find it by email.
/// </summary>
public class OrganizationUserSceneTests : IClassFixture<InPlaySeederApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly InPlaySeederApiApplicationFactory _factory;

    public OrganizationUserSceneTests(InPlaySeederApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _client.SetBasicAuthentication(_factory.Username, _factory.Password);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _client.DeleteAsync("/seed");
        _client.Dispose();
    }

    [Fact]
    public async Task OrganizationUserScene_Staged_RecordsMemberByEmailWithoutUserOrKey()
    {
        var playId = Guid.NewGuid().ToString();
        var organization = await _client.SeedOrganizationAsync(playId);
        var member = await _client.SeedUserAsync(playId);

        var result = await _client.PostSceneAsync(playId, nameof(OrganizationUserScene), new OrganizationUserScene.Request
        {
            UserId = member.UserId,
            OrganizationId = organization.OrganizationId,
            OrganizationKeyB64 = organization.OrganizationKeyB64,
            OrganizationUserType = OrganizationUserType.User,
            OrganizationUserStatusType = OrganizationUserStatusType.Staged
        });

        var organizationUserId = result.GetProperty("organizationUserId").GetGuid();
        var memberEmail = await _factory.QueryAsync(db =>
            db.Users.Where(u => u.Id == member.UserId).Select(u => u.Email).SingleAsync());
        var organizationUser = await _factory.QueryAsync(db =>
            db.OrganizationUsers.SingleAsync(ou => ou.Id == organizationUserId));

        Assert.Equal(organization.OrganizationId, organizationUser.OrganizationId);
        Assert.Equal(OrganizationUserStatusType.Staged, organizationUser.Status);
        Assert.Equal(OrganizationUserType.User, organizationUser.Type);
        Assert.Null(organizationUser.UserId);
        Assert.Equal(memberEmail, organizationUser.Email);
        Assert.Null(organizationUser.Key);
        Assert.Null(organizationUser.ResetPasswordKey);
    }

    [Fact]
    public async Task OrganizationUserScene_StagedWithAccountRecovery_ReturnsBadRequestWithoutWrites()
    {
        var playId = Guid.NewGuid().ToString();
        var organization = await _client.SeedOrganizationAsync(playId);
        var member = await _client.SeedUserAsync(playId);
        var memberEmail = await _factory.QueryAsync(db =>
            db.Users.Where(u => u.Id == member.UserId).Select(u => u.Email).SingleAsync());

        var response = await _client.PostSceneRequestAsync(playId, nameof(OrganizationUserScene), new OrganizationUserScene.Request
        {
            UserId = member.UserId,
            OrganizationId = organization.OrganizationId,
            OrganizationKeyB64 = organization.OrganizationKeyB64,
            OrganizationUserType = OrganizationUserType.User,
            OrganizationUserStatusType = OrganizationUserStatusType.Staged,
            UserKeyB64 = member.UserKeyB64
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await _factory.QueryAsync(db => db.OrganizationUsers.AnyAsync(ou =>
            ou.OrganizationId == organization.OrganizationId
            && (ou.UserId == member.UserId || ou.Email == memberEmail))));
    }
}
