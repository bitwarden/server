using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using NSubstitute;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise;

public abstract class CancelSponsorshipCommandTestsBase : FamiliesForEnterpriseTestsBase
{
    protected async Task AssertRemovedSponsoredPaymentAsync<T>(Organization sponsoredOrg,
OrganizationSponsorship sponsorship, SutProvider<T> sutProvider)
    {
        await sutProvider.GetDependency<IPaymentService>().Received(1)
            .RemoveOrganizationSponsorshipAsync(sponsoredOrg, sponsorship);
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).UpsertAsync(sponsoredOrg);
        if (sponsorship != null)
        {
            await sutProvider.GetDependency<IMailService>().Received(1)
                .SendFamiliesForEnterpriseSponsorshipRevertingEmailAsync(sponsoredOrg.BillingEmailAddress(), sponsorship.ValidUntil.GetValueOrDefault());
        }
    }

    protected async Task AssertDeletedSponsorshipAsync<T>(OrganizationSponsorship sponsorship,
        SutProvider<T> sutProvider)
    {
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1)
            .DeleteAsync(sponsorship);
    }

    protected static async Task AssertDidNotRemoveSponsorshipAsync<T>(SutProvider<T> sutProvider)
    {
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
            .DeleteAsync(default);
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
    }

    protected async Task AssertRemovedSponsorshipAsync<T>(OrganizationSponsorship sponsorship,
        SutProvider<T> sutProvider)
    {
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1)
            .DeleteAsync(sponsorship);
    }

    protected static async Task AssertDidNotRemoveSponsoredPaymentAsync<T>(SutProvider<T> sutProvider)
    {
        await sutProvider.GetDependency<IPaymentService>().DidNotReceiveWithAnyArgs()
            .RemoveOrganizationSponsorshipAsync(default, default);
        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
        await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs()
            .SendFamiliesForEnterpriseSponsorshipRevertingEmailAsync(default, default);
    }

    protected static async Task AssertDidNotDeleteSponsorshipAsync<T>(SutProvider<T> sutProvider)
    {
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
            .DeleteAsync(default);
    }

    protected static async Task AssertDidNotUpdateSponsorshipAsync<T>(SutProvider<T> sutProvider)
    {
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
    }

    protected static async Task AssertUpdatedSponsorshipAsync<T>(OrganizationSponsorship sponsorship,
        SutProvider<T> sutProvider)
    {
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1).UpsertAsync(sponsorship);
    }
}
