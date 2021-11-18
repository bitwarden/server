using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.DataProtection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.Services
{
    [SutProviderCustomize]
    public class OrganizationSponsorshipServiceTests
    {
        private static bool SponsorshipValidator(OrganizationSponsorship sponsorship, OrganizationSponsorship expectedSponsorship)
        {
            try
            {
                AssertHelper.AssertPropertyEqual(sponsorship, expectedSponsorship, nameof(OrganizationSponsorship.Id));
                return true;
            }
            catch
            {
                return false;
            }
        }

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
        public async Task OfferSponsorship_CreatesSponsorship(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            string sponsoredEmail, string friendlyName, Guid sponsorshipId,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            var dataProtector = Substitute.For<IDataProtector>();
            sutProvider.GetDependency<IDataProtectionProvider>().CreateProtector(default).ReturnsForAnyArgs(dataProtector);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>().CreateAsync(default).ReturnsForAnyArgs(callInfo =>
            {
                var sponsorship = callInfo.Arg<OrganizationSponsorship>();
                sponsorship.Id = sponsorshipId;
                return sponsorship;
            });

            await sutProvider.Sut.OfferSponsorshipAsync(sponsoringOrg, sponsoringOrgUser,
                PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, friendlyName);

            var expectedSponsorship = new OrganizationSponsorship
            {
                Id = sponsorshipId,
                SponsoringOrganizationId = sponsoringOrg.Id,
                SponsoringOrganizationUserId = sponsoringOrgUser.Id,
                FriendlyName = friendlyName,
                OfferedToEmail = sponsoredEmail,
                PlanSponsorshipType = PlanSponsorshipType.FamiliesForEnterprise,
                CloudSponsor = true,
            };

            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1)
                .CreateAsync(Arg.Is<OrganizationSponsorship>(s => SponsorshipValidator(s, expectedSponsorship)));

            await sutProvider.GetDependency<IMailService>().Received(1).
                SendFamiliesForEnterpriseOfferEmailAsync(sponsoredEmail, sponsoringOrg.Name,
                false, Arg.Any<string>());
        }

        [Theory]
        [BitAutoData]
        public async Task OfferSponsorship_CreateSponsorshipThrows_RevertsDatabase(Organization sponsoringOrg, OrganizationUser sponsoringOrgUser,
            string sponsoredEmail, string friendlyName, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            var expectedException = new Exception();
            OrganizationSponsorship createdSponsorship = null;
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>().CreateAsync(default).ThrowsForAnyArgs(callInfo =>
            {
                createdSponsorship = callInfo.ArgAt<OrganizationSponsorship>(0);
                createdSponsorship.Id = Guid.NewGuid();
                return expectedException;
            });

            var actualException = await Assert.ThrowsAsync<Exception>(() =>
                sutProvider.Sut.OfferSponsorshipAsync(sponsoringOrg, sponsoringOrgUser,
                    PlanSponsorshipType.FamiliesForEnterprise, sponsoredEmail, friendlyName));
            Assert.Same(expectedException, actualException);

            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1)
                .DeleteAsync(createdSponsorship);
        }

        [Theory]
        [BitAutoData]
        public async Task SendSponsorshipOfferAsync(Organization org, OrganizationSponsorship sponsorship,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sutProvider.GetDependency<IUserRepository>()
                .GetByEmailAsync(sponsorship.OfferedToEmail)
                .Returns(Task.FromResult(new User()));

            await sutProvider.Sut.SendSponsorshipOfferAsync(org, sponsorship);

            await sutProvider.GetDependency<IMailService>().Received(1)
                .SendFamiliesForEnterpriseOfferEmailAsync(sponsorship.OfferedToEmail, org.Name, true, Arg.Any<string>());
        }

        private async Task AssertRemovedSponsoredPaymentAsync(Organization sponsoredOrg,
            OrganizationSponsorship sponsorship, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            await sutProvider.GetDependency<IPaymentService>().Received(1)
                .RemoveOrganizationSponsorshipAsync(sponsoredOrg, sponsorship);
            await sutProvider.GetDependency<IOrganizationRepository>().Received(1).UpsertAsync(sponsoredOrg);
            await sutProvider.GetDependency<IMailService>().Received(1)
                .SendFamiliesForEnterpriseSponsorshipRevertingEmailAsync(sponsoredOrg.BillingEmailAddress(), sponsoredOrg.Name);
        }

        private async Task AssertRemovedSponsorshipAsync(OrganizationSponsorship sponsorship,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            if (sponsorship.CloudSponsor || sponsorship.SponsorshipLapsedDate.HasValue)
            {
                await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1)
                    .DeleteAsync(sponsorship);
            }
            else
            {
                await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1)
                    .UpsertAsync(sponsorship);
            }
        }

        private static async Task AssertDidNotRemoveSponsoredPaymentAsync(SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            await sutProvider.GetDependency<IPaymentService>().DidNotReceiveWithAnyArgs()
                .RemoveOrganizationSponsorshipAsync(default, default);
            await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
            await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs()
                .SendFamiliesForEnterpriseSponsorshipRevertingEmailAsync(default, default);
        }

        private static async Task AssertDidNotRemoveSponsorshipAsync(SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
                .DeleteAsync(default);
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
        }

        [Theory]
        [BitAutoData]
        public async Task ValidateSponsorshipAsync_NoSponsoredOrg_EarlyReturn(Guid sponsoredOrgId,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoredOrgId).Returns((Organization)null);

            var result = await sutProvider.Sut.ValidateSponsorshipAsync(sponsoredOrgId);

            Assert.False(result);
            await AssertDidNotRemoveSponsoredPaymentAsync(sutProvider);
            await AssertDidNotRemoveSponsorshipAsync(sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task ValidateSponsorshipAsync_NoExistingSponsorship_UpdatesStripePlan(Organization sponsoredOrg,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoredOrg.Id).Returns(sponsoredOrg);

            var result = await sutProvider.Sut.ValidateSponsorshipAsync(sponsoredOrg.Id);

            Assert.False(result);
            await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, null, sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task ValidateSponsorshipAsync_SponsoringOrgNull_UpdatesStripePlan(Organization sponsoredOrg,
            OrganizationSponsorship existingSponsorship, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            existingSponsorship.SponsoringOrganizationId = null;

            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(sponsoredOrg.Id).Returns(existingSponsorship);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoredOrg.Id).Returns(sponsoredOrg);

            var result = await sutProvider.Sut.ValidateSponsorshipAsync(sponsoredOrg.Id);

            Assert.False(result);
            await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, existingSponsorship, sutProvider);
            await AssertRemovedSponsorshipAsync(existingSponsorship, sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task ValidateSponsorshipAsync_SponsoringOrgUserNull_UpdatesStripePlan(Organization sponsoredOrg,
            OrganizationSponsorship existingSponsorship, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            existingSponsorship.SponsoringOrganizationUserId = null;

            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(sponsoredOrg.Id).Returns(existingSponsorship);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoredOrg.Id).Returns(sponsoredOrg);

            var result = await sutProvider.Sut.ValidateSponsorshipAsync(sponsoredOrg.Id);

            Assert.False(result);
            await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, existingSponsorship, sutProvider);
            await AssertRemovedSponsorshipAsync(existingSponsorship, sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task ValidateSponsorshipAsync_SponsorshipTypeNull_UpdatesStripePlan(Organization sponsoredOrg,
            OrganizationSponsorship existingSponsorship, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            existingSponsorship.PlanSponsorshipType = null;

            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(sponsoredOrg.Id).Returns(existingSponsorship);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoredOrg.Id).Returns(sponsoredOrg);

            var result = await sutProvider.Sut.ValidateSponsorshipAsync(sponsoredOrg.Id);

            Assert.False(result);
            await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, existingSponsorship, sutProvider);
            await AssertRemovedSponsorshipAsync(existingSponsorship, sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task ValidateSponsorshipAsync_SponsoringOrgNotFound_UpdatesStripePlan(Organization sponsoredOrg,
            OrganizationSponsorship existingSponsorship, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(sponsoredOrg.Id).Returns(existingSponsorship);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoredOrg.Id).Returns(sponsoredOrg);

            var result = await sutProvider.Sut.ValidateSponsorshipAsync(sponsoredOrg.Id);

            Assert.False(result);
            await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, existingSponsorship, sutProvider);
            await AssertRemovedSponsorshipAsync(existingSponsorship, sutProvider);
        }

        [Theory]
        [BitMemberAutoData(nameof(NonEnterprisePlanTypes))]
        public async Task ValidateSponsorshipAsync_SponsoringOrgNotEnterprise_UpdatesStripePlan(PlanType planType,
            Organization sponsoredOrg, OrganizationSponsorship existingSponsorship, Organization sponsoringOrg,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sponsoringOrg.PlanType = planType;
            existingSponsorship.SponsoringOrganizationId = sponsoringOrg.Id;

            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(sponsoredOrg.Id).Returns(existingSponsorship);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoredOrg.Id).Returns(sponsoredOrg);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoringOrg.Id).Returns(sponsoringOrg);

            var result = await sutProvider.Sut.ValidateSponsorshipAsync(sponsoredOrg.Id);

            Assert.False(result);
            await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, existingSponsorship, sutProvider);
            await AssertRemovedSponsorshipAsync(existingSponsorship, sutProvider);
        }

        [Theory]
        [BitMemberAutoData(nameof(EnterprisePlanTypes))]
        public async Task ValidateSponsorshipAsync_SponsoringOrgDisabled_UpdatesStripePlan(PlanType planType,
            Organization sponsoredOrg, OrganizationSponsorship existingSponsorship, Organization sponsoringOrg,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sponsoringOrg.PlanType = planType;
            sponsoringOrg.Enabled = false;
            existingSponsorship.SponsoringOrganizationId = sponsoringOrg.Id;

            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(sponsoredOrg.Id).Returns(existingSponsorship);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoredOrg.Id).Returns(sponsoredOrg);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoringOrg.Id).Returns(sponsoringOrg);

            var result = await sutProvider.Sut.ValidateSponsorshipAsync(sponsoredOrg.Id);

            Assert.False(result);
            await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, existingSponsorship, sutProvider);
            await AssertRemovedSponsorshipAsync(existingSponsorship, sutProvider);
        }

        [Theory]
        [BitMemberAutoData(nameof(EnterprisePlanTypes))]
        public async Task ValidateSponsorshipAsync_Valid(PlanType planType,
            Organization sponsoredOrg, OrganizationSponsorship existingSponsorship, Organization sponsoringOrg,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sponsoringOrg.PlanType = planType;
            sponsoringOrg.Enabled = true;
            existingSponsorship.SponsoringOrganizationId = sponsoringOrg.Id;

            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(sponsoredOrg.Id).Returns(existingSponsorship);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoredOrg.Id).Returns(sponsoredOrg);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(sponsoringOrg.Id).Returns(sponsoringOrg);

            var result = await sutProvider.Sut.ValidateSponsorshipAsync(sponsoredOrg.Id);

            Assert.True(result);

            await AssertDidNotRemoveSponsoredPaymentAsync(sutProvider);
            await AssertDidNotRemoveSponsorshipAsync(sutProvider);
        }


        [Theory]
        [BitAutoData]
        public async Task RemoveSponsorshipAsync_NullDoNothing(SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            await sutProvider.Sut.RemoveSponsorshipAsync(null, null);

            await AssertDidNotRemoveSponsoredPaymentAsync(sutProvider);
            await AssertDidNotRemoveSponsorshipAsync(sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task RemoveSponsorshipAsync_NullSponsoredOrg(OrganizationSponsorship sponsorship,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            await sutProvider.Sut.RemoveSponsorshipAsync(null, sponsorship);

            await AssertDidNotRemoveSponsoredPaymentAsync(sutProvider);
            await AssertRemovedSponsorshipAsync(sponsorship, sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task RemoveSponsorshipAsync_NullSponsorship(Organization sponsoredOrg,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            await sutProvider.Sut.RemoveSponsorshipAsync(sponsoredOrg, null);

            await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, null, sutProvider);
            await AssertDidNotRemoveSponsorshipAsync(sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task RemoveSponsorshipAsync_RemoveBoth(Organization sponsoredOrg,
            OrganizationSponsorship sponsorship, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            await sutProvider.Sut.RemoveSponsorshipAsync(sponsoredOrg, sponsorship);

            await AssertRemovedSponsoredPaymentAsync(sponsoredOrg, sponsorship, sutProvider);
            await AssertRemovedSponsorshipAsync(sponsorship, sutProvider);
        }

        [Theory]
        [BitAutoData]
        public async Task RemoveSponsorshipAsync_SponsoredOrgNotFound_ThrowsBadRequest(Organization sponsoredOrg,
            OrganizationSponsorship sponsorship, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(Arg.Any<Guid>()).Returns(true);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(sponsoredOrg.Id)
                .Returns(sponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RemoveSponsorshipAsync(sponsoredOrg.Id));

            Assert.Contains("Unable to find the sponsored Organization.", exception.Message);
            await sutProvider.Sut
                .DidNotReceiveWithAnyArgs()
                .RemoveSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RemoveSponsorship_NotSponsored_ThrowsBadRequest(Organization sponsoredOrg,
            OrganizationSponsorship sponsorship, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(Arg.Any<Guid>()).Returns(true);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(sponsoredOrg.Id)
                .Returns((OrganizationSponsorship)null);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(Arg.Is<Guid>(v => v != sponsoredOrg.Id))
                .Returns(sponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RemoveSponsorshipAsync(sponsoredOrg.Id));

            Assert.Contains("The requested organization is not currently being sponsored.", exception.Message);
            await sutProvider.Sut
                .DidNotReceiveWithAnyArgs()
                .RemoveSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RemoveSponsorshipAsync_WrongOrgUserType_ThrowsBadRequest(Organization sponsoredOrg,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(Arg.Any<Guid>()).Returns(false);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RemoveSponsorshipAsync(sponsoredOrg.Id));

            Assert.Contains("Only the owner of an organization can remove sponsorship.", exception.Message);
            await sutProvider.Sut
                .DidNotReceiveWithAnyArgs()
                .RemoveSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RevokeSponsorshipAsync_SponsoredOrgNotFound_ThrowsBadRequest(OrganizationUser orgUser,
            OrganizationSponsorship sponsorship, SutProvider<OrganizationSponsorshipService> sutProvider)
        {

            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(orgUser.UserId);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(orgUser.OrganizationId, orgUser.UserId.Value)
                .Returns(orgUser);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoringOrganizationUserIdAsync(orgUser.Id)
                .Returns(sponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RevokeSponsorshipAsync(orgUser.OrganizationId));

            Assert.Contains("Unable to find the sponsored Organization.", exception.Message);
            await sutProvider.Sut
                .DidNotReceiveWithAnyArgs()
                .RemoveSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RevokeSponsorshipAsync_SponsorshipNotRedeemed_ThrowsBadRequest(OrganizationUser orgUser,
            OrganizationSponsorship sponsorship, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sponsorship.SponsoredOrganizationId = null;

            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(orgUser.UserId);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(orgUser.OrganizationId, orgUser.UserId.Value)
                .Returns(orgUser);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoringOrganizationUserIdAsync(Arg.Is<Guid>(v => v != orgUser.Id))
                .Returns(sponsorship);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoringOrganizationUserIdAsync(orgUser.Id)
                .Returns((OrganizationSponsorship)sponsorship);

            await sutProvider.Sut.RevokeSponsorshipAsync(orgUser.OrganizationId);

            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>().Received(1).DeleteAsync(sponsorship);

            await sutProvider.Sut
                .DidNotReceiveWithAnyArgs()
                .RemoveSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RevokeSponsorshipAsync_NoExistingSponsorship_ThrowsBadRequest(OrganizationUser orgUser,
            OrganizationSponsorship sponsorship, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(orgUser.UserId);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(orgUser.OrganizationId, orgUser.UserId.Value)
                .Returns(orgUser);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoringOrganizationUserIdAsync(Arg.Is<Guid>(v => v != orgUser.Id))
                .Returns(sponsorship);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoringOrganizationUserIdAsync(orgUser.Id)
                .Returns((OrganizationSponsorship)null);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RevokeSponsorshipAsync(orgUser.OrganizationId));

            Assert.Contains("You are not currently sponsoring an organization.", exception.Message);
            await sutProvider.Sut
                .DidNotReceiveWithAnyArgs()
                .RemoveSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RevokeSponsorshipAsync_WrongSponsoringUser_ThrowsBadRequest(OrganizationUser sponsoringOrgUser,
            Guid currentUserId, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(currentUserId);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(sponsoringOrgUser.Id)
                .Returns(sponsoringOrgUser);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RevokeSponsorshipAsync(sponsoringOrgUser.Id));

            Assert.Contains("Can only revoke a sponsorship you granted.", exception.Message);
            await sutProvider.Sut
                .DidNotReceiveWithAnyArgs()
                .RemoveSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RedeemSponsorshipAsync_OrgNotFamiles_ThrowsBadRequest(PlanType planType, string sponsorshipToken,
            OrganizationSponsorshipRedeemRequestModel model, User user, OrganizationSponsorship sponsorship,
            Organization org, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            user.Email = sponsorship.OfferedToEmail;
            org.PlanType = planType;

            sutProvider.Sut.ValidateRedemptionTokenAsync(sponsorshipToken)
                .Returns(true);
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(model.SponsoredOrganizationId).Returns(true);
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
            sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user.Id).Returns(user);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetByOfferedToEmailAsync(sponsorship.OfferedToEmail).Returns(sponsorship);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(model.SponsoredOrganizationId).Returns((OrganizationSponsorship)null);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(model.SponsoredOrganizationId).Returns(org);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RedeemSponsorshipAsync(sponsorshipToken, model));

            Assert.Contains("Can only redeem sponsorship offer on families organizations.", exception.Message);
            await sutProvider.Sut
                .DidNotReceiveWithAnyArgs()
                .SetUpSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RedeemSponsorshipAsync_OrgAlreadySponsored_ThrowsBadRequest(string sponsorshipToken,
            OrganizationSponsorshipRedeemRequestModel model, User user, OrganizationSponsorship sponsorship,
            OrganizationSponsorship existingSponsorship, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            user.Email = sponsorship.OfferedToEmail;

            sutProvider.Sut.ValidateRedemptionTokenAsync(sponsorshipToken)
                .Returns(true);
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(model.SponsoredOrganizationId).Returns(true);
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
            sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user.Id).Returns(user);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetByOfferedToEmailAsync(sponsorship.OfferedToEmail).Returns(sponsorship);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(model.SponsoredOrganizationId).Returns(existingSponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RedeemSponsorshipAsync(sponsorshipToken, model));

            Assert.Contains("Cannot redeem a sponsorship offer for an organization that is already sponsored. Revoke existing sponsorship first.", exception.Message);
            await sutProvider.Sut
                .DidNotReceiveWithAnyArgs()
                .SetUpSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RedeemSponsorshipAsync_OfferedToDifferentEmail_ThrowsBadRequest(string sponsorshipToken,
            OrganizationSponsorshipRedeemRequestModel model, User user, OrganizationSponsorship sponsorship,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sutProvider.Sut.ValidateRedemptionTokenAsync(sponsorshipToken)
                .Returns(true);
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(model.SponsoredOrganizationId).Returns(true);
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
            sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user.Id).Returns(user);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>().GetByOfferedToEmailAsync(user.Email)
                .Returns(sponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RedeemSponsorshipAsync(sponsorshipToken, model));

            Assert.Contains("This sponsorship offer was issued to a different user email address.", exception.Message);
            await sutProvider.Sut
                .DidNotReceiveWithAnyArgs()
                .SetUpSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RedeemSponsorshipAsync_SponsorshipNotFound_ThrowsBadRequest(string sponsorshipToken,
            OrganizationSponsorshipRedeemRequestModel model, User user,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sutProvider.Sut.ValidateRedemptionTokenAsync(sponsorshipToken)
                .Returns(true);
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(model.SponsoredOrganizationId).Returns(true);
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(user.Id);
            sutProvider.GetDependency<IUserService>().GetUserByIdAsync(user.Id).Returns(user);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>().GetByOfferedToEmailAsync(user.Email)
                .Returns((OrganizationSponsorship)null);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RedeemSponsorshipAsync(sponsorshipToken, model));

            Assert.Contains("No unredeemed sponsorship offer exists for you.", exception.Message);
            await sutProvider.Sut
                .DidNotReceiveWithAnyArgs()
                .SetUpSponsorshipAsync(default, default);
        }
        

        [Theory]
        [BitMemberAutoData(nameof(NonEnterprisePlanTypes))]
        public async Task CreateSponsorshipAsync_BadSponsoringOrgPlan_ThrowsBadRequest(PlanType sponsoringOrgPlan, Organization org,
            OrganizationSponsorshipRequestModel model, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            org.PlanType = sponsoringOrgPlan;
            model.PlanSponsorshipType = PlanSponsorshipType.FamiliesForEnterprise;

            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.CreateSponsorshipAsync(org.Id, model));

            Assert.Contains("Specified Organization cannot sponsor other organizations.", exception.Message);
            await sutProvider.Sut
                .DidNotReceiveWithAnyArgs()
                .OfferSponsorshipAsync(default, default, default, default, default);
        }

        [Theory]
        [BitMemberAutoData(nameof(NonConfirmedOrganizationUsersStatuses))]
        public async Task CreateSponsorship_BadSponsoringUserStatus_ThrowsBadRequest(
            OrganizationUserStatusType statusType, Organization org, OrganizationUser orgUser,
            OrganizationSponsorshipRequestModel model, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            org.PlanType = PlanType.EnterpriseAnnually;
            orgUser.Status = statusType;

            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(orgUser.UserId);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(org.Id, orgUser.UserId.Value)
                .Returns(orgUser);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.CreateSponsorshipAsync(org.Id, model));

            Assert.Contains("Only confirmed users can sponsor other organizations.", exception.Message);
            await sutProvider.Sut
                .DidNotReceiveWithAnyArgs()
                .OfferSponsorshipAsync(default, default, default, default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task CreateSponsorshipAsync_AlreadySponsoring_ThrowsBadRequest(Organization org,
            OrganizationUser orgUser, OrganizationSponsorship sponsorship,
            OrganizationSponsorshipRequestModel model, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            org.PlanType = PlanType.EnterpriseAnnually;
            orgUser.Status = OrganizationUserStatusType.Confirmed;

            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(orgUser.UserId);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(org.Id, orgUser.UserId.Value)
                .Returns(orgUser);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoringOrganizationUserIdAsync(orgUser.Id).Returns(sponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.CreateSponsorshipAsync(org.Id, model));

            Assert.Contains("Can only sponsor one organization per Organization User.", exception.Message);
            await sutProvider.Sut
                .DidNotReceiveWithAnyArgs()
                .OfferSponsorshipAsync(default, default, default, default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task ResendSponsorshipOfferAsync_SponsoringOrgNotFound_ThrowsBadRequest(Guid sponsoringOrgId,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.ResendSponsorshipOfferAsync(sponsoringOrgId));

            Assert.Contains("Cannot find the requested sponsoring organization.", exception.Message);
            await sutProvider.Sut
                .DidNotReceiveWithAnyArgs()
                .SendSponsorshipOfferAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task ResendSponsorshipOfferAsync_SponsoringOrgUserNotFound_ThrowsBadRequest(Organization org,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.ResendSponsorshipOfferAsync(org.Id));

            Assert.Contains("Only confirmed users can sponsor other organizations.", exception.Message);
            await sutProvider.Sut
                .DidNotReceiveWithAnyArgs()
                .SendSponsorshipOfferAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        [BitMemberAutoData(nameof(NonConfirmedOrganizationUsersStatuses))]
        public async Task ResendSponsorshipOfferAsync_SponsoringOrgUserNotConfirmed_ThrowsBadRequest(OrganizationUserStatusType status,
            Organization org, OrganizationUser orgUser,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            orgUser.Status = status;

            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(orgUser.UserId);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(org.Id, orgUser.UserId.Value)
                .Returns(orgUser);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.ResendSponsorshipOfferAsync(org.Id));

            Assert.Contains("Only confirmed users can sponsor other organizations.", exception.Message);
            await sutProvider.Sut
                .DidNotReceiveWithAnyArgs()
                .SendSponsorshipOfferAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task ResendSponsorshipOfferAsync_SponsorshipNotFound_ThrowsBadRequest(Organization org,
            OrganizationUser orgUser, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            orgUser.Status = OrganizationUserStatusType.Confirmed;

            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(orgUser.UserId);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(org.Id, orgUser.UserId.Value)
                .Returns(orgUser);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.ResendSponsorshipOfferAsync(org.Id));

            Assert.Contains("Cannot find an outstanding sponsorship offer for this organization.", exception.Message);
            await sutProvider.Sut
                .DidNotReceiveWithAnyArgs()
                .SendSponsorshipOfferAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task ResendSponsorshipOfferAsync_NoOfferToEmail_ThrowsBadRequest(Organization org,
            OrganizationUser orgUser, OrganizationSponsorship sponsorship,
            SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            orgUser.Status = OrganizationUserStatusType.Confirmed;
            sponsorship.OfferedToEmail = null;

            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
            sutProvider.GetDependency<ICurrentContext>().UserId.Returns(orgUser.UserId);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(org.Id, orgUser.UserId.Value)
                .Returns(orgUser);
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>().GetBySponsoringOrganizationUserIdAsync(orgUser.Id)
                .Returns(sponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.ResendSponsorshipOfferAsync(org.Id));

            Assert.Contains("Cannot find an outstanding sponsorship offer for this organization.", exception.Message);
            await sutProvider.Sut
                .DidNotReceiveWithAnyArgs()
                .SendSponsorshipOfferAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RedeemSponsorshipAsync_BadToken_ThrowsBadRequest(string sponsorshipToken,
            OrganizationSponsorshipRedeemRequestModel model, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sutProvider.Sut
                .ValidateRedemptionTokenAsync(sponsorshipToken)
                .Returns(false);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RedeemSponsorshipAsync(sponsorshipToken, model));

            Assert.Contains("Failed to parse sponsorship token.", exception.Message);
            await sutProvider.Sut
                .DidNotReceiveWithAnyArgs()
                .SetUpSponsorshipAsync(default, default);
        }

        [Theory]
        [BitAutoData]
        public async Task RedeemSponsorshipAsync_NotSponsoredOrgOwner_ThrowsBadRequest(string sponsorshipToken,
            OrganizationSponsorshipRedeemRequestModel model, SutProvider<OrganizationSponsorshipService> sutProvider)
        {
            sutProvider.Sut
                .ValidateRedemptionTokenAsync(sponsorshipToken)
                .Returns(true);
            sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(model.SponsoredOrganizationId).Returns(false);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.RedeemSponsorshipAsync(sponsorshipToken, model));

            Assert.Contains("Can only redeem sponsorship for an organization you own.", exception.Message);
            await sutProvider.Sut
                .DidNotReceiveWithAnyArgs()
                .SetUpSponsorshipAsync(default, default);
        }
    }
}
