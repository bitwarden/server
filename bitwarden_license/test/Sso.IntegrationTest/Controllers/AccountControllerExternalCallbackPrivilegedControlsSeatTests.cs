using System.Net;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models.Mail.Mailer;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Bit.Core.Test.Billing.Mocks;
using Bit.Core.Utilities;
using Bit.Sso.IntegrationTest.Utilities;
using Microsoft.AspNetCore.Mvc.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;
using Xunit;

namespace Bit.Sso.IntegrationTest.Controllers;

/// <summary>
/// When an SSO sign-in moves a Staged member that has Privileged Controls access to Invited, the member starts
/// to use a Privileged Controls seat. The SSO callback must reserve that seat first. When no seat can be reserved,
/// the member joins without Privileged Controls access, and on cloud the organization's Owners get an email that
/// the member needs a seat.
/// </summary>
/// <remarks>
/// Each test does the SSO callback for one of two sign-ins against a Staged member: a person with no Bitwarden
/// account, whom SSO provisions just in time, or a person with an existing Bitwarden account, who gets an invite
/// email. Password Manager seats are unlimited unless a test sets them, so the Password Manager seat check passes.
/// Plans come from a substituted pricing client, so no test calls the pricing service. Each test sets
/// <see cref="FeatureFlagKeys.PM39883_Pam_Billing_Support"/>. Most turn it on, because seat reservations on cloud do
/// nothing while it is off.
/// </remarks>
public class AccountControllerExternalCallbackPrivilegedControlsSeatTests
{
    private const string _billingSupportFlagSetting =
        $"globalSettings:launchDarkly:flagValues:{FeatureFlagKeys.PM39883_Pam_Billing_Support}";

    /*
    * SUCCESS PATH: A just-in-time provisioned user claims a Staged member with Privileged Controls access, and
    * the organization has a free Privileged Controls seat. The member keeps access, no seat is bought, and the
    * Owners get no seat-needed email. The member brings the seats in use to exactly the autoscale limit, so the
    * Owners get the limit-reached email.
    */
    [Fact]
    public async Task ExternalCallback_JitAgainstStagedOrgUserWithPrivilegedControls_WithFreeSeat_KeepsPrivilegedControls()
    {
        // Arrange — one Privileged Controls seat, none taken, autoscale limit of one.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithMockedPricingClient()
            .WithConfiguration(_billingSupportFlagSetting, "true")
            .WithOrganization(org => WithPrivilegedControlsSeats(org, pamSeats: 1, maxAutoscalePamSeats: 1))
            .WithStagedOrganizationUser(orgUser => orgUser.AccessPam = true)
            .WithMockedMailer()
            .BuildAsync();
        var ownerEmail = await SeedOwnerAsync(testData);

        // Act
        var response = await CreateNonRedirectingClient(testData).GetAsync("/Account/ExternalCallback");

        // Assert
        AssertSsoSucceeded(response);

        var orgUser = await GetOrgUserAsync(testData);
        Assert.Equal(OrganizationUserStatusType.Invited, orgUser.Status);
        Assert.NotNull(orgUser.UserId);
        Assert.True(orgUser.AccessPam);

        Assert.Equal(1, (await GetOrganizationAsync(testData)).PamSeats);
        await AssertSeatNeededEmailNotSentAsync(testData);
        await AssertSeatLimitReachedEmailSentAsync(testData, ownerEmail, maxSeatCount: 1);
    }

    /*
    * SUCCESS PATH: A just-in-time provisioned user claims a Staged member with Privileged Controls access. All
    * Privileged Controls seats are taken, but the organization has no autoscale limit. The SSO callback adds one
    * seat and the member keeps access.
    *
    * The organization has no subscription id, so the autoscale treats it as a sales-initiated trial and raises
    * the seat count without a Stripe call.
    */
    [Fact]
    public async Task ExternalCallback_JitAgainstStagedOrgUserWithPrivilegedControls_WhenAutoscaleCanGrow_AddsSeatAndKeepsPrivilegedControls()
    {
        // Arrange — one Privileged Controls seat, already taken, no autoscale limit.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithMockedPricingClient()
            .WithConfiguration(_billingSupportFlagSetting, "true")
            .WithOrganization(org => WithPrivilegedControlsSeats(org, pamSeats: 1, maxAutoscalePamSeats: null))
            .WithStagedOrganizationUser(orgUser => orgUser.AccessPam = true)
            .WithMockedMailer()
            .BuildAsync();
        await SeedOwnerAsync(testData);
        await SeedPrivilegedControlsSeatHolderAsync(testData);

        // Act
        var response = await CreateNonRedirectingClient(testData).GetAsync("/Account/ExternalCallback");

        // Assert
        AssertSsoSucceeded(response);

        var orgUser = await GetOrgUserAsync(testData);
        Assert.Equal(OrganizationUserStatusType.Invited, orgUser.Status);
        Assert.True(orgUser.AccessPam);

        Assert.Equal(2, (await GetOrganizationAsync(testData)).PamSeats);
        await AssertNoPrivilegedControlsEmailSentAsync(testData);
    }

    /*
    * SUCCESS PATH: A just-in-time provisioned user claims a Staged member with Privileged Controls access. All
    * Privileged Controls seats are taken, the organization has no autoscale limit, and it pays for its seats
    * through a Stripe subscription. The SSO callback buys one seat on the subscription and the member keeps
    * access.
    */
    [Fact]
    public async Task ExternalCallback_JitAgainstStagedOrgUserWithPrivilegedControls_WhenAutoscaleCanGrowOnPaidSubscription_BuysSeatAndKeepsPrivilegedControls()
    {
        // Arrange — one Privileged Controls seat, already taken, no autoscale limit, billed on a Stripe subscription.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithMockedPricingClient()
            .WithConfiguration(_billingSupportFlagSetting, "true")
            .WithOrganization(org =>
            {
                WithPrivilegedControlsSeats(org, pamSeats: 1, maxAutoscalePamSeats: null);
                org.GatewaySubscriptionId = _subscriptionId;
            })
            .WithStagedOrganizationUser(orgUser => orgUser.AccessPam = true)
            .WithMockedStripeAdapter(ReturnSubscriptionWithOnePrivilegedControlsSeat)
            .WithMockedMailer()
            .BuildAsync();
        await SeedOwnerAsync(testData);
        await SeedPrivilegedControlsSeatHolderAsync(testData);

        // Act
        var response = await CreateNonRedirectingClient(testData).GetAsync("/Account/ExternalCallback");

        // Assert
        AssertSsoSucceeded(response);

        var orgUser = await GetOrgUserAsync(testData);
        Assert.Equal(OrganizationUserStatusType.Invited, orgUser.Status);
        Assert.True(orgUser.AccessPam);

        Assert.Equal(2, (await GetOrganizationAsync(testData)).PamSeats);
        var stripeAdapter = testData.Factory.Services.GetRequiredService<IStripeAdapter>();
        await stripeAdapter.Received(1).UpdateSubscriptionAsync(_subscriptionId,
            Arg.Is<SubscriptionUpdateOptions>(options => options.Items.Any(item =>
                item.Id == _privilegedControlsSeatItemId && item.Quantity == 2)));
        await AssertNoPrivilegedControlsEmailSentAsync(testData);
    }

    /*
    * FAILURE PATH: A just-in-time provisioned user claims a Staged member with Privileged Controls access. All
    * Privileged Controls seats are taken, so autoscale buys one before the user is created. Creating the user then
    * fails, because another organization claims the email domain and blocks account creation. The member stays
    * Staged with Privileged Controls access, no user is created, and the seat that autoscale bought stays bought.
    */
    [Fact]
    public async Task ExternalCallback_JitAgainstStagedOrgUserWithPrivilegedControls_WhenRegistrationFailsAfterSeatBought_KeepsSeatAndStaysStaged()
    {
        // Arrange — one Privileged Controls seat, already taken, no autoscale limit. Another organization claims
        // the member's email domain and blocks account creation for it.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithMockedPricingClient()
            .WithConfiguration(_billingSupportFlagSetting, "true")
            .WithOrganization(org => WithPrivilegedControlsSeats(org, pamSeats: 1, maxAutoscalePamSeats: null))
            .WithStagedOrganizationUser(orgUser => orgUser.AccessPam = true)
            .WithMockedMailer()
            .BuildAsync();
        await SeedOwnerAsync(testData);
        await SeedPrivilegedControlsSeatHolderAsync(testData);
        var claimedEmail = testData.OrganizationUser!.Email!;
        await SeedOtherOrganizationBlockingDomainAsync(testData, claimedEmail.Split('@')[1]);

        // Act
        var response = await CreateNonRedirectingClient(testData).GetAsync("/Account/ExternalCallback");

        // Assert — the domain claim blocks registration, and the error surfaces as a 500.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("This email address is claimed by an organization using Bitwarden.",
            await response.Content.ReadAsStringAsync());

        var userRepository = testData.Factory.Services.GetRequiredService<IUserRepository>();
        Assert.Null(await userRepository.GetByEmailAsync(claimedEmail));

        var orgUser = await GetOrgUserAsync(testData);
        Assert.Equal(OrganizationUserStatusType.Staged, orgUser.Status);
        Assert.Null(orgUser.UserId);
        Assert.True(orgUser.AccessPam);

        Assert.Equal(2, (await GetOrganizationAsync(testData)).PamSeats);
        await AssertNoPrivilegedControlsEmailSentAsync(testData);
    }

    /*
    * DEGRADED PATH: A just-in-time provisioned user claims a Staged member with Privileged Controls access, and
    * the organization is at its Privileged Controls autoscale limit. The sign-in still succeeds. The member joins
    * without Privileged Controls access, and the Owners get an email that names the member.
    */
    [Fact]
    public async Task ExternalCallback_JitAgainstStagedOrgUserWithPrivilegedControls_AtSeatLimit_JoinsWithoutPrivilegedControlsAndEmailsOwners()
    {
        // Arrange — one Privileged Controls seat, already taken, autoscale limit of one.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithMockedPricingClient()
            .WithConfiguration(_billingSupportFlagSetting, "true")
            .WithOrganization(org => WithPrivilegedControlsSeats(org, pamSeats: 1, maxAutoscalePamSeats: 1))
            .WithStagedOrganizationUser(orgUser => orgUser.AccessPam = true)
            .WithMockedMailer()
            .BuildAsync();
        var ownerEmail = await SeedOwnerAsync(testData);
        await SeedPrivilegedControlsSeatHolderAsync(testData);

        // Act
        var response = await CreateNonRedirectingClient(testData).GetAsync("/Account/ExternalCallback");

        // Assert
        AssertSsoSucceeded(response);

        var orgUser = await GetOrgUserAsync(testData);
        Assert.Equal(OrganizationUserStatusType.Invited, orgUser.Status);
        Assert.NotNull(orgUser.UserId);
        Assert.False(orgUser.AccessPam);

        Assert.Equal(1, (await GetOrganizationAsync(testData)).PamSeats);
        await AssertSeatNeededEmailSentAsync(testData, ownerEmail, testData.OrganizationUser!.Email!);
    }

    /*
    * SUCCESS PATH: On cloud with the billing support flag off, a just-in-time provisioned user claims a Staged
    * member with Privileged Controls access, and the organization is at its Privileged Controls autoscale limit.
    * With the flag off, the reservation does not check seats, so the member keeps access, no seat is bought, and
    * the Owners get no email.
    */
    [Fact]
    public async Task ExternalCallback_JitAgainstStagedOrgUserWithPrivilegedControls_AtSeatLimitWithBillingSupportFlagOff_KeepsPrivilegedControlsWithoutReservingSeat()
    {
        // Arrange — one Privileged Controls seat, already taken, autoscale limit of one, billing support flag off.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithMockedPricingClient()
            .WithConfiguration(_billingSupportFlagSetting, "false")
            .WithOrganization(org => WithPrivilegedControlsSeats(org, pamSeats: 1, maxAutoscalePamSeats: 1))
            .WithStagedOrganizationUser(orgUser => orgUser.AccessPam = true)
            .WithMockedMailer()
            .BuildAsync();
        await SeedOwnerAsync(testData);
        await SeedPrivilegedControlsSeatHolderAsync(testData);

        // Act
        var response = await CreateNonRedirectingClient(testData).GetAsync("/Account/ExternalCallback");

        // Assert
        AssertSsoSucceeded(response);

        var orgUser = await GetOrgUserAsync(testData);
        Assert.Equal(OrganizationUserStatusType.Invited, orgUser.Status);
        Assert.NotNull(orgUser.UserId);
        Assert.True(orgUser.AccessPam);

        Assert.Equal(1, (await GetOrganizationAsync(testData)).PamSeats);
        await AssertNoPrivilegedControlsEmailSentAsync(testData);
    }

    /*
    * DEGRADED PATH: A just-in-time provisioned user claims a Staged member with Privileged Controls access, and
    * the organization has zero Privileged Controls seats. Autoscale cannot add seats to an organization that has
    * none, so the member joins without Privileged Controls access and the Owners get an email.
    */
    [Fact]
    public async Task ExternalCallback_JitAgainstStagedOrgUserWithPrivilegedControls_WithZeroSeats_JoinsWithoutPrivilegedControlsAndEmailsOwners()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithMockedPricingClient()
            .WithConfiguration(_billingSupportFlagSetting, "true")
            .WithOrganization(org => WithPrivilegedControlsSeats(org, pamSeats: 0, maxAutoscalePamSeats: null))
            .WithStagedOrganizationUser(orgUser => orgUser.AccessPam = true)
            .WithMockedMailer()
            .BuildAsync();
        var ownerEmail = await SeedOwnerAsync(testData);

        // Act
        var response = await CreateNonRedirectingClient(testData).GetAsync("/Account/ExternalCallback");

        // Assert
        AssertSsoSucceeded(response);

        var orgUser = await GetOrgUserAsync(testData);
        Assert.Equal(OrganizationUserStatusType.Invited, orgUser.Status);
        Assert.False(orgUser.AccessPam);

        Assert.Equal(0, (await GetOrganizationAsync(testData)).PamSeats);
        await AssertSeatNeededEmailSentAsync(testData, ownerEmail, testData.OrganizationUser!.Email!);
    }

    /*
    * DEGRADED PATH: On a self-hosted instance, a just-in-time provisioned user claims a Staged member with
    * Privileged Controls access, and no Privileged Controls seat is free. Self-hosted instances cannot autoscale,
    * so the member joins without Privileged Controls access. Self-hosted instances send no Privileged Controls
    * email.
    */
    [Fact]
    public async Task ExternalCallback_JitAgainstStagedOrgUserWithPrivilegedControls_OnSelfHostedWithNoFreeSeat_JoinsWithoutPrivilegedControlsAndDoesNotEmail()
    {
        // Arrange — one Privileged Controls seat, already taken.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithMockedPricingClient()
            .WithConfiguration(_billingSupportFlagSetting, "true")
            .WithOrganization(org => WithPrivilegedControlsSeats(org, pamSeats: 1, maxAutoscalePamSeats: null))
            .WithStagedOrganizationUser(orgUser => orgUser.AccessPam = true)
            .AsSelfHosted()
            .WithSelfHostedEnvironment()
            .WithMockedMailer()
            .BuildAsync();
        await SeedOwnerAsync(testData);
        await SeedPrivilegedControlsSeatHolderAsync(testData);

        // Act
        var response = await CreateNonRedirectingClient(testData).GetAsync("/Account/ExternalCallback");

        // Assert
        AssertSsoSucceeded(response);

        var orgUser = await GetOrgUserAsync(testData);
        Assert.Equal(OrganizationUserStatusType.Invited, orgUser.Status);
        Assert.False(orgUser.AccessPam);

        Assert.Equal(1, (await GetOrganizationAsync(testData)).PamSeats);
        await AssertNoPrivilegedControlsEmailSentAsync(testData);
    }

    /*
    * SUCCESS PATH: A just-in-time provisioned user claims a Staged member without Privileged Controls access.
    * The member does not start to use a Privileged Controls seat, so the SSO callback reserves none, even when
    * the organization is at its Privileged Controls seat limit.
    */
    [Fact]
    public async Task ExternalCallback_JitAgainstStagedOrgUserWithoutPrivilegedControls_AtSeatLimit_DoesNotReserveSeatOrEmail()
    {
        // Arrange — one Privileged Controls seat, already taken, autoscale limit of one.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithMockedPricingClient()
            .WithConfiguration(_billingSupportFlagSetting, "true")
            .WithOrganization(org => WithPrivilegedControlsSeats(org, pamSeats: 1, maxAutoscalePamSeats: 1))
            .WithStagedOrganizationUser(orgUser => orgUser.AccessPam = false)
            .WithMockedMailer()
            .BuildAsync();
        await SeedOwnerAsync(testData);
        await SeedPrivilegedControlsSeatHolderAsync(testData);

        // Act
        var response = await CreateNonRedirectingClient(testData).GetAsync("/Account/ExternalCallback");

        // Assert
        AssertSsoSucceeded(response);

        var orgUser = await GetOrgUserAsync(testData);
        Assert.Equal(OrganizationUserStatusType.Invited, orgUser.Status);
        Assert.False(orgUser.AccessPam);

        Assert.Equal(1, (await GetOrganizationAsync(testData)).PamSeats);
        await AssertNoPrivilegedControlsEmailSentAsync(testData);
    }

    /*
    * FAILURE PATH: A just-in-time provisioned user claims a Staged member with Privileged Controls access, but
    * the organization has no Password Manager seat and cannot autoscale one. The Password Manager seat check runs
    * first and stops the sign-in, so the SSO callback must not buy a Privileged Controls seat for a member who
    * never joins.
    */
    [Fact]
    public async Task ExternalCallback_JitAgainstStagedOrgUserWithPrivilegedControls_WhenNoPasswordManagerSeat_DoesNotReservePrivilegedControlsSeat()
    {
        // Arrange — Password Manager seats at cap and at the autoscale limit. Privileged Controls autoscale would
        // succeed (no limit), so a reservation would show as a raised PamSeats value.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithMockedPricingClient()
            .WithConfiguration(_billingSupportFlagSetting, "true")
            .WithOrganization(org =>
            {
                org.Seats = 5;
                org.MaxAutoscaleSeats = 5;
                WithPrivilegedControlsSeats(org, pamSeats: 1, maxAutoscalePamSeats: null);
            })
            .WithStagedOrganizationUser(orgUser => orgUser.AccessPam = true)
            .WithMockedMailer()
            .BuildAsync();
        await SeedPrivilegedControlsSeatHolderAsync(testData);

        // Act
        var response = await CreateNonRedirectingClient(testData).GetAsync("/Account/ExternalCallback");

        // Assert
        ExternalCallbackAssertions.AssertNoSeatsAvailableRedirect(response);

        var orgUser = await GetOrgUserAsync(testData);
        Assert.Equal(OrganizationUserStatusType.Staged, orgUser.Status);
        Assert.Null(orgUser.UserId);
        Assert.True(orgUser.AccessPam);

        Assert.Equal(1, (await GetOrganizationAsync(testData)).PamSeats);
        await AssertNoPrivilegedControlsEmailSentAsync(testData);
    }

    /*
    * SUCCESS PATH: A person with an existing Bitwarden account signs in with SSO against a Staged member with
    * Privileged Controls access, and the organization has a free Privileged Controls seat. The member moves to
    * Invited with access, gets an invite email, and the Owners get no seat-needed email. The member brings the
    * seats in use to exactly the autoscale limit, so the Owners get the limit-reached email.
    */
    [Fact]
    public async Task ExternalCallback_ExistingUserAgainstStagedOrgUserWithPrivilegedControls_WithFreeSeat_InvitesWithPrivilegedControls()
    {
        // Arrange — one Privileged Controls seat, none taken, autoscale limit of one.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithMockedPricingClient()
            .WithConfiguration(_billingSupportFlagSetting, "true")
            .WithOrganization(org => WithPrivilegedControlsSeats(org, pamSeats: 1, maxAutoscalePamSeats: 1))
            .WithUser()
            .WithStagedOrganizationUser(orgUser => orgUser.AccessPam = true)
            .WithMockedSendOrganizationInvitesCommand()
            .WithMockedMailer()
            .BuildAsync();
        var ownerEmail = await SeedOwnerAsync(testData);

        // Act
        var response = await CreateNonRedirectingClient(testData).GetAsync("/Account/ExternalCallback");

        // Assert
        AssertStagedInviteAcceptanceRequiredRedirect(response);

        var orgUser = await GetOrgUserAsync(testData);
        Assert.Equal(OrganizationUserStatusType.Invited, orgUser.Status);
        Assert.Null(orgUser.UserId);
        Assert.True(orgUser.AccessPam);

        var inviteCommand = testData.Factory.Services.GetRequiredService<ISendOrganizationInvitesCommand>();
        await inviteCommand.Received(1).SendInvitesAsync(Arg.Is<SendInvitesRequest>(r =>
            r.Users.Length == 1 &&
            r.Users[0].Id == testData.OrganizationUser!.Id &&
            r.Users[0].AccessPam));

        Assert.Equal(1, (await GetOrganizationAsync(testData)).PamSeats);
        await AssertSeatNeededEmailNotSentAsync(testData);
        await AssertSeatLimitReachedEmailSentAsync(testData, ownerEmail, maxSeatCount: 1);
    }

    /*
    * DEGRADED PATH: A person with an existing Bitwarden account signs in with SSO against a Staged member with
    * Privileged Controls access, and the organization is at its Privileged Controls autoscale limit. The member
    * moves to Invited without access and gets an invite email. The Owners get an email that names the member.
    */
    [Fact]
    public async Task ExternalCallback_ExistingUserAgainstStagedOrgUserWithPrivilegedControls_AtSeatLimit_InvitesWithoutPrivilegedControlsAndEmailsOwners()
    {
        // Arrange — one Privileged Controls seat, already taken, autoscale limit of one.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithMockedPricingClient()
            .WithConfiguration(_billingSupportFlagSetting, "true")
            .WithOrganization(org => WithPrivilegedControlsSeats(org, pamSeats: 1, maxAutoscalePamSeats: 1))
            .WithUser()
            .WithStagedOrganizationUser(orgUser => orgUser.AccessPam = true)
            .WithMockedSendOrganizationInvitesCommand()
            .WithMockedMailer()
            .BuildAsync();
        var ownerEmail = await SeedOwnerAsync(testData);
        await SeedPrivilegedControlsSeatHolderAsync(testData);

        // Act
        var response = await CreateNonRedirectingClient(testData).GetAsync("/Account/ExternalCallback");

        // Assert
        AssertStagedInviteAcceptanceRequiredRedirect(response);

        var orgUser = await GetOrgUserAsync(testData);
        Assert.Equal(OrganizationUserStatusType.Invited, orgUser.Status);
        Assert.False(orgUser.AccessPam);

        var inviteCommand = testData.Factory.Services.GetRequiredService<ISendOrganizationInvitesCommand>();
        await inviteCommand.Received(1).SendInvitesAsync(Arg.Is<SendInvitesRequest>(r =>
            r.Users.Length == 1 &&
            r.Users[0].Id == testData.OrganizationUser!.Id &&
            !r.Users[0].AccessPam));

        Assert.Equal(1, (await GetOrganizationAsync(testData)).PamSeats);
        await AssertSeatNeededEmailSentAsync(testData, ownerEmail, testData.OrganizationUser!.Email!);
    }

    /*
    * FAILURE PATH: A person with an existing Bitwarden account signs in with SSO against a Staged member with
    * Privileged Controls access at the seat limit, and the invite email fails. The SSO callback rolls the member
    * back to Staged and gives back Privileged Controls access, so the next sign-in starts again from the same
    * state. The Owners get no Privileged Controls email, because the member did not join.
    */
    [Fact]
    public async Task ExternalCallback_ExistingUserAgainstStagedOrgUserWithPrivilegedControls_AtSeatLimit_WhenInviteSendFails_RestoresPrivilegedControlsAndDoesNotEmail()
    {
        // Arrange — one Privileged Controls seat, already taken, autoscale limit of one.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithMockedPricingClient()
            .WithConfiguration(_billingSupportFlagSetting, "true")
            .WithOrganization(org => WithPrivilegedControlsSeats(org, pamSeats: 1, maxAutoscalePamSeats: 1))
            .WithUser()
            .WithStagedOrganizationUser(orgUser => orgUser.AccessPam = true)
            .WithMockedSendOrganizationInvitesCommand()
            .WithMockedMailer()
            .BuildAsync();
        await SeedOwnerAsync(testData);
        await SeedPrivilegedControlsSeatHolderAsync(testData);

        var inviteCommand = testData.Factory.Services.GetRequiredService<ISendOrganizationInvitesCommand>();
        inviteCommand
            .When(x => x.SendInvitesAsync(Arg.Any<SendInvitesRequest>()))
            .Do(_ => throw new Exception("simulated invite send failure"));

        // Act
        var response = await testData.Factory.CreateClient().GetAsync("/Account/ExternalCallback");

        // Assert — the untyped exception surfaces as a 500, after the invite step was reached.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await inviteCommand.Received(1).SendInvitesAsync(Arg.Any<SendInvitesRequest>());

        var orgUser = await GetOrgUserAsync(testData);
        Assert.Equal(OrganizationUserStatusType.Staged, orgUser.Status);
        Assert.Null(orgUser.UserId);
        Assert.True(orgUser.AccessPam);

        await AssertNoPrivilegedControlsEmailSentAsync(testData);
    }

    /*
    * FAILURE PATH: A person with an existing Bitwarden account signs in with SSO against a Staged member with
    * Privileged Controls access. All Privileged Controls seats are taken, so autoscale buys one, and then the
    * invite email fails. The SSO callback rolls the member back to Staged with Privileged Controls access. The
    * seat that autoscale bought stays bought.
    */
    [Fact]
    public async Task ExternalCallback_ExistingUserAgainstStagedOrgUserWithPrivilegedControls_WhenInviteSendFailsAfterSeatBought_KeepsSeatAndRestoresStaged()
    {
        // Arrange — one Privileged Controls seat, already taken, no autoscale limit.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithMockedPricingClient()
            .WithConfiguration(_billingSupportFlagSetting, "true")
            .WithOrganization(org => WithPrivilegedControlsSeats(org, pamSeats: 1, maxAutoscalePamSeats: null))
            .WithUser()
            .WithStagedOrganizationUser(orgUser => orgUser.AccessPam = true)
            .WithMockedSendOrganizationInvitesCommand()
            .WithMockedMailer()
            .BuildAsync();
        await SeedOwnerAsync(testData);
        await SeedPrivilegedControlsSeatHolderAsync(testData);

        var inviteCommand = testData.Factory.Services.GetRequiredService<ISendOrganizationInvitesCommand>();
        inviteCommand
            .When(x => x.SendInvitesAsync(Arg.Any<SendInvitesRequest>()))
            .Do(_ => throw new Exception("simulated invite send failure"));

        // Act
        var response = await testData.Factory.CreateClient().GetAsync("/Account/ExternalCallback");

        // Assert — the untyped exception surfaces as a 500, after the invite step was reached.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await inviteCommand.Received(1).SendInvitesAsync(Arg.Any<SendInvitesRequest>());

        var orgUser = await GetOrgUserAsync(testData);
        Assert.Equal(OrganizationUserStatusType.Staged, orgUser.Status);
        Assert.Null(orgUser.UserId);
        Assert.True(orgUser.AccessPam);

        Assert.Equal(2, (await GetOrganizationAsync(testData)).PamSeats);
        await AssertNoPrivilegedControlsEmailSentAsync(testData);
    }

    /*
    * DEGRADED PATH: On cloud, a just-in-time provisioned user claims a Staged member with Privileged Controls
    * access, and the seat reservation fails for a reason other than the seat limit. The member joins without
    * Privileged Controls access. The Owners get no Privileged Controls email, because more seats would not fix
    * the failure.
    */
    [Fact]
    public async Task ExternalCallback_JitAgainstStagedOrgUserWithPrivilegedControls_WhenReservationFailsUnexpectedly_JoinsWithoutPrivilegedControlsAndDoesNotEmail()
    {
        // Arrange — one Privileged Controls seat, already taken, no autoscale limit. The autoscale must read the
        // Stripe subscription, and that read fails.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithMockedPricingClient()
            .WithConfiguration(_billingSupportFlagSetting, "true")
            .WithOrganization(org =>
            {
                WithPrivilegedControlsSeats(org, pamSeats: 1, maxAutoscalePamSeats: null);
                org.GatewaySubscriptionId = _subscriptionId;
            })
            .WithStagedOrganizationUser(orgUser => orgUser.AccessPam = true)
            .WithMockedStripeAdapter(FailSubscriptionLookup)
            .WithMockedMailer()
            .BuildAsync();
        await SeedOwnerAsync(testData);
        await SeedPrivilegedControlsSeatHolderAsync(testData);

        // Act
        var response = await CreateNonRedirectingClient(testData).GetAsync("/Account/ExternalCallback");

        // Assert
        AssertSsoSucceeded(response);
        await AssertSubscriptionLookupAttemptedAsync(testData);

        var orgUser = await GetOrgUserAsync(testData);
        Assert.Equal(OrganizationUserStatusType.Invited, orgUser.Status);
        Assert.False(orgUser.AccessPam);

        Assert.Equal(1, (await GetOrganizationAsync(testData)).PamSeats);
        await AssertNoPrivilegedControlsEmailSentAsync(testData);
    }

    /*
    * SUCCESS PATH: A person with an existing Bitwarden account signs in with SSO against a Staged member without
    * Privileged Controls access. The member does not start to use a Privileged Controls seat, so the SSO callback
    * reserves none, even when the organization is at its Privileged Controls seat limit.
    */
    [Fact]
    public async Task ExternalCallback_ExistingUserAgainstStagedOrgUserWithoutPrivilegedControls_AtSeatLimit_DoesNotReserveSeatOrEmail()
    {
        // Arrange — one Privileged Controls seat, already taken, autoscale limit of one.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithMockedPricingClient()
            .WithConfiguration(_billingSupportFlagSetting, "true")
            .WithOrganization(org => WithPrivilegedControlsSeats(org, pamSeats: 1, maxAutoscalePamSeats: 1))
            .WithUser()
            .WithStagedOrganizationUser(orgUser => orgUser.AccessPam = false)
            .WithMockedSendOrganizationInvitesCommand()
            .WithMockedMailer()
            .BuildAsync();
        await SeedOwnerAsync(testData);
        await SeedPrivilegedControlsSeatHolderAsync(testData);

        // Act
        var response = await CreateNonRedirectingClient(testData).GetAsync("/Account/ExternalCallback");

        // Assert
        AssertStagedInviteAcceptanceRequiredRedirect(response);

        var orgUser = await GetOrgUserAsync(testData);
        Assert.Equal(OrganizationUserStatusType.Invited, orgUser.Status);
        Assert.False(orgUser.AccessPam);

        Assert.Equal(1, (await GetOrganizationAsync(testData)).PamSeats);
        await AssertNoPrivilegedControlsEmailSentAsync(testData);
    }

    /*
    * FAILURE PATH: A person with an existing Bitwarden account signs in with SSO against a Staged member with
    * Privileged Controls access, but the organization has no Password Manager seat and cannot autoscale one. The
    * Password Manager seat check runs first and stops the sign-in, so the SSO callback must not buy a Privileged
    * Controls seat or send an invite.
    */
    [Fact]
    public async Task ExternalCallback_ExistingUserAgainstStagedOrgUserWithPrivilegedControls_WhenNoPasswordManagerSeat_DoesNotReservePrivilegedControlsSeat()
    {
        // Arrange — Password Manager seats at cap and at the autoscale limit. Privileged Controls autoscale would
        // succeed (no limit), so a reservation would show as a raised PamSeats value.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithMockedPricingClient()
            .WithConfiguration(_billingSupportFlagSetting, "true")
            .WithOrganization(org =>
            {
                org.Seats = 5;
                org.MaxAutoscaleSeats = 5;
                WithPrivilegedControlsSeats(org, pamSeats: 1, maxAutoscalePamSeats: null);
            })
            .WithUser()
            .WithStagedOrganizationUser(orgUser => orgUser.AccessPam = true)
            .WithMockedSendOrganizationInvitesCommand()
            .WithMockedMailer()
            .BuildAsync();
        await SeedPrivilegedControlsSeatHolderAsync(testData);

        // Act
        var response = await CreateNonRedirectingClient(testData).GetAsync("/Account/ExternalCallback");

        // Assert
        ExternalCallbackAssertions.AssertNoSeatsAvailableRedirect(response);

        var inviteCommand = testData.Factory.Services.GetRequiredService<ISendOrganizationInvitesCommand>();
        await inviteCommand.DidNotReceive().SendInvitesAsync(Arg.Any<SendInvitesRequest>());

        var orgUser = await GetOrgUserAsync(testData);
        Assert.Equal(OrganizationUserStatusType.Staged, orgUser.Status);
        Assert.True(orgUser.AccessPam);

        Assert.Equal(1, (await GetOrganizationAsync(testData)).PamSeats);
        await AssertNoPrivilegedControlsEmailSentAsync(testData);
    }

    /*
    * DEGRADED PATH: On cloud, a person with an existing Bitwarden account signs in with SSO against a Staged
    * member with Privileged Controls access, and the seat reservation fails for a reason other than the seat
    * limit. The member is invited without Privileged Controls access. The Owners get no Privileged Controls
    * email, because more seats would not fix the failure.
    */
    [Fact]
    public async Task ExternalCallback_ExistingUserAgainstStagedOrgUserWithPrivilegedControls_WhenReservationFailsUnexpectedly_InvitesWithoutPrivilegedControlsAndDoesNotEmail()
    {
        // Arrange — one Privileged Controls seat, already taken, no autoscale limit. The autoscale must read the
        // Stripe subscription, and that read fails.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithMockedPricingClient()
            .WithConfiguration(_billingSupportFlagSetting, "true")
            .WithOrganization(org =>
            {
                WithPrivilegedControlsSeats(org, pamSeats: 1, maxAutoscalePamSeats: null);
                org.GatewaySubscriptionId = _subscriptionId;
            })
            .WithUser()
            .WithStagedOrganizationUser(orgUser => orgUser.AccessPam = true)
            .WithMockedSendOrganizationInvitesCommand()
            .WithMockedStripeAdapter(FailSubscriptionLookup)
            .WithMockedMailer()
            .BuildAsync();
        await SeedOwnerAsync(testData);
        await SeedPrivilegedControlsSeatHolderAsync(testData);

        // Act
        var response = await CreateNonRedirectingClient(testData).GetAsync("/Account/ExternalCallback");

        // Assert
        AssertStagedInviteAcceptanceRequiredRedirect(response);
        await AssertSubscriptionLookupAttemptedAsync(testData);

        var orgUser = await GetOrgUserAsync(testData);
        Assert.Equal(OrganizationUserStatusType.Invited, orgUser.Status);
        Assert.False(orgUser.AccessPam);

        Assert.Equal(1, (await GetOrganizationAsync(testData)).PamSeats);
        await AssertNoPrivilegedControlsEmailSentAsync(testData);
    }

    private const string _subscriptionId = "sub_integration_test";
    private const string _privilegedControlsSeatItemId = "si_privileged_controls_seats";

    /// <summary>
    /// Makes Stripe return an active, paid subscription that bills one Privileged Controls seat, both when the
    /// subscription is read and when it is updated.
    /// </summary>
    private static void ReturnSubscriptionWithOnePrivilegedControlsSeat(IStripeAdapter stripeAdapter)
    {
        var plan = MockPlans.Get(PlanType.EnterpriseAnnually);
        var subscription = new Subscription
        {
            Id = _subscriptionId,
            Status = StripeConstants.SubscriptionStatus.Active,
            CollectionMethod = StripeConstants.CollectionMethod.ChargeAutomatically,
            Customer = new Customer { Id = "cus_integration_test" },
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        Id = _privilegedControlsSeatItemId,
                        Quantity = 1,
                        Price = new Price
                        {
                            Id = plan.PrivilegedControls.StripeSeatPlanId,
                            Recurring = new PriceRecurring { Interval = StripeConstants.Intervals.Year },
                            Metadata = new Dictionary<string, string>
                            {
                                [StripeConstants.MetadataKeys.PurchasableReference] =
                                    StripeConstants.PurchasableReferences.PrivilegedControlsSeat
                            }
                        }
                    }
                ]
            }
        };
        stripeAdapter.GetSubscriptionAsync(_subscriptionId, Arg.Any<SubscriptionGetOptions>()).Returns(subscription);
        stripeAdapter.UpdateSubscriptionAsync(_subscriptionId, Arg.Any<SubscriptionUpdateOptions>()).Returns(subscription);
    }

    private static void FailSubscriptionLookup(IStripeAdapter stripeAdapter) =>
        stripeAdapter.GetSubscriptionAsync(_subscriptionId, Arg.Any<SubscriptionGetOptions>())
            .ThrowsAsync(new Exception("simulated Stripe outage"));

    /// <summary>
    /// Proves the reservation reached the Stripe read, so the failure came from the substituted outage and not
    /// from an earlier step.
    /// </summary>
    private static async Task AssertSubscriptionLookupAttemptedAsync(SsoTestData testData)
    {
        var stripeAdapter = testData.Factory.Services.GetRequiredService<IStripeAdapter>();
        await stripeAdapter.Received(1).GetSubscriptionAsync(_subscriptionId, Arg.Any<SubscriptionGetOptions>());
    }

    private static void WithPrivilegedControlsSeats(Organization organization,
        int pamSeats, int? maxAutoscalePamSeats)
    {
        organization.PlanType = PlanType.EnterpriseAnnually;
        organization.UsePam = true;
        organization.PamSeats = pamSeats;
        organization.MaxAutoscalePamSeats = maxAutoscalePamSeats;
    }

    private static HttpClient CreateNonRedirectingClient(SsoTestData testData) =>
        testData.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>
    /// Seeds a Confirmed Owner, who gets the organization's seat-needed emails.
    /// </summary>
    /// <returns>The Owner's email address.</returns>
    private static async Task<string> SeedOwnerAsync(SsoTestData testData)
    {
        var owner = await SeedConfirmedMemberAsync(testData, OrganizationUserType.Owner, accessPam: false);
        return owner.Email;
    }

    /// <summary>
    /// Seeds a second organization that has verified <paramref name="domainName"/> and turned on the policy that
    /// blocks account creation for its claimed domains. Registering a new user with an email in that domain fails.
    /// </summary>
    private static async Task SeedOtherOrganizationBlockingDomainAsync(SsoTestData testData, string domainName)
    {
        var organizationRepository = testData.Factory.Services.GetRequiredService<IOrganizationRepository>();
        var otherOrganization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Domain Claiming Organization",
            BillingEmail = "billing@claiming.test",
            Plan = "Enterprise",
            Enabled = true,
            UsePolicies = true,
            UseOrganizationDomains = true
        });

        var domain = new OrganizationDomain
        {
            OrganizationId = otherOrganization.Id,
            DomainName = domainName,
            Txt = "bw-test"
        };
        domain.SetVerifiedDate();
        await testData.Factory.Services.GetRequiredService<IOrganizationDomainRepository>().CreateAsync(domain);

        await testData.Factory.Services.GetRequiredService<IPolicyRepository>().CreateAsync(new Policy
        {
            OrganizationId = otherOrganization.Id,
            Type = PolicyType.BlockClaimedDomainAccountCreation,
            Enabled = true
        });
    }

    /// <summary>
    /// Seeds a Confirmed member with Privileged Controls access, who takes one Privileged Controls seat.
    /// </summary>
    private static Task SeedPrivilegedControlsSeatHolderAsync(SsoTestData testData) =>
        SeedConfirmedMemberAsync(testData, OrganizationUserType.User, accessPam: true);

    private static async Task<User> SeedConfirmedMemberAsync(SsoTestData testData, OrganizationUserType type,
        bool accessPam)
    {
        var userRepository = testData.Factory.Services.GetRequiredService<IUserRepository>();
        var user = await userRepository.CreateAsync(new User
        {
            Email = $"member_{Guid.NewGuid()}@test.dev",
            Name = "Member",
            ApiKey = Guid.NewGuid().ToString(),
            SecurityStamp = Guid.NewGuid().ToString()
        });

        var organizationUserRepository = testData.Factory.Services.GetRequiredService<IOrganizationUserRepository>();
        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = testData.Organization!.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = type,
            AccessPam = accessPam
        });

        return user;
    }

    private static async Task<OrganizationUser> GetOrgUserAsync(SsoTestData testData)
    {
        var organizationUserRepository = testData.Factory.Services.GetRequiredService<IOrganizationUserRepository>();
        var orgUser = await organizationUserRepository.GetByIdAsync(testData.OrganizationUser!.Id);
        Assert.NotNull(orgUser);
        return orgUser;
    }

    private static async Task<Organization> GetOrganizationAsync(SsoTestData testData)
    {
        var organizationRepository = testData.Factory.Services.GetRequiredService<IOrganizationRepository>();
        var organization = await organizationRepository.GetByIdAsync(testData.Organization!.Id);
        Assert.NotNull(organization);
        return organization;
    }

    private static async Task AssertSeatNeededEmailSentAsync(SsoTestData testData, string ownerEmail,
        string memberEmail)
    {
        var mailer = testData.Factory.Services.GetRequiredService<IMailer>();
        await mailer.Received(1).SendEmail(Arg.Is<PrivilegedControlsSeatNeededMail>(mail =>
            mail.ToEmails.Contains(ownerEmail) &&
            mail.View.OrganizationId == testData.Organization!.Id &&
            mail.View.MemberEmail == CoreHelpers.PreventEmailAutoLinking(memberEmail)));
    }

    private static async Task AssertSeatNeededEmailNotSentAsync(SsoTestData testData)
    {
        var mailer = testData.Factory.Services.GetRequiredService<IMailer>();
        await mailer.DidNotReceive().SendEmail(Arg.Any<PrivilegedControlsSeatNeededMail>());
    }

    /// <summary>
    /// Asserts the Owners got neither Privileged Controls seat email: not the seat-needed email and not the
    /// limit-reached email.
    /// </summary>
    private static async Task AssertNoPrivilegedControlsEmailSentAsync(SsoTestData testData)
    {
        var mailer = testData.Factory.Services.GetRequiredService<IMailer>();
        await mailer.DidNotReceive().SendEmail(Arg.Any<PrivilegedControlsSeatNeededMail>());
        await mailer.DidNotReceive().SendEmail(Arg.Any<PrivilegedControlsSeatLimitReachedMail>());
    }

    private static async Task AssertSeatLimitReachedEmailSentAsync(SsoTestData testData, string ownerEmail,
        int maxSeatCount)
    {
        var mailer = testData.Factory.Services.GetRequiredService<IMailer>();
        await mailer.Received(1).SendEmail(Arg.Is<PrivilegedControlsSeatLimitReachedMail>(mail =>
            mail.ToEmails.Contains(ownerEmail) &&
            mail.View.OrganizationId == testData.Organization!.Id &&
            mail.View.MaxSeatCount == maxSeatCount));
    }

    /// <summary>
    /// Asserts the response is the standard SSO success redirect, not a redirect back to the web client's
    /// <c>/login</c> or <c>/sso-login-failed</c> page.
    /// </summary>
    private static void AssertSsoSucceeded(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var location = response.Headers.Location.ToString();
        Assert.DoesNotContain("/login?", location);
        Assert.DoesNotContain("/sso-login-failed?", location);
    }

    private static void AssertStagedInviteAcceptanceRequiredRedirect(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var location = response.Headers.Location.ToString();
        Assert.Contains("/login?", location);
        Assert.Contains("error=ssoStagedOrgUserInviteAcceptanceRequired", location);
    }
}
