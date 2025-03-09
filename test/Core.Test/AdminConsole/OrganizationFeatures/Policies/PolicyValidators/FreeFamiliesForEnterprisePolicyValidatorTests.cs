using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

[SutProviderCustomize]
public class FreeFamiliesForEnterprisePolicyValidatorTests
{
    [Theory, BitAutoData]
    public async Task OnSaveSideEffectsAsync_DoesNotNotifyUserWhenPolicyDisabled(
        Organization organization,
        List<OrganizationSponsorship> organizationSponsorships,
        [PolicyUpdate(PolicyType.FreeFamiliesSponsorshipPolicy)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.FreeFamiliesSponsorshipPolicy, true)] Policy policy,
        SutProvider<FreeFamiliesForEnterprisePolicyValidator> sutProvider)
    {

        policy.Enabled = true;
        policyUpdate.Enabled = false;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(policyUpdate.OrganizationId)
            .Returns(organization);

        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetManyBySponsoringOrganizationAsync(policyUpdate.OrganizationId)
            .Returns(organizationSponsorships);

        await sutProvider.Sut.OnSaveSideEffectsAsync(policyUpdate, policy);

        await sutProvider.GetDependency<IMailService>().DidNotReceive()
            .SendFamiliesForEnterpriseRemoveSponsorshipsEmailAsync(organizationSponsorships[0].FriendlyName, organizationSponsorships[0].ValidUntil.ToString(),
                organizationSponsorships[0].SponsoredOrganizationId.ToString(), organization.DisplayName());
    }

    [Theory, BitAutoData]
    public async Task OnSaveSideEffectsAsync_DoesNotifyUserWhenPolicyDisabled(
        Organization organization,
        List<OrganizationSponsorship> organizationSponsorships,
        [PolicyUpdate(PolicyType.FreeFamiliesSponsorshipPolicy)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.FreeFamiliesSponsorshipPolicy, true)] Policy policy,
        SutProvider<FreeFamiliesForEnterprisePolicyValidator> sutProvider)
    {

        policy.Enabled = false;
        policyUpdate.Enabled = true;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(policyUpdate.OrganizationId)
            .Returns(organization);

        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetManyBySponsoringOrganizationAsync(policyUpdate.OrganizationId)
            .Returns(organizationSponsorships);
        // Act
        await sutProvider.Sut.OnSaveSideEffectsAsync(policyUpdate, policy);

        // Assert
        var offerAcceptanceDate = organizationSponsorships[0].ValidUntil!.Value.AddDays(-7).ToString("MM/dd/yyyy");
        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendFamiliesForEnterpriseRemoveSponsorshipsEmailAsync(organizationSponsorships[0].FriendlyName, offerAcceptanceDate,
                organizationSponsorships[0].SponsoredOrganizationId.ToString(), organization.Name);

    }
}
