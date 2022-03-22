using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Cloud
{
    [SutProviderCustomize]
    public class SetUpSponsorshipCommandTests : FamiliesForEnterpriseTestsBase
    {
        [Theory]
        [BitAutoData]
        public async Task SetUpSponsorship_SponsorshipNotFound_ThrowsBadRequest(Organization org,
            SutProvider<SetUpSponsorshipCommand> sutProvider)
        {
            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.SetUpSponsorshipAsync(null, org));

            Assert.Contains("No unredeemed sponsorship offer exists for you.", exception.Message);
            await sutProvider.GetDependency<IPaymentService>()
                .DidNotReceiveWithAnyArgs()
                .SponsorOrganizationAsync(default, default);
            await sutProvider.GetDependency<IOrganizationRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
        }

        [Theory]
        [BitAutoData]
        public async Task SetUpSponsorship_OrgAlreadySponsored_ThrowsBadRequest(Organization org,
            OrganizationSponsorship sponsorship, OrganizationSponsorship existingSponsorship,
            SutProvider<SetUpSponsorshipCommand> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .GetBySponsoredOrganizationIdAsync(org.Id).Returns(existingSponsorship);

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.SetUpSponsorshipAsync(sponsorship, org));

            Assert.Contains("Cannot redeem a sponsorship offer for an organization that is already sponsored. Revoke existing sponsorship first.", exception.Message);
            await sutProvider.GetDependency<IPaymentService>()
                .DidNotReceiveWithAnyArgs()
                .SponsorOrganizationAsync(default, default);
            await sutProvider.GetDependency<IOrganizationRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
        }

        [Theory]
        [BitMemberAutoData(nameof(NonFamiliesPlanTypes))]
        public async Task SetUpSponsorship_OrgNotFamiles_ThrowsBadRequest(PlanType planType,
            OrganizationSponsorship sponsorship, Organization org,
            SutProvider<SetUpSponsorshipCommand> sutProvider)
        {
            org.PlanType = planType;

            var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
                sutProvider.Sut.SetUpSponsorshipAsync(sponsorship, org));

            Assert.Contains("Can only redeem sponsorship offer on families organizations.", exception.Message);
            await sutProvider.GetDependency<IPaymentService>()
                .DidNotReceiveWithAnyArgs()
                .SponsorOrganizationAsync(default, default);
            await sutProvider.GetDependency<IOrganizationRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
            await sutProvider.GetDependency<IOrganizationSponsorshipRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
        }
    }
}
