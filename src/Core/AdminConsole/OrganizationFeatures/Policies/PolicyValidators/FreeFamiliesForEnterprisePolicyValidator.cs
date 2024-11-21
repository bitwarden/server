#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public class FreeFamiliesForEnterprisePolicyValidator(
    IOrganizationSponsorshipRepository organizationSponsorshipRepository,
    IMailService mailService,
    IOrganizationRepository organizationRepository)
    : IPolicyValidator
{
    public PolicyType Type => PolicyType.FreeFamiliesSponsorshipPolicy;
    public IEnumerable<PolicyType> RequiredPolicies => [];

    public async Task OnSaveSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        if (currentPolicy is not { Enabled: true } && policyUpdate is { Enabled: true })
        {
            await NotifiesUserWithApplicablePoliciesAsync(policyUpdate);
        }
    }

    private async Task NotifiesUserWithApplicablePoliciesAsync(PolicyUpdate policy)
    {
        var organizationSponsorships = (await organizationSponsorshipRepository.GetManyBySponsoringOrganizationAsync(policy.OrganizationId))
            .Where(p => p.SponsoredOrganizationId is not null)
            .ToList();

        var organization = await organizationRepository.GetByIdAsync(policy.OrganizationId);
        var organizationName = organization?.Name;

        foreach (var org in organizationSponsorships)
        {
            var offerAcceptanceDate = org.ValidUntil!.Value.AddDays(-7).ToString("MM/dd/yyyy");
            await mailService.SendFamiliesForEnterpriseRemoveSponsorshipsEmailAsync(org.FriendlyName, offerAcceptanceDate,
                org.SponsoredOrganizationId.ToString(), organizationName);
        }
    }

    public Task<string> ValidateAsync(PolicyUpdate policyUpdate, Policy? currentPolicy) => Task.FromResult("");
}
