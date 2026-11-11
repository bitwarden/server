using System.Net;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Services;
using Bit.Sso.IntegrationTest.Utilities;
using Microsoft.AspNetCore.Mvc.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Sso.IntegrationTest.ExternalCallback;

/// <summary>
/// <c>/Account/ExternalCallback</c> seat check before just-in-time provisioning: free seats, a seat cap on
/// self-hosted, and cloud autoscale that succeeds, fails, or fails after it changed the seat count.
/// </summary>
public class AccountControllerExternalCallbackSeatAvailabilityTests
{
    /*
    * Test to verify /Account/ExternalCallback redirects directly to the client's
    * /sso-login-failed terminal page when the target organization is at its seat cap
    * and cannot autoscale because it's a self-hosted instance. Replaces the pre-fix
    * behavior of surfacing a raw 500 — the user cannot resolve without admin
    * intervention, so we route to the terminal page (no /login layover) instead.
    */
    [Fact]
    public async Task ExternalCallback_WithNoAvailableSeats_OnSelfHosted_RedirectsToSsoLoginFailed()
    {
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithOrganization(org =>
            {
                org.Seats = 5; // Organization has seat limit
            })
            .AsSelfHosted()
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        ExternalCallbackAssertions.AssertNoSeatsAvailableRedirect(response);
    }

    /*
    * Test to verify /Account/ExternalCallback redirects directly to the client's
    * /sso-login-failed terminal page when the organization is at its seat cap and
    * cloud autoscale cannot grow past MaxAutoscaleSeats. Same contract as the
    * self-hosted variant.
    */
    [Fact]
    public async Task ExternalCallback_WithNoAvailableSeats_AndAutoAddSeatsFails_RedirectsToSsoLoginFailed()
    {
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
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

        ExternalCallbackAssertions.AssertNoSeatsAvailableRedirect(response);
    }

    /*
    * SUCCESS PATH: Test to verify /Account/ExternalCallback JIT-provisions a brand-new
    * user when the org has an explicit seat cap with headroom. Locks in the "guard fires,
    * seats available" branch of the pre-user-creation seat check — currently exercised
    * only implicitly by the unlimited-seats JIT test.
    */
    [Fact]
    public async Task ExternalCallback_WithSeatCapAndHeadroom_JitSucceeds()
    {
        // Arrange — seat cap of 1, 0 occupied (builder skips auto-fill when Seats == 1),
        // so the seat check should observe 1 available seat and proceed without autoscale.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithOrganization(org =>
            {
                org.Seats = 1;
            })
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        // Assert — JIT proceeded and produced the standard SSO-success redirect.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    /*
    * SUCCESS PATH: Test to verify /Account/ExternalCallback JIT-provisions successfully
    * when the org is at its seat cap but cloud autoscale succeeds. Locks in the
    * "at cap, cloud, autoscale succeeds" branch of the pre-user-creation seat check.
    * IOrganizationService.AutoAddSeatsAsync is substituted to bypass the real billing
    * gateway (which requires a Stripe-linked org).
    */
    [Fact]
    public async Task ExternalCallback_WithSeatCapAtLimitAndAutoscaleSucceeds_JitSucceeds()
    {
        // Arrange — Seats = 5 causes the builder to auto-fill 5 Confirmed OrgUsers,
        // putting the org at cap. Mocked AutoAddSeatsAsync succeeds silently.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
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

        // Assert — SSO callback succeeded and autoscale was actually invoked (proves we
        // went through the "at cap → autoscale → success" branch, not the "seats available"
        // shortcut).
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var orgService = testData.Factory.Services.GetRequiredService<IOrganizationService>();
        await orgService.Received(1).AutoAddSeatsAsync(Arg.Any<Organization>(), 1);
    }

    /*
    * FAILURE PATH: Test to verify /Account/ExternalCallback rolls back a mid-flight
    * seat-count mutation when AutoAddSeatsAsync partially succeeds (bumps Seats) and
    * then throws. The catch-path must call AdjustSeatsAsync with the negated delta
    * before surfacing the NoSeatsAvailable redirect.
    */
    [Fact]
    public async Task ExternalCallback_WithAutoscalePartialFailure_RollsBackSeatsAndRedirects()
    {
        // Arrange — Seats = 5, at cap. Mocked AutoAddSeatsAsync bumps Seats to 6 then throws.
        // The catch block should observe org.Seats (6) != initialSeatCount (5) and call
        // AdjustSeatsAsync(orgId, -1) to roll back before throwing SsoAuthnNoSeatsAvailableException.
        var testData = await new SsoTestDataBuilder()
            .WithSsoConfig()
            .WithOrganization(org =>
            {
                org.Seats = 5;
            })
            .WithAutoscalePartialFailure()
            .BuildAsync();

        var client = testData.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/Account/ExternalCallback");

        ExternalCallbackAssertions.AssertNoSeatsAvailableRedirect(response);

        // Assert — rollback ran with the exact delta before the redirect fired.
        var orgService = testData.Factory.Services.GetRequiredService<IOrganizationService>();
        await orgService.Received(1).AdjustSeatsAsync(testData.Organization!.Id, -1);
    }
}
