using Bit.Api.Billing.Controllers;
using Bit.Api.Models.Request.Organizations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.Billing.Mocks;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Api.Test.Billing.Controllers;

[ControllerCustomize(typeof(OrganizationSponsorshipsController))]
[SutProviderCustomize]
public class OrganizationSponsorshipsControllerTests
{
    public static IEnumerable<object[]> EnterprisePlanTypes =>
        Enum.GetValues<PlanType>().Where(p => MockPlans.Get(p).ProductTier == ProductTierType.Enterprise).Select(p => new object[] { p });
    public static IEnumerable<object[]> NonEnterprisePlanTypes =>
        Enum.GetValues<PlanType>().Where(p => MockPlans.Get(p).ProductTier != ProductTierType.Enterprise).Select(p => new object[] { p });
    public static IEnumerable<object[]> NonFamiliesPlanTypes =>
        Enum.GetValues<PlanType>().Where(p => MockPlans.Get(p).ProductTier != ProductTierType.Families).Select(p => new object[] { p });

    public static IEnumerable<object[]> NonConfirmedOrganizationUsersStatuses =>
        Enum.GetValues<OrganizationUserStatusType>()
            .Where(s => s != OrganizationUserStatusType.Confirmed)
            .Select(s => new object[] { s });


    [Theory]
    [BitAutoData]
    public async Task RedeemSponsorship_BadToken_ThrowsBadRequest(string sponsorshipToken, User user,
        OrganizationSponsorshipRedeemRequestModel model, SutProvider<OrganizationSponsorshipsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user.Id)
            .Returns(user);
        sutProvider.GetDependency<IValidateRedemptionTokenCommand>().ValidateRedemptionTokenAsync(sponsorshipToken,
            user.Email).Returns((false, null));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RedeemSponsorship(sponsorshipToken, model));

        Assert.Contains("Failed to parse sponsorship token.", exception.Message);
        await sutProvider.GetDependency<ISetUpSponsorshipCommand>()
            .DidNotReceiveWithAnyArgs()
            .SetUpSponsorshipAsync(default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task RedeemSponsorship_NotSponsoredOrgOwner_ThrowsBadRequest(string sponsorshipToken, User user,
        OrganizationSponsorship sponsorship, OrganizationSponsorshipRedeemRequestModel model,
        SutProvider<OrganizationSponsorshipsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user.Id)
            .Returns(user);
        sutProvider.GetDependency<IValidateRedemptionTokenCommand>().ValidateRedemptionTokenAsync(sponsorshipToken,
            user.Email).Returns((true, sponsorship));
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(model.SponsoredOrganizationId).Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RedeemSponsorship(sponsorshipToken, model));

        Assert.Contains("Can only redeem sponsorship for an organization you own.", exception.Message);
        await sutProvider.GetDependency<ISetUpSponsorshipCommand>()
            .DidNotReceiveWithAnyArgs()
            .SetUpSponsorshipAsync(default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task RedeemSponsorship_NotSponsoredOrgOwner_Success(string sponsorshipToken, User user,
        OrganizationSponsorship sponsorship, Organization sponsoringOrganization,
        OrganizationSponsorshipRedeemRequestModel model, SutProvider<OrganizationSponsorshipsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user.Id)
            .Returns(user);
        sutProvider.GetDependency<IValidateRedemptionTokenCommand>().ValidateRedemptionTokenAsync(sponsorshipToken,
            user.Email).Returns((true, sponsorship));
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(model.SponsoredOrganizationId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(model.SponsoredOrganizationId).Returns(sponsoringOrganization);

        await sutProvider.Sut.RedeemSponsorship(sponsorshipToken, model);

        await sutProvider.GetDependency<ISetUpSponsorshipCommand>().Received(1)
            .SetUpSponsorshipAsync(sponsorship, sponsoringOrganization);
    }

    [Theory]
    [BitAutoData]
    public async Task PreValidateSponsorshipToken_ValidatesToken_Success(string sponsorshipToken, User user,
        OrganizationSponsorship sponsorship, SutProvider<OrganizationSponsorshipsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
        sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user.Id)
            .Returns(user);
        sutProvider.GetDependency<IValidateRedemptionTokenCommand>()
            .ValidateRedemptionTokenAsync(sponsorshipToken, user.Email).Returns((true, sponsorship));

        await sutProvider.Sut.PreValidateSponsorshipToken(sponsorshipToken);

        await sutProvider.GetDependency<IValidateRedemptionTokenCommand>().Received(1)
            .ValidateRedemptionTokenAsync(sponsorshipToken, user.Email);
    }

    [Theory]
    [BitAutoData]
    public async Task RevokeSponsorship_WrongSponsoringUser_ThrowsBadRequest(OrganizationUser sponsoringOrgUser,
        Guid currentUserId, SutProvider<OrganizationSponsorshipsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(currentUserId);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(sponsoringOrgUser.Id)
            .Returns(sponsoringOrgUser);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RevokeSponsorship(sponsoringOrgUser.Id));

        Assert.Contains("Can only revoke a sponsorship you granted.", exception.Message);
        await sutProvider.GetDependency<IRemoveSponsorshipCommand>()
            .DidNotReceiveWithAnyArgs()
            .RemoveSponsorshipAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task RemoveSponsorship_WrongOrgUserType_ThrowsBadRequest(Organization sponsoredOrg,
        SutProvider<OrganizationSponsorshipsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(Arg.Any<Guid>()).Returns(false);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RemoveSponsorship(sponsoredOrg.Id));

        Assert.Contains("Only the owner of an organization can remove sponsorship.", exception.Message);
        await sutProvider.GetDependency<IRemoveSponsorshipCommand>()
            .DidNotReceiveWithAnyArgs()
            .RemoveSponsorshipAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSponsoredOrganizations_OrganizationNotFound_ThrowsNotFound(
        Guid sponsoringOrgId,
        SutProvider<OrganizationSponsorshipsController> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoringOrgId).ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetSponsoredOrganizations(sponsoringOrgId));

        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetManyBySponsoringOrganizationAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSponsoredOrganizations_NotOrganizationOwner_ThrowsNotFound(
        Organization sponsoringOrg,
        SutProvider<OrganizationSponsorshipsController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoringOrg.Id).Returns(sponsoringOrg);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(sponsoringOrg.Id).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(sponsoringOrg.Id).Returns(false);

        // Create a CurrentContextOrganization with ManageUsers set to false
        var currentContextOrg = new CurrentContextOrganization
        {
            Id = sponsoringOrg.Id,
            Permissions = new Permissions { ManageUsers = false }
        };
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns(new List<CurrentContextOrganization> { currentContextOrg });

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sutProvider.Sut.GetSponsoredOrganizations(sponsoringOrg.Id));

        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetManyBySponsoringOrganizationAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task GetSponsoredOrganizations_Success_ReturnsSponsorships(
        Organization sponsoringOrg,
        List<OrganizationSponsorship> sponsorships,
        SutProvider<OrganizationSponsorshipsController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoringOrg.Id).Returns(sponsoringOrg);
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(sponsoringOrg.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(sponsoringOrg.Id).Returns(false);

        // Create a CurrentContextOrganization from the sponsoringOrg
        var currentContextOrg = new CurrentContextOrganization
        {
            Id = sponsoringOrg.Id,
            Permissions = new Permissions { ManageUsers = true }
        };
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns(new List<CurrentContextOrganization> { currentContextOrg });

        sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
            .GetManyBySponsoringOrganizationAsync(sponsoringOrg.Id).Returns(sponsorships);

        // Set IsAdminInitiated to true for all test sponsorships
        foreach (var sponsorship in sponsorships)
        {
            sponsorship.IsAdminInitiated = true;
        }

        // Act
        var result = await sutProvider.Sut.GetSponsoredOrganizations(sponsoringOrg.Id);

        // Assert
        Assert.Equal(sponsorships.Count, result.Data.Count());
        await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1)
            .GetManyBySponsoringOrganizationAsync(sponsoringOrg.Id);
    }
}
