using Bit.Api.Controllers.SelfHosted;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Controllers.SelfHosted;

[ControllerCustomize(typeof(SelfHostedOrganizationSponsorshipsController))]
[SutProviderCustomize]
public class SelfHostedOrganizationSponsorshipsControllerTests
{
    /// <summary>
    /// A member-initiated sponsorship is hidden from GET {orgId}/sponsored, so the admin revoke
    /// route must not act on it either.
    /// </summary>
    [Theory]
    [BitAutoData]
    public async Task AdminInitiatedRevokeSponsorshipAsync_MemberInitiatedSponsorship_ThrowsBadRequest(
        Guid sponsoringOrgId,
        OrganizationSponsorship sponsorship,
        SutProvider<SelfHostedOrganizationSponsorshipsController> sutProvider)
    {
        // Arrange
        sponsorship.IsAdminInitiated = false;
        sponsorship.FriendlyName = "personal-family@example.com";
        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetManyBySponsoringOrganizationAsync(sponsoringOrgId)
            .Returns(new List<OrganizationSponsorship> { sponsorship });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdminInitiatedRevokeSponsorshipAsync(sponsoringOrgId, sponsorship.FriendlyName));

        Assert.Contains("could not be found under the given sponsoring organization", exception.Message);
        await sutProvider.GetDependency<IRevokeSponsorshipCommand>()
            .DidNotReceiveWithAnyArgs()
            .RevokeSponsorshipAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task AdminInitiatedRevokeSponsorshipAsync_AdminInitiatedSponsorship_Revokes(
        Guid sponsoringOrgId,
        OrganizationSponsorship sponsorship,
        SutProvider<SelfHostedOrganizationSponsorshipsController> sutProvider)
    {
        // Arrange
        sponsorship.IsAdminInitiated = true;
        sponsorship.FriendlyName = "employee@example.com";
        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetManyBySponsoringOrganizationAsync(sponsoringOrgId)
            .Returns(new List<OrganizationSponsorship> { sponsorship });

        // Act
        await sutProvider.Sut.AdminInitiatedRevokeSponsorshipAsync(sponsoringOrgId, sponsorship.FriendlyName);

        // Assert
        await sutProvider.GetDependency<IRevokeSponsorshipCommand>().Received(1)
            .RevokeSponsorshipAsync(sponsorship);
    }
}
