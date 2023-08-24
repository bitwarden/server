using Bit.Api.Controllers;
using Bit.Api.Models.Request.Organizations;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Controllers;

[ControllerCustomize(typeof(OrganizationSponsorshipsController))]
[SutProviderCustomize]
public class OrganizationSponsorshipsControllerTests
{
    public static IEnumerable<object[]> EnterprisePlanTypes =>
        Enum.GetValues<PlanType>().Where(p => StaticStore.GetPasswordManagerPlan(p).Product == ProductType.Enterprise).Select(p => new object[] { p });
    public static IEnumerable<object[]> NonEnterprisePlanTypes =>
        Enum.GetValues<PlanType>().Where(p => StaticStore.GetPasswordManagerPlan(p).Product != ProductType.Enterprise).Select(p => new object[] { p });
    public static IEnumerable<object[]> NonFamiliesPlanTypes =>
        Enum.GetValues<PlanType>().Where(p => StaticStore.GetPasswordManagerPlan(p).Product != ProductType.Families).Select(p => new object[] { p });

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
}
