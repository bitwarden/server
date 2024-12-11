using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SelfHosted;
using Bit.Core.Test.AutoFixture.OrganizationSponsorshipFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.SelfHosted;

[SutProviderCustomize]
[OrganizationSponsorshipCustomize]
public class SelfHostedRevokeSponsorshipCommandTests : CancelSponsorshipCommandTestsBase
{
    [Theory]
    [BitAutoData]
    public async Task RevokeSponsorship_NoExistingSponsorship_ThrowsBadRequest(
        SutProvider<SelfHostedRevokeSponsorshipCommand> sutProvider
    )
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RevokeSponsorshipAsync(null)
        );

        Assert.Contains("You are not currently sponsoring an organization.", exception.Message);
        await AssertDidNotDeleteSponsorshipAsync(sutProvider);
        await AssertDidNotUpdateSponsorshipAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task RevokeSponsorship_SponsorshipNotSynced_DeletesSponsorship(
        OrganizationSponsorship sponsorship,
        SutProvider<SelfHostedRevokeSponsorshipCommand> sutProvider
    )
    {
        sponsorship.LastSyncDate = null;

        await sutProvider.Sut.RevokeSponsorshipAsync(sponsorship);
        await AssertDeletedSponsorshipAsync(sponsorship, sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task RevokeSponsorship_SponsorshipSynced_MarksForDeletion(
        OrganizationSponsorship sponsorship,
        SutProvider<SelfHostedRevokeSponsorshipCommand> sutProvider
    )
    {
        sponsorship.LastSyncDate = DateTime.UtcNow;

        await sutProvider.Sut.RevokeSponsorshipAsync(sponsorship);

        Assert.True(sponsorship.ToDelete);
        await AssertUpdatedSponsorshipAsync(sponsorship, sutProvider);
        await AssertDidNotDeleteSponsorshipAsync(sutProvider);
    }
}
