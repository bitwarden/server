using System;
using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.Subscription;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using Organization = Bit.Core.Models.Table.Organization;


namespace Bit.Core.Test.OrganizationFeatures.Subscription
{
    public class OrganizationSubscriptionServiceTests
    {
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task UpgradePlan_ChecksAccessPolicy(Guid organizationId, OrganizationUpgrade upgrade,
        SutProvider<OrganizationSubscriptionService> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationSubscriptionAccessPolicies>().CanUpgradePlanAsync(default, default).ReturnsForAnyArgs(
                AccessPolicyResult.Fail(),
                AccessPolicyResult.Fail("Fail Reason"));

            var notFoundException = await Assert.ThrowsAsync<NotFoundException>(
                () => sutProvider.Sut.UpgradePlanAsync(organizationId, upgrade));

            var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.UpgradePlanAsync(organizationId, upgrade));

            Assert.Equal("Fail Reason", badRequestException.Message);
        }

        [Theory]
        [FreeOrganizationUpgradeAutoData]
        public async Task UpgradePlan_Passes(Organization organization, OrganizationUpgrade upgrade,
        SutProvider<OrganizationSubscriptionService> sutProvider)
        {
            sutProvider.GetDependency<IOrganizationSubscriptionAccessPolicies>().CanUpgradePlanAsync(default, default).ReturnsForAnyArgs(AccessPolicyResult.Success);
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
            await sutProvider.Sut.UpgradePlanAsync(organization.Id, upgrade);
            await sutProvider.GetDependency<IPaymentService>().Received(1).UpgradeFreeOrganizationAsync(
                Arg.Any<Organization>(), Arg.Any<Plan>(), upgrade.AdditionalStorageGb, upgrade.AdditionalSeats, upgrade.PremiumAccessAddon, upgrade.TaxInfo);
            await sutProvider.GetDependency<IOrganizationService>().ReceivedWithAnyArgs(1).ReplaceAndUpdateCache(default);
        }
    }
}
