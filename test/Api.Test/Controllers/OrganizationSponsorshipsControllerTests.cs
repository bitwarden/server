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

namespace Bit.Api.Test.Controllers
{
    [ControllerCustomize(typeof(OrganizationSponsorshipsController))]
    [SutProviderCustomize]
    public class OrganizationSponsorshipsControllerTests
    {
        public static IEnumerable<object[]> EnterprisePlanTypes =>
            Enum.GetValues<PlanType>().Where(p => PlanTypeHelper.IsEnterprise(p)).Select(p => new object[] { p });
        public static IEnumerable<object[]> NonEnterprisePlanTypes =>
            Enum.GetValues<PlanType>().Where(p => !PlanTypeHelper.IsEnterprise(p)).Select(p => new object[] { p });

        [Theory]
        [BitMemberAutoData(nameof(NonEnterprisePlanTypes))]
        public async Task CreateSponsorship_BadSponsoringOrgPlan_ThrowsBadRequest(PlanType sponsoringOrgPlan, Organization org,
            SutProvider<OrganizationSponsorshipsController> sutProvider)
        {
            org.PlanType = sponsoringOrgPlan;
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.CreateSponsorship(org.Id.ToString(), null));

            Assert.Contains("Specified Organization cannot sponsor other organizations.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .OfferSponsorshipAsync(default, default, default)
                .DidNotReceiveWithAnyArgs();
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
                .OfferSponsorshipAsync(default, default, default)
                .DidNotReceiveWithAnyArgs();
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
                .OfferSponsorshipAsync(default, default, default)
                .DidNotReceiveWithAnyArgs();
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
                .OfferSponsorshipAsync(default, default, default)
                .DidNotReceiveWithAnyArgs();
        }

        // TODO: Test redeem sponsorship

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
                .RemoveSponsorshipAsync(default)
                .DidNotReceiveWithAnyArgs();
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

            Assert.Contains("You are not currently sponsoring and organization.", exception.Message);
            await sutProvider.GetDependency<IOrganizationSponsorshipService>()
                .RemoveSponsorshipAsync(default)
                .DidNotReceiveWithAnyArgs();
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
                .RemoveSponsorshipAsync(default)
                .DidNotReceiveWithAnyArgs();
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
                .RemoveSponsorshipAsync(default)
                .DidNotReceiveWithAnyArgs();
        }
    }
}
