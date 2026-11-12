using System.Net;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Sso.IntegrationTest.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Sso.IntegrationTest.ExternalCallback;

/// <summary>
/// <c>/Account/ExternalCallback</c> finds an existing Bitwarden user with no SSO link by the email claim. It accepts
/// the external login only when the organization user is Accepted or Confirmed. With no organization user or an Invited
/// one, the user goes back to the web vault login page with an error code. A Key Connector user who is not yet a
/// member, a Revoked organization user, and an unknown status each get an error.
/// </summary>
public class ExternalCallbackEmailMatchedUserTests
{
    /*
    * Test to verify /Account/ExternalCallback returns error when an existing user
    * uses Key Connector but has no org user record (was removed from organization).
    */
    [Fact]
    public async Task ExternalCallback_WithExistingKeyConnectorUser_AndNoOrgUser_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser(user =>
            {
                user.UsesKeyConnector = true;
            })
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because user uses Key Connector but has no org user record
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("You were removed from the organization managing single sign-on for your account", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /*
    * Test to verify /Account/ExternalCallback returns error when an existing user
    * uses Key Connector and has an org user record in the invited status.
    */
    [Fact]
    public async Task ExternalCallback_WithExistingKeyConnectorUser_AndInvitedOrgUser_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig(ssoConfig => { })
            .WithUser(user =>
            {
                user.UsesKeyConnector = true;
            })
            .WithOrganizationUser(orgUser =>
            {
                orgUser.Status = OrganizationUserStatusType.Invited;
            })
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because user uses Key Connector but the Org user is in the invited status
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("You were removed from the organization managing single sign-on for your account", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /*
    * Test to verify /Account/ExternalCallback returns error when an existing user
    * uses Key Connector and encounters a Staged OrganizationUser row (created by
    * directory import) in the SSO-target org. Key Connector users have no master
    * password and cannot complete the "sign in with master password and accept the
    * invite" flow that the Staged-promotion path would email them into. The Key
    * Connector guard must reject them cleanly here — without this branch the flow
    * would fall through to PromoteStagedOrgUserAndSendInviteAsync, consume a seat,
    * mail an unactionable invite, and dead-end on UserAlreadyExistsKeyConnector on
    * the next SSO attempt.
    */
    [Fact]
    public async Task ExternalCallback_WithExistingKeyConnectorUser_AndStagedOrgUser_ReturnsError()
    {
        // Arrange — existing KC user (UsesKeyConnector=true) AND a Staged
        // OrganizationUser row (UserId=null) matching the SSO-claimed email.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser(user =>
            {
                user.UsesKeyConnector = true;
            })
            .WithStagedOrganizationUser()
            .WithMockedSendOrganizationInvitesCommand()
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert — Key Connector guard rejects the login before the Staged branch runs.
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("You were removed from the organization managing single sign-on for your account", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        // Assert — no invite email was issued (guard must fire before the promotion branch).
        var inviteCommand = testData.Factory.Services.GetRequiredService<ISendOrganizationInvitesCommand>();
        await inviteCommand.DidNotReceive().SendInvitesAsync(Arg.Any<SendInvitesRequest>());

        // Assert — Staged row was not mutated (still Staged, UserId still null).
        var orgUserRepo = testData.Factory.Services.GetRequiredService<IOrganizationUserRepository>();
        var refreshedOrgUser = await orgUserRepo.GetByIdAsync(testData.OrganizationUser!.Id);
        Assert.NotNull(refreshedOrgUser);
        Assert.Equal(OrganizationUserStatusType.Staged, refreshedOrgUser.Status);
        Assert.Null(refreshedOrgUser.UserId);
    }

    /*
    * Test to verify /Account/ExternalCallback redirects an existing user (not using
    * Key Connector) with no OrganizationUser row back to the web vault's /login with
    * the OrgMembershipRequired errorCode + context. Covers both the user who clicked
    * an open invite link (client has the invite stashed) and the user with no pending
    * invite at all — the server cannot tell them apart, so this single redirect
    * contract serves both.
    */
    [Fact]
    public async Task ExternalCallback_WithExistingUser_AndNoOrgUser_RedirectsToWebVaultLogin()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false // Capture the redirect rather than following to the (non-existent) web vault.
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert — 302 to the web vault /login with the expected query-string context.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var location = response.Headers.Location!.ToString();
        Assert.Contains("/login?", location);
        Assert.Contains("error=ssoOrgMembershipRequired", location);

        // The redirect carries the seeded user's email, the org id (stable match key), and
        // the org display name (URL-encoded, for the toast) so the web client can pre-fill
        // /login and dispatch its match/no-match split.
        Assert.Contains($"email={Uri.EscapeDataString(testData.User!.Email)}", location);
        Assert.Contains($"organizationId={testData.Organization!.Id}", location);
        Assert.Contains(
            $"organizationName={Uri.EscapeDataString(testData.Organization!.DisplayName())}",
            location);
    }

    /*
    * Test to verify /Account/ExternalCallback redirects an existing user with an
    * Invited org user back to the web vault's /login with context so the client can
    * surface an actionable toast. Previously this rendered a server error page;
    * the security gate (refusing SSO completion for invited users) is unchanged.
    */
    [Fact]
    public async Task ExternalCallback_WithExistingUser_AndInvitedOrgUserStatus_RedirectsToWebVaultLogin()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithOrganizationUser(orgUser =>
            {
                orgUser.Status = OrganizationUserStatusType.Invited;
            })
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false // Capture the redirect rather than following to the (non-existent) web vault.
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert — 302 to the web vault /login with the expected query-string context.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var location = response.Headers.Location!.ToString();
        Assert.Contains("/login?", location);
        Assert.Contains("error=ssoOrgInviteAcceptanceRequired", location);

        // The redirect carries the seeded user's email, the org id (stable match key), and
        // the org display name (URL-encoded, for the toast) so the web client can pre-fill
        // /login and render a contextual toast.
        Assert.Contains($"email={Uri.EscapeDataString(testData.User!.Email)}", location);
        Assert.Contains($"organizationId={testData.Organization!.Id}", location);
        Assert.Contains(
            $"organizationName={Uri.EscapeDataString(testData.Organization!.DisplayName())}",
            location);
    }

    /*
    * Test to verify /Account/ExternalCallback returns error when org user has an unknown/invalid status.
    * This tests defensive code that handles future enum values or data corruption scenarios.
    * We simulate this by casting an invalid integer to OrganizationUserStatusType.
    */
    [Fact]
    public async Task ExternalCallback_WithUnknownOrgUserStatus_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithOrganizationUser(orgUser =>
            {
                orgUser.Status = (OrganizationUserStatusType)99; // Invalid enum value - simulates future status or data corruption
            })
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because org user status is unknown/invalid
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains("is in an unknown state", stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /*
    * Test to verify /Account/ExternalCallback returns error for revoked org user.
    */
    [Fact]
    public async Task ExternalCallback_WithRevokedOrgUser_ReturnsError()
    {
        // Arrange
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithOrganizationUser(orgUser =>
            {
                orgUser.Status = OrganizationUserStatusType.Revoked;
            })
            .BuildAsync();

        var client = testData.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should fail because user state is invalid
        var stringResponse = await response.Content.ReadAsStringAsync();
        Assert.Contains(
            $"Your access to organization {testData.Organization?.DisplayName()} has been revoked. Please contact your administrator for assistance.",
            stringResponse);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /*
    * SUCCESS PATH: Test to verify /Account/ExternalCallback succeeds when an existing user
    * with a valid (Confirmed) organization user status logs in via SSO for the first time.
    */
    [Fact]
    public async Task ExternalCallback_WithExistingUserAndConfirmedOrgUser_ReturnsSuccess()
    {
        // Arrange - Existing user with confirmed org user status, no SSO link yet
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithOrganizationUser(orgUser =>
            {
                orgUser.Status = OrganizationUserStatusType.Confirmed;
            })
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false // Prevent auto-redirects to capture initial response
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should succeed and redirect
        Assert.True(
            response.StatusCode == HttpStatusCode.Redirect,
            $"Expected success/redirect but got {response.StatusCode}");

        Assert.NotNull(response.Headers.Location);
    }

    /*
    * SUCCESS PATH: Test to verify /Account/ExternalCallback succeeds when an existing user
    * with Accepted organization user status logs in via SSO.
    */
    [Fact]
    public async Task ExternalCallback_WithExistingUserAndAcceptedOrgUser_ReturnsSuccess()
    {
        // Arrange - Existing user with accepted org user status
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithUser()
            .WithOrganizationUser(orgUser =>
            {
                orgUser.Status = OrganizationUserStatusType.Accepted;
            })
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false // Prevent auto-redirects to capture initial response
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert - Should succeed and redirect
        Assert.True(
            response.StatusCode == HttpStatusCode.Redirect,
            $"Expected success/redirect but got {response.StatusCode}");

        Assert.NotNull(response.Headers.Location);
    }
}
