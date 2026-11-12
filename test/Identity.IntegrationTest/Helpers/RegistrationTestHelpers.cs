using System.Text.Json.Nodes;
using Bit.IntegrationTestCommon.Factories;
using Microsoft.AspNetCore.DataProtection;

namespace Bit.Identity.IntegrationTest.Helpers;

/// <summary>
/// Arrange helpers for the registration tests: organizations with invite links, and registration tokens.
/// </summary>
public static class RegistrationTestHelpers
{
    public static async Task<(Organization Org, OrganizationInviteLink InviteLink)> SeedOrgWithClaimedDomainAndInviteLinkAsync(
        IdentityApplicationFactory factory, string claimedDomain, IEnumerable<string>? allowedDomains = null)
    {
        var organizationRepository = factory.Services.GetRequiredService<IOrganizationRepository>();
        var organizationDomainRepository = factory.Services.GetRequiredService<IOrganizationDomainRepository>();
        var policyRepository = factory.Services.GetRequiredService<IPolicyRepository>();
        var organizationInviteLinkRepository = factory.Services.GetRequiredService<IOrganizationInviteLinkRepository>();

        var organization = new Organization
        {
            Name = $"ClaimedDomainOrg-{Guid.NewGuid():N}",
            BillingEmail = $"billing+{Guid.NewGuid():N}@example.com",
            Plan = "Enterprise",
            Enabled = true,
            UsePolicies = true,
            UseOrganizationDomains = true,
            UseInviteLinks = true,
        };
        organization = await organizationRepository.CreateAsync(organization);

        var domain = new OrganizationDomain
        {
            OrganizationId = organization.Id,
            DomainName = claimedDomain,
            Txt = "bw-test",
        };
        domain.SetVerifiedDate();
        await organizationDomainRepository.CreateAsync(domain);

        var policy = new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.BlockClaimedDomainAccountCreation,
            Enabled = true,
        };
        await policyRepository.CreateAsync(policy);

        var inviteLink = new OrganizationInviteLink
        {
            OrganizationId = organization.Id,
            Invite = "opaque-invite-blob",
            SupportsConfirmation = false,
        };
        inviteLink.SetAllowedDomains(allowedDomains ?? new[] { claimedDomain });
        inviteLink.SetNewId();
        inviteLink.SetNewCode();
        await organizationInviteLinkRepository.CreateAsync(inviteLink);

        return (organization, inviteLink);
    }

    public static async Task<(Organization Org, OrganizationInviteLink InviteLink)> SeedOrgWithInviteLinkAsync(
        IdentityApplicationFactory factory, IEnumerable<string>? allowedDomains = null)
    {
        var organizationRepository = factory.Services.GetRequiredService<IOrganizationRepository>();
        var organizationInviteLinkRepository = factory.Services.GetRequiredService<IOrganizationInviteLinkRepository>();

        var organization = new Organization
        {
            Name = $"OtherOrg-{Guid.NewGuid():N}",
            BillingEmail = $"billing+{Guid.NewGuid():N}@example.com",
            Plan = "Enterprise",
            Enabled = true,
            UsePolicies = true,
            UseInviteLinks = true,
        };
        organization = await organizationRepository.CreateAsync(organization);

        var inviteLink = new OrganizationInviteLink
        {
            OrganizationId = organization.Id,
            Invite = "opaque-invite-blob",
            SupportsConfirmation = false,
        };
        inviteLink.SetAllowedDomains(allowedDomains ?? Array.Empty<string>());
        inviteLink.SetNewId();
        inviteLink.SetNewCode();
        await organizationInviteLinkRepository.CreateAsync(inviteLink);

        return (organization, inviteLink);
    }

    // TODO: PM-45479 - Delete this helper with the TokenWithoutEmailSent tests.
    /// <summary>
    /// Mints a registration token whose payload has no EmailSent field, the shape of tokens issued before
    /// that property existed. Protected with the same purpose and prefix the real token factory uses.
    /// </summary>
    public static string ProtectRegistrationTokenWithoutEmailSent(IdentityApplicationFactory factory, string email, string name)
    {
        var payload = JsonNode.Parse(new RegistrationEmailVerificationTokenable(email, name).ToToken().ToString())!.AsObject();
        payload.Remove(nameof(RegistrationEmailVerificationTokenable.EmailSent));

        var protector = factory.GetService<IDataProtectionProvider>()
            .CreateProtector(RegistrationEmailVerificationTokenable.DataProtectorPurpose);
        return RegistrationEmailVerificationTokenable.ClearTextPrefix + protector.Protect(payload.ToJsonString());
    }
}
