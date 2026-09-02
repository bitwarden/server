using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Models.Response;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using NSubstitute;
using NSubstitute.ClearExtensions;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

/// <summary>
/// Covers the members-grid "Send invite" row action, which promotes Staged members to Invited without
/// touching their access. Seat expansion and the revert-on-send-failure path only hold together across the
/// command, the repositories and the billing plumbing, so they are exercised here rather than in unit tests.
///
/// Members that are no longer staged are reported per row; only seat expansion fails the whole request.
/// </summary>
public class OrganizationUsersControllerSendInviteToStagedUsersTests
    : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly LoginHelper _loginHelper;
    private readonly ISendOrganizationInvitesCommand _sendOrganizationInvitesCommand;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public OrganizationUsersControllerSendInviteToStagedUsersTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IFeatureService>(featureService =>
            featureService.IsEnabled(FeatureFlagKeys.PM34423StagedStatus).Returns(true));
        _factory.SubstituteService<ISendOrganizationInvitesCommand>(_ => { });

        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _sendOrganizationInvitesCommand = _factory.GetService<ISendOrganizationInvitesCommand>();
        _organizationRepository = _factory.GetService<IOrganizationRepository>();
        _organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
    }

    public async Task InitializeAsync()
    {
        // The substitutes live on the shared class fixture, so a throw configured by one test would
        // otherwise apply to whichever test runs next.
        _sendOrganizationInvitesCommand.ClearSubstitute();

        _factory.GetService<GlobalSettings>().SelfHosted = false;

        _ownerEmail = $"staged-send-invite-{Guid.NewGuid()}@bitwarden.com";
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
    public async Task SendInvite_WithStagedMembers_PromotesThemAndSendsInvitations()
    {
        var staged = await StageMembersAsync(2);

        var response = await SendInviteAsync(staged.Select(member => member.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await ReadResultsAsync(response);
        Assert.All(results, result => Assert.Empty(result.Error));

        var promoted = await _organizationUserRepository.GetManyAsync(staged.Select(member => member.Id).ToList());
        Assert.All(promoted, member => Assert.Equal(OrganizationUserStatusType.Invited, member.Status));

        // Promotion is access-neutral: the row keeps the identity the provisioning tool gave it.
        Assert.All(promoted, member => Assert.Equal(OrganizationUserType.User, member.Type));
        Assert.All(promoted, member => Assert.NotNull(member.ExternalId));
        Assert.All(promoted, member => Assert.Null(member.UserId));

        await _sendOrganizationInvitesCommand.Received(1).SendInvitesAsync(
            Arg.Is<SendInvitesRequest>(request =>
                request.Organization.Id == _organization.Id &&
                request.Users.Length == 2));
    }

    [Fact]
    public async Task SendInvite_WhenTheOrganizationIsOutOfSeats_AutoscalesTheSubscription()
    {
        // One seat, taken by the owner. Staged members are free, so the shortfall is exactly two.
        await SetSeatsAsync(seats: 1, maxAutoscaleSeats: 5);
        var staged = await StageMembersAsync(2);

        var response = await SendInviteAsync(staged.Select(member => member.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var organization = await _organizationRepository.GetByIdAsync(_organization.Id);
        Assert.NotNull(organization);
        Assert.Equal(3, organization.Seats);

        var promoted = await _organizationUserRepository.GetManyAsync(staged.Select(member => member.Id).ToList());
        Assert.All(promoted, member => Assert.Equal(OrganizationUserStatusType.Invited, member.Status));
    }

    [Fact]
    public async Task SendInvite_WhenSendingInvitationsFailsAfterAutoscaling_KeepsTheAddedSeats()
    {
        await SetSeatsAsync(seats: 1, maxAutoscaleSeats: 5);
        var staged = await StageMembersAsync(1);

        _sendOrganizationInvitesCommand
            .SendInvitesAsync(Arg.Any<SendInvitesRequest>())
            .ThrowsAsync(new InvalidOperationException("mail delivery is unavailable"));

        var response = await SendInviteAsync(staged.Select(member => member.Id));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var reverted = await _organizationUserRepository.GetByIdAsync(staged[0].Id);
        Assert.NotNull(reverted);
        Assert.Equal(OrganizationUserStatusType.Staged, reverted.Status);

        // Seats bought from the gateway are not handed back: the admin can retry without paying twice, and
        // unwinding a subscription change on a send failure is riskier than leaving the seat in place.
        var organization = await _organizationRepository.GetByIdAsync(_organization.Id);
        Assert.NotNull(organization);
        Assert.Equal(2, organization.Seats);
    }

    [Fact]
    public async Task SendInvite_WhenTheAutoscaleLimitCannotCoverEveryMember_LeavesThemAllStaged()
    {
        // Room to autoscale by one, but two members were selected. Seats are reserved once for the whole
        // eligible set, and the only thing enforcing the cap is AutoAddSeatsAsync itself, so this has to run
        // against the real one.
        await SetSeatsAsync(seats: 1, maxAutoscaleSeats: 2);
        var staged = await StageMembersAsync(2);

        var response = await SendInviteAsync(staged.Select(member => member.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var organization = await _organizationRepository.GetByIdAsync(_organization.Id);
        Assert.NotNull(organization);
        Assert.Equal(1, organization.Seats);

        var untouched = await _organizationUserRepository.GetManyAsync(staged.Select(member => member.Id).ToList());
        Assert.All(untouched, member => Assert.Equal(OrganizationUserStatusType.Staged, member.Status));

        await _sendOrganizationInvitesCommand.DidNotReceive().SendInvitesAsync(Arg.Any<SendInvitesRequest>());
    }

    [Fact]
    public async Task SendInvite_WhenAMemberIsNoLongerStaged_SkipsItAndInvitesTheRest()
    {
        var staged = await StageMembersAsync(1);
        var (_, confirmed) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);

        var response = await SendInviteAsync([staged[0].Id, confirmed.Id]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await ReadResultsAsync(response);
        Assert.Empty(Assert.Single(results, result => result.Id == staged[0].Id).Error);
        Assert.NotEmpty(Assert.Single(results, result => result.Id == confirmed.Id).Error);

        var promoted = await _organizationUserRepository.GetByIdAsync(staged[0].Id);
        Assert.NotNull(promoted);
        Assert.Equal(OrganizationUserStatusType.Invited, promoted.Status);

        var untouched = await _organizationUserRepository.GetByIdAsync(confirmed.Id);
        Assert.NotNull(untouched);
        Assert.Equal(OrganizationUserStatusType.Confirmed, untouched.Status);

        await _sendOrganizationInvitesCommand.Received(1).SendInvitesAsync(
            Arg.Is<SendInvitesRequest>(request => request.Users.Length == 1));
    }

    [Fact]
    public async Task SendInvite_WhenAMemberBelongsToAnotherOrganization_ReportsItWithoutTouchingIt()
    {
        var otherOwnerEmail = $"staged-send-invite-other-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(otherOwnerEmail);
        var (otherOrganization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually, ownerEmail: otherOwnerEmail, passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);

        var outsider = await OrganizationTestHelpers.CreateStagedUserAsync(_factory, otherOrganization,
            $"outsider-{Guid.NewGuid()}@bitwarden.com");

        var response = await SendInviteAsync([outsider.Id]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await ReadResultsAsync(response);
        Assert.NotEmpty(Assert.Single(results, result => result.Id == outsider.Id).Error);

        var untouched = await _organizationUserRepository.GetByIdAsync(outsider.Id);
        Assert.NotNull(untouched);
        Assert.Equal(OrganizationUserStatusType.Staged, untouched.Status);

        await _sendOrganizationInvitesCommand.DidNotReceive().SendInvitesAsync(Arg.Any<SendInvitesRequest>());
    }

    private static async Task<List<OrganizationUserBulkResponseModel>> ReadResultsAsync(HttpResponseMessage response)
    {
        var body = await response.Content
            .ReadFromJsonAsync<ListResponseModel<OrganizationUserBulkResponseModel>>();
        Assert.NotNull(body);
        return body.Data.ToList();
    }

    private Task<HttpResponseMessage> SendInviteAsync(IEnumerable<Guid> organizationUserIds) =>
        _client.PostAsJsonAsync($"organizations/{_organization.Id}/users/send-invite",
            new OrganizationUserBulkRequestModel { Ids = organizationUserIds });

    private async Task<List<OrganizationUser>> StageMembersAsync(int count)
    {
        var staged = new List<OrganizationUser>();
        for (var i = 0; i < count; i++)
        {
            staged.Add(await OrganizationTestHelpers.CreateStagedUserAsync(_factory, _organization,
                $"staged-{Guid.NewGuid()}@bitwarden.com"));
        }

        return staged;
    }

    /// <summary>
    /// Fixes the organization's seat headroom. Gateway ids are stood in because AutoAddSeats refuses to
    /// adjust a subscription it cannot identify, and the test host's payment service is a no-op substitute.
    /// </summary>
    private async Task SetSeatsAsync(int seats, int? maxAutoscaleSeats)
    {
        _organization.Seats = seats;
        _organization.MaxAutoscaleSeats = maxAutoscaleSeats;
        _organization.Gateway = GatewayType.Stripe;
        _organization.GatewayCustomerId = "cus_integration_test";
        _organization.GatewaySubscriptionId = "sub_integration_test";
        await _organizationRepository.ReplaceAsync(_organization);
    }
}
