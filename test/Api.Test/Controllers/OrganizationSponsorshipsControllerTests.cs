using Xunit;
using Bit.Test.Common.AutoFixture.Attributes;
using System.Threading.Tasks;
using System;
using Bit.Core.Enums;
using System.Linq;
using System.Collections.Generic;
using Bit.Core.Models.Table;
using Bit.Test.Common.AutoFixture;
using Bit.Api.Controllers;
using Bit.Core.Context;
using NSubstitute;
using Bit.Core.Exceptions;
using Bit.Api.Test.AutoFixture.Attributes;
using Bit.Core.Repositories;
using Bit.Core.Models.Api.Request;
using Bit.Core.Services;
using Bit.Core.Models.Api;
using Bit.Core.Utilities;

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

        [Theory]
        [BitMemberAutoData(nameof(NonEnterprisePlanTypes))]
        public async Task CreateSponsorship_BadSponsoringOrgPlan_ThrowsBadRequest(PlanType sponsoringOrgPlan, Organization org,
            OrganizationSponsorshipRequestModel model, SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            org.PlanType = sponsoringOrgPlan;
            model.PlanSponsorshipType = PlanSponsorshipType.FamiliesForEnterprise;

            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.CreateSponsorship(org.Id.ToString(), model));

            Assert.Contains("Specified Organization cannot sponsor other organizations.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .OfferSponsorshipAsync(default, default, default, default);
        }

        public static IEnumerable<object[]> NonConfirmedOrganizationUsersStatuses =>
            Enum.GetValues<OrganizationUserStatusType>()
                .Where(s => s != OrganizationUserStatusType.Confirmed)
                .Select(s => new object[] { s });

        [Theory]
        [BitMemberAutoData(nameof(NonConfirmedOrganizationUsersStatuses))]
        public async Task CreateSponsorship_BadSponsoringUserStatus_ThrowsBadRequest(
            OrganizationUserStatusType statusType, Guid userId, Organization org, OrganizationUser orgUser,
            OrganizationSponsorshipRequestModel model, SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            org.PlanType = PlanType.EnterpriseAnnually;
            orgUser.Status = statusType;
            orgUser.UserId = userId;
            model.OrganizationUserId = orgUser.Id;

            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(orgUser.Id).Returns(orgUser);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.CreateSponsorship(org.Id.ToString(), model));

            Assert.Contains("Only confirm users can sponsor other organizations.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .OfferSponsorshipAsync(default, default, default, default);
        }

        [Theory]
        [BitAutoData("c56c7ab4-a174-412a-a822-abe53ea71d50")]
        public async Task CreateSponsorship_CreateSponsorshipAsDifferentUser_ThrowsBadRequest(Guid userId,
            Organization org, OrganizationUser orgUser, OrganizationSponsorshipRequestModel model,
            SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            org.PlanType = PlanType.EnterpriseAnnually;
            orgUser.Status = OrganizationUserStatusType.Confirmed;
            model.OrganizationUserId = orgUser.Id;

            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(orgUser.Id).Returns(orgUser);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.CreateSponsorship(org.Id.ToString(), model));

            Assert.Contains("Can only create organization sponsorships for yourself.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .OfferSponsorshipAsync(default, default, default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task CreateSponsorship_AlreadySponsoring_ThrowsBadRequest(Organization org,
            OrganizationUser orgUser, OrganizationSponsorship sponsorship,
            OrganizationSponsorshipRequestModel model, SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            org.PlanType = PlanType.EnterpriseAnnually;
            orgUser.Status = OrganizationUserStatusType.Confirmed;
            model.OrganizationUserId = orgUser.Id;

            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(orgUser.UserId);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(orgUser.Id).Returns(orgUser);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoringOrganizationUserIdAsync(orgUser.Id).Returns(sponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.CreateSponsorship(org.Id.ToString(), model));

            Assert.Contains("Can only sponsor one organization per Organization User.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .OfferSponsorshipAsync(default, default, default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RedeemSponsorship_BadToken_ThrowsBadRequest(string sponsorshipToken,
            OrganizationSponsorshipRedeemRequestModel model, SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationSponsorshipService>().ValidateRedemptionTokenAsync(sponsorshipToken)
                .Returns(false);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RedeemSponsorship(sponsorshipToken, model));

            Assert.Contains("Failed to parse sponsorship token.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .SetUpSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RedeemSponsorship_NotSponsoredOrgOwner_ThrowsBadRequest(string sponsorshipToken,
            OrganizationSponsorshipRedeemRequestModel model, SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationSponsorshipService>().ValidateRedemptionTokenAsync(sponsorshipToken)
                .Returns(true);
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
        public async Task RedeemSponsorship_SponsorshipNotFound_ThrowsBadRequest(string sponsorshipToken,
            OrganizationSponsorshipRedeemRequestModel model, User user,
            SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationSponsorshipService>().ValidateRedemptionTokenAsync(sponsorshipToken)
                .Returns(true);
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(model.SponsoredOrganizationId).Returns(true);
            sutProvider.GetDependency<ICurrentContext>().User.Returns(user);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>().GetByOfferedToEmailAsync(user.Email)
                .Returns((OrganizationSponsorship)null);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RedeemSponsorship(sponsorshipToken, model));

            Assert.Contains("No unredeemed sponsorship offer exists for you.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .SetUpSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RedeemSponsorship_OfferedToDifferentEmail_ThrowsBadRequest(string sponsorshipToken,
            OrganizationSponsorshipRedeemRequestModel model, User user, OrganizationSponsorship sponsorship,
            SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationSponsorshipService>().ValidateRedemptionTokenAsync(sponsorshipToken)
                .Returns(true);
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(model.SponsoredOrganizationId).Returns(true);
            sutProvider.GetDependency<ICurrentContext>().User.Returns(user);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>().GetByOfferedToEmailAsync(user.Email)
                .Returns(sponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RedeemSponsorship(sponsorshipToken, model));

            Assert.Contains("This sponsorship offer was issued to a different user email address.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .SetUpSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RedeemSponsorship_OrgAlreadySponsored_ThrowsBadRequest(string sponsorshipToken,
            OrganizationSponsorshipRedeemRequestModel model, User user, OrganizationSponsorship sponsorship,
            OrganizationSponsorship existingSponsorship, SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            user.Email = sponsorship.OfferedToEmail;

            sutProvider.GetDependency<IOrganizationSponsorshipService>().ValidateRedemptionTokenAsync(sponsorshipToken)
                .Returns(true);
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(model.SponsoredOrganizationId).Returns(true);
            sutProvider.GetDependency<ICurrentContext>().User.Returns(user);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetByOfferedToEmailAsync(sponsorship.OfferedToEmail).Returns(sponsorship);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(model.SponsoredOrganizationId).Returns(existingSponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RedeemSponsorship(sponsorshipToken, model));

            Assert.Contains("Cannot redeem a sponsorship offer for an organization that is already sponsored. Revoke existing sponsorship first.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .SetUpSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RedeemSponsorship_OrgNotFamiles_ThrowsBadRequest(PlanType planType, string sponsorshipToken,
            OrganizationSponsorshipRedeemRequestModel model, User user, OrganizationSponsorship sponsorship,
            Organization org, SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            user.Email = sponsorship.OfferedToEmail;
            org.PlanType = planType;

            sutProvider.GetDependency<IOrganizationSponsorshipService>().ValidateRedemptionTokenAsync(sponsorshipToken)
                .Returns(true);
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(model.SponsoredOrganizationId).Returns(true);
            sutProvider.GetDependency<ICurrentContext>().User.Returns(user);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetByOfferedToEmailAsync(sponsorship.OfferedToEmail).Returns(sponsorship);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(model.SponsoredOrganizationId).Returns((OrganizationSponsorship)null);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(model.SponsoredOrganizationId).Returns(org);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RedeemSponsorship(sponsorshipToken, model));

            Assert.Contains("Can only redeem sponsorship offer on families organizations.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .SetUpSponsorshipAsync(default, default);
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
                sutProvider.Sut.RevokeSponsorship(sponsoringOrgUser.Id.ToString()));

            Assert.Contains("Can only revoke a sponsorship you granted.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .RemoveSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RevokeSponsorship_NoExistingSponsorship_ThrowsBadRequest(OrganizationUser sponsoringOrgUser,
            OrganizationSponsorship sponsorship, SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(sponsoringOrgUser.UserId);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(sponsoringOrgUser.Id)
                .Returns(sponsoringOrgUser);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoringOrganizationUserIdAsync(Arg.Is<Guid>(v => v != sponsoringOrgUser.Id))
                .Returns(sponsorship);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoringOrganizationUserIdAsync(sponsoringOrgUser.Id)
                .Returns((OrganizationSponsorship)null);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RevokeSponsorship(sponsoringOrgUser.Id.ToString()));

            Assert.Contains("You are not currently sponsoring an organization.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .RemoveSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RevokeSponsorship_SponsorshipNotRedeemed_ThrowsBadRequest(OrganizationUser sponsoringOrgUser,
            OrganizationSponsorship sponsorship, SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            sponsorship.SponsoredOrganizationId = null;

            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(sponsoringOrgUser.UserId);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(sponsoringOrgUser.Id)
                .Returns(sponsoringOrgUser);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoringOrganizationUserIdAsync(Arg.Is<Guid>(v => v != sponsoringOrgUser.Id))
                .Returns(sponsorship);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoringOrganizationUserIdAsync(sponsoringOrgUser.Id)
                .Returns((OrganizationSponsorship)sponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RevokeSponsorship(sponsoringOrgUser.Id.ToString()));

            Assert.Contains("You are not currently sponsoring an organization.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .RemoveSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RevokeSponsorship_SponsoredOrgNotFound_ThrowsBadRequest(OrganizationUser sponsoringOrgUser,
            OrganizationSponsorship sponsorship, SutProvider<OrganizationSponsorshipsController> sutProvider)
        {

            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(sponsoringOrgUser.UserId);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(sponsoringOrgUser.Id)
                .Returns(sponsoringOrgUser);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoringOrganizationUserIdAsync(sponsoringOrgUser.Id)
                .Returns(sponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RevokeSponsorship(sponsoringOrgUser.Id.ToString()));

            Assert.Contains("Unable to find the sponsored Organization.", exception.Message);
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
                sutProvider.Sut.RemoveSponsorship(sponsoredOrg.Id.ToString()));

            Assert.Contains("Only the owner of an organization can remove sponsorship.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .RemoveSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RemoveSponsorship_NotSponsored_ThrowsBadRequest(Organization sponsoredOrg,
            OrganizationSponsorship sponsorship, SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(Arg.Any<Guid>()).Returns(true);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(sponsoredOrg.Id)
                .Returns((OrganizationSponsorship)null);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(Arg.Is<Guid>(v => v != sponsoredOrg.Id))
                .Returns(sponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RemoveSponsorship(sponsoredOrg.Id.ToString()));

            Assert.Contains("The requested organization is not currently being sponsored.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .RemoveSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RemoveSponsorship_SponsoredOrgNotFound_ThrowsBadRequest(Organization sponsoredOrg,
    OrganizationSponsorship sponsorship, SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(Arg.Any<Guid>()).Returns(true);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(sponsoredOrg.Id)
                .Returns(sponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RemoveSponsorship(sponsoredOrg.Id.ToString()));

            Assert.Contains("Unable to find the sponsored Organization.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .DidNotReceiveWithAnyArgs()
                .RemoveSponsorshipAsync(default, default);
        }
    }
}
