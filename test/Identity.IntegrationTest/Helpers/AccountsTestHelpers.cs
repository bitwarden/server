using System.Text.Json.Nodes;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.IntegrationTestCommon.Factories;
using Microsoft.AspNetCore.DataProtection;

namespace Bit.Identity.IntegrationTest.Helpers;

/// <summary>
/// Arrange helpers for tests of the accounts registration and prelogin endpoints.
/// </summary>
public static class AccountsTestHelpers
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

    public static async Task<User> CreateUserAsync(string email, string name, IdentityApplicationFactory factory, string masterPasswordSalt = null)
    {
        var userRepository = factory.Services.GetRequiredService<IUserRepository>();

        var user = new User
        {
            Email = email,
            Id = Guid.NewGuid(),
            Name = name,
            SecurityStamp = Guid.NewGuid().ToString(),
            ApiKey = "test_api_key",
            MasterPasswordSalt = masterPasswordSalt,
        };

        await userRepository.CreateAsync(user);

        return user;
    }
}
