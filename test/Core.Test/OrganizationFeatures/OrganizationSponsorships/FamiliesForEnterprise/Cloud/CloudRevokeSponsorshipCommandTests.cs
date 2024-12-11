using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;
using Bit.Core.Test.AutoFixture.OrganizationSponsorshipFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;

[SutProviderCustomize]
[OrganizationSponsorshipCustomize]
public class CloudRevokeSponsorshipCommandTests : CancelSponsorshipCommandTestsBase
{
    [Theory]
    [BitAutoData]
    public async Task RevokeSponsorship_NoExistingSponsorship_ThrowsBadRequest(
        SutProvider<CloudRevokeSponsorshipCommand> sutProvider
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
    public async Task RevokeSponsorship_SponsorshipNotRedeemed_DeletesSponsorship(
        OrganizationSponsorship sponsorship,
        SutProvider<CloudRevokeSponsorshipCommand> sutProvider
    )
    {
        sponsorship.SponsoredOrganizationId = null;

        await sutProvider.Sut.RevokeSponsorshipAsync(sponsorship);
        await AssertDeletedSponsorshipAsync(sponsorship, sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task RevokeSponsorship_SponsorshipRedeemed_MarksForDelete(
        OrganizationSponsorship sponsorship,
        SutProvider<CloudRevokeSponsorshipCommand> sutProvider
    )
    {
        await sutProvider.Sut.RevokeSponsorshipAsync(sponsorship);

        Assert.True(sponsorship.ToDelete);
        await AssertUpdatedSponsorshipAsync(sponsorship, sutProvider);
        await AssertDidNotDeleteSponsorshipAsync(sutProvider);
    }
}
