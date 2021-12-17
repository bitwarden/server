using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Api.Controllers;
using Bit.Api.Models.Request.Organizations;
using Bit.Api.Test.AutoFixture.Attributes;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Controllers
{
    [ControllerCustomize(typeof(OrganizationSponsorshipsController))]
    [SutProviderCustomize]
    public class OrganizationSponsorshipsControllerTests
    {
        public static IEnumerable<object[]> EnterprisePlanTypes =>
            Enum.GetValues<PlanType>().Where(p => StaticStore.GetPlan(p).Product == ProductType.Enterprise).Select(p => new object[] { p });
        public static IEnumerable<object[]> NonEnterprisePlanTypes =>
            Enum.GetValues<PlanType>().Where(p => StaticStore.GetPlan(p).Product != ProductType.Enterprise).Select(p => new object[] { p });
        public static IEnumerable<object[]> NonFamiliesPlanTypes =>
            Enum.GetValues<PlanType>().Where(p => StaticStore.GetPlan(p).Product != ProductType.Families).Select(p => new object[] { p });

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
            sutProvider.GetDependency<IOrganizationSponsorshipService>().ValidateRedemptionTokenAsync(sponsorshipToken,
                user.Email).Returns(false);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RedeemSponsorship(sponsorshipToken, model));

            Assert.Contains("Failed to parse sponsorship token.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .SetUpSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RedeemSponsorship_NotSponsoredOrgOwner_ThrowsBadRequest(string sponsorshipToken, User user,
            OrganizationSponsorshipRedeemRequestModel model, SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
            sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user.Id)
                .Returns(user);
            sutProvider.GetDependency<IOrganizationSponsorshipService>().ValidateRedemptionTokenAsync(sponsorshipToken,
                user.Email).Returns(true);
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(model.SponsoredOrganizationId).Returns(false);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RedeemSponsorship(sponsorshipToken, model));

            Assert.Contains("Can only redeem sponsorship for an organization you own.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .SetUpSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task PreValidateSponsorshipToken_ValidatesToken_Success(string sponsorshipToken, User user,
            SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
            sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user.Id)
                .Returns(user);

            await sutProvider.Sut.PreValidateSponsorshipToken(sponsorshipToken);

            await sutProvider.GetDependency<IOrganizationSponsorshipService>().Received(1)
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
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .RemoveSponsorshipAsync(default, default);
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
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .RemoveSponsorshipAsync(default, default);
        }
    }
}
