using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Models.Request;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using NSubstitute;
using NSubstitute.ClearExtensions;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

/// <summary>
/// Covers the invite dialog, which posts a list of email addresses and cannot tell a staged member apart
/// from a brand new one. <see cref="Bit.Core.AdminConsole.Services.Implementations.OrganizationService"/>
/// partitions the batch: staged rows are promoted in place, everything else is created. Seat accounting and
/// the rollback both span that partition, so they are exercised end to end here.
/// </summary>
public class OrganizationUsersControllerInviteTests
    : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly LoginHelper _loginHelper;
    private readonly ISendOrganizationInvitesCommand _sendOrganizationInvitesCommand;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IGroupRepository _groupRepository;

    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public OrganizationUsersControllerInviteTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<ISendOrganizationInvitesCommand>(_ => { });

        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _sendOrganizationInvitesCommand = _factory.GetService<ISendOrganizationInvitesCommand>();
        _organizationRepository = _factory.GetService<IOrganizationRepository>();
        _organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        _groupRepository = _factory.GetService<IGroupRepository>();
    }

    public async Task InitializeAsync()
    {
        // The substitute lives on the shared class fixture, so a throw configured by one test would
        // otherwise apply to whichever test runs next.
        _sendOrganizationInvitesCommand.ClearSubstitute();

        _factory.GetService<GlobalSettings>().SelfHosted = false;

        _ownerEmail = $"staged-invite-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually, ownerEmail: _ownerEmail, passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);

        await _loginHelper.LoginAsync(_ownerEmail);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Invite_WithAMixOfStagedAndNewEmails_PromotesInPlaceAndCreatesTheRest()
    {
        var stagedEmail = $"staged-{Guid.NewGuid()}@bitwarden.com";
        var staged = await OrganizationTestHelpers.CreateStagedUserAsync(_factory, _organization, stagedEmail,
            externalId: "directory-connector-id");
        var newEmail = $"new-{Guid.NewGuid()}@bitwarden.com";

        var collection = await OrganizationTestHelpers.CreateCollectionAsync(_factory, _organization.Id, "Shared");
        var group = await OrganizationTestHelpers.CreateGroup(_factory, _organization.Id);

        var response = await InviteAsync(new OrganizationUserInviteRequestModel
        {
            Emails = [stagedEmail, newEmail],
            Type = OrganizationUserType.Admin,
            Collections = [new SelectionReadOnlyRequestModel { Id = collection.Id, Manage = true }],
            Groups = [group.Id]
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The staged row is reused rather than replaced, so anything keyed off its id or external id
        // (SCIM, Directory Connector) keeps pointing at the same member.
        var promoted = await _organizationUserRepository.GetByIdAsync(staged.Id);
        Assert.NotNull(promoted);
        Assert.Equal(OrganizationUserStatusType.Invited, promoted.Status);
        Assert.Equal(OrganizationUserType.Admin, promoted.Type);
        Assert.Equal("directory-connector-id", promoted.ExternalId);

        var created = await _organizationUserRepository.GetByOrganizationEmailAsync(_organization.Id, newEmail);
        Assert.NotNull(created);
        Assert.Equal(OrganizationUserStatusType.Invited, created.Status);

        // Owner plus exactly one row per invited email: the staged member was not duplicated.
        var allMembers = await _organizationUserRepository.GetManyByOrganizationAsync(_organization.Id, null);
        Assert.Equal(3, allMembers.Count);

        // The promoted member is configured like the new one, not left with the empty access it was staged with.
        foreach (var memberId in new[] { promoted.Id, created.Id })
        {
            var (_, collections) = await _organizationUserRepository.GetByIdWithCollectionsAsync(memberId);
            Assert.Equal(collection.Id, Assert.Single(collections).Id);
            Assert.Equal(group.Id, Assert.Single(await _groupRepository.GetManyIdsByUserIdAsync(memberId)));
        }

        await _sendOrganizationInvitesCommand.Received(1).SendInvitesAsync(
            Arg.Is<SendInvitesRequest>(request => request.Users.Length == 2));
    }

    [Fact]
    public async Task Invite_WhenTheDialogSelectsNoAccess_LeavesTheStagedMembersExistingAccessAlone()
    {
        var stagedEmail = $"staged-{Guid.NewGuid()}@bitwarden.com";
        var staged = await OrganizationTestHelpers.CreateStagedUserAsync(_factory, _organization, stagedEmail);

        // A staged member can be given access before anyone invites them, via the member dialog or by being
        // added to a group. The invite dialog cannot show that access, so it must not clear it.
        var collection = await OrganizationTestHelpers.CreateCollectionAsync(_factory, _organization.Id, "Shared");
        var group = await OrganizationTestHelpers.CreateGroup(_factory, _organization.Id);
        await _organizationUserRepository.ReplaceAsync(staged,
            [new CollectionAccessSelection { Id = collection.Id, Manage = true }]);
        await _organizationUserRepository.UpdateGroupsAsync(staged.Id, [group.Id], DateTime.UtcNow);

        var response = await InviteAsync(new OrganizationUserInviteRequestModel
        {
            Emails = [stagedEmail],
            Type = OrganizationUserType.User,
            Collections = [],
            Groups = []
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var promoted = await _organizationUserRepository.GetByIdAsync(staged.Id);
        Assert.NotNull(promoted);
        Assert.Equal(OrganizationUserStatusType.Invited, promoted.Status);

        var (_, collections) = await _organizationUserRepository.GetByIdWithCollectionsAsync(staged.Id);
        Assert.Equal(collection.Id, Assert.Single(collections).Id);
        Assert.Equal(group.Id, Assert.Single(await _groupRepository.GetManyIdsByUserIdAsync(staged.Id)));
    }

    [Theory]
    // Owner plus room for two: promoting one staged member and creating one new one fits exactly.
    [InlineData(3)]
    // Owner only: both members are short a seat, so the subscription grows to the same total.
    [InlineData(1)]
    public async Task Invite_BuysSeatsForStagedAndNewMembersAlike(int startingSeats)
    {
        await SetSeatsAsync(seats: startingSeats, maxAutoscaleSeats: 10);

        var stagedEmail = $"staged-{Guid.NewGuid()}@bitwarden.com";
        await OrganizationTestHelpers.CreateStagedUserAsync(_factory, _organization, stagedEmail);

        var response = await InviteAsync(new OrganizationUserInviteRequestModel
        {
            Emails = [stagedEmail, $"new-{Guid.NewGuid()}@bitwarden.com"],
            Type = OrganizationUserType.User
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // A staged member holds no seat until they are invited, so both cost one.
        var organization = await _organizationRepository.GetByIdAsync(_organization.Id);
        Assert.NotNull(organization);
        Assert.Equal(3, organization.Seats);
    }

    [Fact]
    public async Task Invite_WhenTheAutoscaleLimitWouldBeExceeded_RejectsTheWholeBatch()
    {
        await SetSeatsAsync(seats: 1, maxAutoscaleSeats: 2);

        var stagedEmail = $"staged-{Guid.NewGuid()}@bitwarden.com";
        var staged = await OrganizationTestHelpers.CreateStagedUserAsync(_factory, _organization, stagedEmail);

        var response = await InviteAsync(new OrganizationUserInviteRequestModel
        {
            Emails = [stagedEmail, $"new-{Guid.NewGuid()}@bitwarden.com"],
            Type = OrganizationUserType.User
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var organization = await _organizationRepository.GetByIdAsync(_organization.Id);
        Assert.NotNull(organization);
        Assert.Equal(1, organization.Seats);

        var untouched = await _organizationUserRepository.GetByIdAsync(staged.Id);
        Assert.NotNull(untouched);
        Assert.Equal(OrganizationUserStatusType.Staged, untouched.Status);
    }

    [Fact]
    public async Task Invite_WhenSendingInvitationsFails_RestoresTheStagedMemberAndDeletesTheNewOne()
    {
        var stagedEmail = $"staged-{Guid.NewGuid()}@bitwarden.com";
        var staged = await OrganizationTestHelpers.CreateStagedUserAsync(_factory, _organization, stagedEmail,
            externalId: "directory-connector-id");
        var before = await _organizationUserRepository.GetByIdAsync(staged.Id);
        Assert.NotNull(before);

        var newEmail = $"new-{Guid.NewGuid()}@bitwarden.com";

        _sendOrganizationInvitesCommand
            .SendInvitesAsync(Arg.Any<SendInvitesRequest>())
            .ThrowsAsync(new InvalidOperationException("mail delivery is unavailable"));

        var response = await InviteAsync(new OrganizationUserInviteRequestModel
        {
            Emails = [stagedEmail, newEmail],
            Type = OrganizationUserType.Admin
        });

        // Every invite failure is collected into an AggregateException, which the Api maps to 400.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // The staged row existed before this call and belongs to whatever provisioned it, so it is put back
        // rather than deleted.
        var reverted = await _organizationUserRepository.GetByIdAsync(staged.Id);
        Assert.NotNull(reverted);
        Assert.Equal(OrganizationUserStatusType.Staged, reverted.Status);
        Assert.Equal(before.Type, reverted.Type);
        Assert.Equal(before.ExternalId, reverted.ExternalId);
        Assert.Equal(before.RevisionDate, reverted.RevisionDate);

        // The row this call created is not.
        Assert.Null(await _organizationUserRepository.GetByOrganizationEmailAsync(_organization.Id, newEmail));
    }

    [Fact]
    public async Task Invite_WhenSendingInvitationsFailsAfterAutoscaling_GivesTheSeatsBack()
    {
        await SetSeatsAsync(seats: 1, maxAutoscaleSeats: 10);

        var stagedEmail = $"staged-{Guid.NewGuid()}@bitwarden.com";
        var staged = await OrganizationTestHelpers.CreateStagedUserAsync(_factory, _organization, stagedEmail);

        _sendOrganizationInvitesCommand
            .SendInvitesAsync(Arg.Any<SendInvitesRequest>())
            .ThrowsAsync(new InvalidOperationException("mail delivery is unavailable"));

        var response = await InviteAsync(new OrganizationUserInviteRequestModel
        {
            Emails = [stagedEmail, $"new-{Guid.NewGuid()}@bitwarden.com"],
            Type = OrganizationUserType.User
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var reverted = await _organizationUserRepository.GetByIdAsync(staged.Id);
        Assert.NotNull(reverted);
        Assert.Equal(OrganizationUserStatusType.Staged, reverted.Status);

        // Unlike the row action, this path unwinds its own autoscale: nobody was invited, so nobody is billed.
        var organization = await _organizationRepository.GetByIdAsync(_organization.Id);
        Assert.NotNull(organization);
        Assert.Equal(1, organization.Seats);
    }

    private Task<HttpResponseMessage> InviteAsync(OrganizationUserInviteRequestModel model)
    {
        // The dialog always posts an array, even when nothing was selected, and OrganizationService
        // dereferences it without a null check. Match the client rather than exercise that gap here.
        model.Collections ??= [];
        return _client.PostAsJsonAsync($"organizations/{_organization.Id}/users/invite", model);
    }

    /// <summary>
    /// Fixes the organization's seat headroom. Gateway ids are stood in because AutoAddSeats refuses to
    /// adjust a subscription it cannot identify, and the test host's payment service is a no-op substitute.
    /// </summary>
    private Task SetSeatsAsync(int seats, int? maxAutoscaleSeats) =>
        OrganizationTestHelpers.SetSeatsAsync(_factory, _organization, seats, maxAutoscaleSeats);
}
