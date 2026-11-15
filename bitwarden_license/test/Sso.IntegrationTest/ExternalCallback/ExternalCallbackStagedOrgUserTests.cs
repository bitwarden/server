using System.Net;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Auth.Repositories;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Sso.IntegrationTest.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Sso.IntegrationTest.ExternalCallback;

/// <summary>
/// An external login through <c>/Account/ExternalCallback</c> against a Staged organization user moves the row
/// to Invited, and that move needs a seat. A new user is provisioned just in time. An existing Bitwarden user gets
/// an invite email and goes back to the login page. When no seat is available, the row stays Staged, SSO creates
/// no account, no invite is sent, and the user goes to the client's SSO login failed page. When the invite email
/// fails, the row goes back to Staged.
/// </summary>
public class ExternalCallbackStagedOrgUserTests
{
    /*
    * SUCCESS PATH: Test to verify /Account/ExternalCallback promotes a Staged OrganizationUser
    * row to Invited when a brand-new (no BW User yet) user JIT-provisions against it. Staged
    * rows come from directory-import (UserId=null, Email set); the JIT finish-accept path
    * only handles Invited, so we must promote before letting the standard invite lifecycle
    * pick up. Also asserts RevisionDate is bumped so watermark-driven consumers see the change.
    */
    [Fact]
    public async Task ExternalCallback_WithJitProvisioning_AgainstStagedOrgUser_PromotesToInvited()
    {
        // Arrange — no BW User; a Staged OrganizationUser exists matching the SSO-claimed email.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithStagedOrganizationUser()
            .BuildAsync();

        // Capture the seeded row's RevisionDate so we can prove it was bumped by the promotion.
        var stagedRowInitialRevisionDate = testData.OrganizationUser!.RevisionDate;

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act — JIT-provision the user via SSO.
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert — SSO callback succeeded end-to-end.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        // Assert — the Staged row has been promoted to Invited, back-linked to the
        // newly-provisioned BW user, and RevisionDate has advanced past the seeded value.
        var orgUserRepo = testData.Factory.Services.GetRequiredService<IOrganizationUserRepository>();
        var refreshedOrgUser = await orgUserRepo.GetByIdAsync(testData.OrganizationUser!.Id);
        Assert.NotNull(refreshedOrgUser);
        Assert.Equal(OrganizationUserStatusType.Invited, refreshedOrgUser.Status);
        Assert.NotNull(refreshedOrgUser.UserId);
        Assert.True(refreshedOrgUser.RevisionDate > stagedRowInitialRevisionDate,
            $"RevisionDate should be bumped after promotion. Before: {stagedRowInitialRevisionDate:O}, After: {refreshedOrgUser.RevisionDate:O}.");
    }

    /*
    * FAILURE PATH: Test to verify /Account/ExternalCallback rejects a Staged-against-JIT
    * SSO login when the org is at its seat cap on self-hosted. Staged rows do not count
    * against occupied seats; promoting one to Invited consumes a seat and therefore must
    * run the same "seats available or autoscale" check as a fresh JIT provisioning.
    */
    [Fact]
    public async Task ExternalCallback_WithJitProvisioning_AgainstStagedOrgUser_OnSelfHostedAtSeatCap_RedirectsToSsoLoginFailed()
    {
        // Arrange — Seats = 5 causes the builder to auto-fill 5 Confirmed OrgUsers (occupying
        // all seats). The Staged row is not counted against seats. Self-hosted disables autoscale.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithStagedOrganizationUser()
            .WithOrganization(org =>
            {
                org.Seats = 5;
            })
            .AsSelfHosted()
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Promoting the Staged row would exceed the seat cap and self-hosted cannot autoscale.
        ExternalCallbackAssertions.AssertNoSeatsAvailableRedirect(response);
    }

    /*
    * FAILURE PATH: Test to verify /Account/ExternalCallback rejects a Staged-against-JIT
    * SSO login when the org is at its seat cap on cloud and autoscale cannot grow the cap.
    * Mirrors ExternalCallback_WithNoAvailableSeats_AndAutoAddSeatsFails_RedirectsToSsoLoginFailed
    * but for the Staged-promotion path rather than fresh JIT.
    */
    [Fact]
    public async Task ExternalCallback_WithJitProvisioning_AgainstStagedOrgUser_OnCloudAutoscaleFails_RedirectsToSsoLoginFailed()
    {
        // Arrange — Seats == MaxAutoscaleSeats forces AutoAddSeatsAsync to fail. Builder
        // auto-fills 5 Confirmed OrgUsers, so the org is at cap when SSO arrives.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithStagedOrganizationUser()
            .WithOrganization(org =>
            {
                org.Seats = 5;
                org.MaxAutoscaleSeats = 5;
            })
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Promoting the Staged row would exceed the seat cap and autoscale cannot grow past
        // MaxAutoscaleSeats.
        ExternalCallbackAssertions.AssertNoSeatsAvailableRedirect(response);
    }

    /*
    * SUCCESS PATH: Test to verify /Account/ExternalCallback JIT-provisions a user against a
    * Staged OrganizationUser when the org is at seat cap but cloud autoscale can grow. Locks
    * in the "Staged promotion, at cap, autoscale succeeds" branch — the three sibling Staged
    * failure tests only exercise the throwing paths. IOrganizationService.AutoAddSeatsAsync
    * is substituted to bypass the real billing gateway.
    */
    [Fact]
    public async Task ExternalCallback_WithJitProvisioning_AgainstStagedOrgUser_OnCloudAutoscaleSucceeds_PromotesToInvited()
    {
        // Arrange — Seats = 5 fills 5 Confirmed OrgUsers (at cap). Staged row does not count
        // against the cap; promoting it would push us over, but the mocked autoscale grows.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithStagedOrganizationUser()
            .WithOrganization(org =>
            {
                org.Seats = 5;
            })
            .WithAutoscaleSucceeds()
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert — SSO callback succeeded end-to-end.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        // Assert — autoscale was actually invoked (proves we went through the "at cap →
        // autoscale → promote" branch, not the "seats available" shortcut).
        var orgService = testData.Factory.Services.GetRequiredService<IOrganizationService>();
        await orgService.Received(1).AutoAddSeatsAsync(Arg.Any<Organization>(), 1);

        // Assert — Staged row promoted to Invited and back-linked to the newly-provisioned user.
        var orgUserRepo = testData.Factory.Services.GetRequiredService<IOrganizationUserRepository>();
        var refreshedOrgUser = await orgUserRepo.GetByIdAsync(testData.OrganizationUser!.Id);
        Assert.NotNull(refreshedOrgUser);
        Assert.Equal(OrganizationUserStatusType.Invited, refreshedOrgUser.Status);
        Assert.NotNull(refreshedOrgUser.UserId);
    }

    /*
    * REGRESSION GUARD: Two-phase test verifying that when the seat check throws while
    * JIT-provisioning a BW User against a Staged OrganizationUser row, no BW User row
    * is persisted (Phase 1), and that after an admin adds seats the retry proceeds
    * cleanly through the standard JIT flow (Phase 2). Locks in the pre-user-creation
    * seat check ordering — if that ordering ever regresses, Phase 1 flips because a BW
    * User row appears despite the 500.
    */
    [Fact]
    public async Task ExternalCallback_JitAgainstStagedOrgUser_WhenSeatCheckThrows_RejectsCleanlyAndRecoversAfterCapIncrease()
    {
        // Phase 1 arrange — cloud org at cap, autoscale maxed (Seats == MaxAutoscaleSeats).
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithStagedOrganizationUser()
            .WithOrganization(org =>
            {
                org.Seats = 5;
                org.MaxAutoscaleSeats = 5;
            })
            .WithMockedSendOrganizationInvitesCommand()
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var ssoClaimedEmail = testData.OrganizationUser!.Email!;

        // Phase 1 act — first SSO attempt should be rejected.
        var response1 = await client.GetAsync("/Account/ExternalCallback");

        ExternalCallbackAssertions.AssertNoSeatsAvailableRedirect(response1);

        // Phase 1 assert — no BW User row was persisted. This is the fix's contract:
        // the seat check runs before RegisterSSOAutoProvisionedUserAsync so a rejection
        // does not leave account state behind for a login that never completed.
        var userRepo = testData.Factory.Services.GetRequiredService<IUserRepository>();
        var userAfterPhase1 = await userRepo.GetByEmailAsync(ssoClaimedEmail);
        Assert.Null(userAfterPhase1);

        // Phase 1 assert — Staged row unchanged and no SsoUser link exists.
        var orgUserRepo = testData.Factory.Services.GetRequiredService<IOrganizationUserRepository>();
        var stagedRowAfterPhase1 = await orgUserRepo.GetByIdAsync(testData.OrganizationUser!.Id);
        Assert.Equal(OrganizationUserStatusType.Staged, stagedRowAfterPhase1!.Status);
        Assert.Null(stagedRowAfterPhase1.UserId);

        // Phase 2 arrange — admin bumps seats and autoscale cap so the retry has headroom.
        var orgRepo = testData.Factory.Services.GetRequiredService<IOrganizationRepository>();
        var org = await orgRepo.GetByIdAsync(testData.Organization!.Id);
        org!.Seats = 6;
        org.MaxAutoscaleSeats = 6;
        await orgRepo.ReplaceAsync(org);

        // Phase 2 act — user retries SSO.
        var response2 = await client.GetAsync("/Account/ExternalCallback");

        // Phase 2 assert — standard successful SSO redirect (not a /login redirect with
        // an error code; the retry runs through fresh-JIT and passes the status filter).
        Assert.Equal(HttpStatusCode.Redirect, response2.StatusCode);
        Assert.NotNull(response2.Headers.Location);

        // Phase 2 assert — BW User row now exists, Staged row promoted to Invited and
        // back-linked to it, SsoUser link established.
        var userAfterPhase2 = await userRepo.GetByEmailAsync(ssoClaimedEmail);
        Assert.NotNull(userAfterPhase2);

        var stagedRowAfterPhase2 = await orgUserRepo.GetByIdAsync(testData.OrganizationUser!.Id);
        Assert.Equal(OrganizationUserStatusType.Invited, stagedRowAfterPhase2!.Status);
        Assert.Equal(userAfterPhase2!.Id, stagedRowAfterPhase2.UserId);

        var ssoUserRepo = testData.Factory.Services.GetRequiredService<ISsoUserRepository>();
        var ssoLinkAfterPhase2 = await ssoUserRepo.GetByUserIdOrganizationIdAsync(testData.Organization.Id, userAfterPhase2.Id);
        Assert.NotNull(ssoLinkAfterPhase2);
    }

    /*
    * SUCCESS PATH: Test to verify /Account/ExternalCallback promotes a Staged OrganizationUser
    * row to Invited, sends a real invite email via ISendOrganizationInvitesCommand, and redirects
    * to /login with the StagedOrgUserInviteAcceptanceRequired error when an existing BW user
    * (matched by email) attempts SSO against a Staged placeholder. Distinct from the standard
    * InviteAcceptanceRequired code so the client can tell the user to check their email for
    * the freshly-sent invite rather than referencing an invite they were expected to have.
    */
    [Fact]
    public async Task ExternalCallback_WithExistingUser_AndStagedOrgUser_PromotesInvitesAndRedirects()
    {
        // Arrange — existing BW user AND a Staged OrganizationUser row sharing the SSO-claimed email.
        // The Staged row has UserId=null (matches the directory-import shape).
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithStagedOrganizationUser()
            .WithMockedSendOrganizationInvitesCommand()
            .BuildAsync();

        var stagedRowInitialRevisionDate = testData.OrganizationUser!.RevisionDate;

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert — 302 to /login with the StagedOrgUserInviteAcceptanceRequired error code and context.
        ExternalCallbackAssertions.AssertStagedInviteAcceptanceRequiredRedirect(response);
        var location = response.Headers.Location!.ToString();
        Assert.Contains($"email={Uri.EscapeDataString(testData.User!.Email)}", location);
        Assert.Contains($"organizationId={testData.Organization!.Id}", location);

        // Assert — Staged row promoted to Invited, UserId left null (matches standard admin-invite
        // shape; UserId is set at accept time by the accept endpoint), RevisionDate bumped.
        var orgUserRepo = testData.Factory.Services.GetRequiredService<IOrganizationUserRepository>();
        var refreshedOrgUser = await orgUserRepo.GetByIdAsync(testData.OrganizationUser!.Id);
        Assert.NotNull(refreshedOrgUser);
        Assert.Equal(OrganizationUserStatusType.Invited, refreshedOrgUser.Status);
        Assert.Null(refreshedOrgUser.UserId);
        Assert.True(refreshedOrgUser.RevisionDate > stagedRowInitialRevisionDate,
            $"RevisionDate should be bumped after promotion. Before: {stagedRowInitialRevisionDate:O}, After: {refreshedOrgUser.RevisionDate:O}.");

        // Assert — invite email was issued for the promoted OrgUser, with invitingUserId=null
        // (matches the SCIM/automated pattern documented on SendInvitesRequest).
        var inviteCommand = testData.Factory.Services.GetRequiredService<ISendOrganizationInvitesCommand>();
        await inviteCommand.Received(1).SendInvitesAsync(Arg.Is<SendInvitesRequest>(r =>
            r.Users.Length == 1 &&
            r.Users[0].Id == testData.OrganizationUser!.Id &&
            r.Organization.Id == testData.Organization!.Id &&
            r.InvitingUserId == null));
    }

    /*
    * FAILURE PATH: Test to verify /Account/ExternalCallback rejects an existing-user Staged
    * promotion when the org is at its seat cap on self-hosted. Also asserts we do NOT send
    * an invite email (seat check must run before the email step) and do NOT mutate the row.
    */
    [Fact]
    public async Task ExternalCallback_WithExistingUser_AndStagedOrgUser_OnSelfHostedAtSeatCap_RedirectsAndDoesNotInvite()
    {
        // Arrange — Seats = 5, builder fills 5 Confirmed rows to reach cap. Staged row does not
        // count against the cap; promoting it would push us over.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithStagedOrganizationUser()
            .WithOrganization(org =>
            {
                org.Seats = 5;
            })
            .AsSelfHosted()
            .WithMockedSendOrganizationInvitesCommand()
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        ExternalCallbackAssertions.AssertNoSeatsAvailableRedirect(response);

        // Assert — no invite was sent (seat check must run before the email step).
        var inviteCommand = testData.Factory.Services.GetRequiredService<ISendOrganizationInvitesCommand>();
        await inviteCommand.DidNotReceive().SendInvitesAsync(Arg.Any<SendInvitesRequest>());

        // Assert — Staged row was not mutated.
        var orgUserRepo = testData.Factory.Services.GetRequiredService<IOrganizationUserRepository>();
        var refreshedOrgUser = await orgUserRepo.GetByIdAsync(testData.OrganizationUser!.Id);
        Assert.NotNull(refreshedOrgUser);
        Assert.Equal(OrganizationUserStatusType.Staged, refreshedOrgUser.Status);
        Assert.Null(refreshedOrgUser.UserId);
    }

    /*
    * FAILURE PATH: Mirror of the self-hosted seat-cap test for the cloud path where autoscale
    * cannot grow beyond MaxAutoscaleSeats. Same expectations: NoSeatsAvailable redirect, no
    * invite, no row mutation.
    */
    [Fact]
    public async Task ExternalCallback_WithExistingUser_AndStagedOrgUser_OnCloudAutoscaleFails_RedirectsAndDoesNotInvite()
    {
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithStagedOrganizationUser()
            .WithOrganization(org =>
            {
                org.Seats = 5;
                org.MaxAutoscaleSeats = 5;
            })
            .WithMockedSendOrganizationInvitesCommand()
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        ExternalCallbackAssertions.AssertNoSeatsAvailableRedirect(response);

        // Assert — no invite was sent.
        var inviteCommand = testData.Factory.Services.GetRequiredService<ISendOrganizationInvitesCommand>();
        await inviteCommand.DidNotReceive().SendInvitesAsync(Arg.Any<SendInvitesRequest>());

        // Assert — Staged row was not mutated.
        var orgUserRepo = testData.Factory.Services.GetRequiredService<IOrganizationUserRepository>();
        var refreshedOrgUser = await orgUserRepo.GetByIdAsync(testData.OrganizationUser!.Id);
        Assert.NotNull(refreshedOrgUser);
        Assert.Equal(OrganizationUserStatusType.Staged, refreshedOrgUser.Status);
        Assert.Null(refreshedOrgUser.UserId);
    }

    /*
    * FAILURE PATH: When SendInvitesAsync throws after the Staged row has already been
    * flipped to Invited, the promotion must roll back so the row stays Staged. Otherwise
    * a seat is consumed (Invited counts against the cap; Staged does not) for an invite
    * the user never received, and the next SSO attempt would dead-end on the "accept your
    * invite" redirect for an invite that was never delivered.
    */
    [Fact]
    public async Task ExternalCallback_WithExistingUser_AndStagedOrgUser_WhenInviteSendFails_RollsBackPromotion()
    {
        // Arrange — existing BW user AND a Staged OrganizationUser row sharing the SSO-claimed email.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithStagedOrganizationUser()
            .WithMockedSendOrganizationInvitesCommand()
            .BuildAsync();

        // Configure the mocked invite command to throw a plain Exception, simulating a
        // transient failure downstream of HandlebarsMailService (which is not one of the
        // typed exceptions caught by ExternalCallback).
        var inviteCommand = testData.Factory.Services.GetRequiredService<ISendOrganizationInvitesCommand>();
        inviteCommand
            .When(x => x.SendInvitesAsync(Arg.Any<SendInvitesRequest>()))
            .Do(_ => throw new Exception("simulated invite send failure"));

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert — plain Exception is not caught by any typed handler in ExternalCallback,
        // so it surfaces as a 500.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        // Assert — SendInvitesAsync was reached (proves the promotion cleared the seat
        // check and the ReplaceAsync before failing on the invite send).
        await inviteCommand.Received(1).SendInvitesAsync(Arg.Any<SendInvitesRequest>());

        // Assert — the Staged row was rolled back. Without rollback the row is left as
        // Invited and the next SSO attempt hits SsoAuthnRequiresInviteAcceptanceException,
        // stranding the user on /login for an invite that was never delivered.
        var orgUserRepo = testData.Factory.Services.GetRequiredService<IOrganizationUserRepository>();
        var refreshedOrgUser = await orgUserRepo.GetByIdAsync(testData.OrganizationUser!.Id);
        Assert.NotNull(refreshedOrgUser);
        Assert.Equal(OrganizationUserStatusType.Staged, refreshedOrgUser.Status);
        Assert.Null(refreshedOrgUser.UserId);
    }
}
