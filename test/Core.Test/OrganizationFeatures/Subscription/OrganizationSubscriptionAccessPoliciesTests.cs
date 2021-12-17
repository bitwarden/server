using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using Organization = Bit.Core.Models.Table.Organization;

namespace Bit.Core.OrganizationFeatures.Subscription
{
    [SutProviderCustomize]
    public class OrganizationSubscriptionAccessPoliciesTests
    {
        [Theory, PaidOrganizationAutoData]
        public async Task UpgradePlan_OrganizationIsNull_Throws(OrganizationUpgrade upgrade,
            SutProvider<OrganizationSubscriptionAccessPolicies> sutProvider)
        {
            var result = await sutProvider.Sut.CanUpgradePlanAsync(null, upgrade);

            Assert.Equal(AccessPolicyResult.Fail(), result);
        }

        [Theory, BitAutoData]
        public async Task UpgradePlan_GatewayCustomIdIsNull_Throws(Organization organization,
            OrganizationUpgrade upgrade,
            SutProvider<OrganizationSubscriptionAccessPolicies> sutProvider)
        {
            organization.GatewayCustomerId = string.Empty;
            var result = await sutProvider.Sut.CanUpgradePlanAsync(organization, upgrade);

            Assert.Equal(AccessPolicyResult.Fail("Your account has no payment method available."), result);
        }

        [Theory, BitAutoData]
        public async Task UpgradePlan_AlreadyInPlan_Throws(Organization organization, OrganizationUpgrade upgrade,
            SutProvider<OrganizationSubscriptionAccessPolicies> sutProvider)
        {
            upgrade.Plan = organization.PlanType;
            var result = await sutProvider.Sut.CanUpgradePlanAsync(organization, upgrade);

            Assert.Equal(AccessPolicyResult.Fail("Organization is already on this plan."), result);
        }

        [Theory, PaidOrganizationAutoData]
        public async Task UpgradePlan_UpgradeFromPaidPlan_Throws(Organization organization, OrganizationUpgrade upgrade,
            SutProvider<OrganizationSubscriptionAccessPolicies> sutProvider)
        {
            var result = await sutProvider.Sut.CanUpgradePlanAsync(organization, upgrade);

            Assert.Equal(AccessPolicyResult.Fail("You can only upgrade from the free plan. Contact support."), result);
        }

        [Theory]
        [FreeOrganizationUpgradeAutoData]
        public async Task UpgradePlan_Passes(Organization organization, OrganizationUpgrade upgrade,
            SutProvider<OrganizationSubscriptionAccessPolicies> sutProvider)
        {
            var result = await sutProvider.Sut.CanUpgradePlanAsync(organization, upgrade);

            Assert.Equal(AccessPolicyResult.Success, result);
        }

        [Theory]
        [InlinePaidOrganizationAutoData(PlanType.EnterpriseAnnually,
            new object[] {"Cannot set max seat autoscaling below current seat count.", 1, 2})]
        [InlinePaidOrganizationAutoData(PlanType.EnterpriseAnnually,
            new object[] {"Cannot set max seat autoscaling below current seat count.", 4, 6})]
        [InlineFreeOrganizationAutoData("Your plan does not allow seat autoscaling.", 10, null)]
        public void UpdateAutoScaling_BadInput_Fails(string expectedMessage,
            int? maxAutoscaleSeats, int? currentSeats, Organization organization,
            SutProvider<OrganizationSubscriptionAccessPolicies> sutProvider)
        {
            organization.Seats = currentSeats;

            var result = sutProvider.Sut.CanUpdateAutoscaling(organization, maxAutoscaleSeats);

            Assert.Equal(AccessPolicyResult.Fail(expectedMessage), result);
        }

        [Theory, BitAutoData]
        public void UpdateSubscription_MaxSeatsBelowSeatCount_Fails(Organization org,
            SutProvider<OrganizationSubscriptionAccessPolicies> sutProvider)
        {
            org.Seats = 10;
            var seatAdjustment = 5;
            var maxAutoscaleSeats = 11;
            var result = sutProvider.Sut.CanUpdateSubscription(org, seatAdjustment, maxAutoscaleSeats);

            Assert.Equal(AccessPolicyResult.Fail("Cannot set max seat autoscaling below seat count."), result);
        }

        [Theory, BitAutoData]
        public async Task UpdateSubscription_NoOrganization_Throws(
            SutProvider<OrganizationSubscriptionAccessPolicies> sutProvider)
        {
            var result = sutProvider.Sut.CanUpdateAutoscaling(null, 0);

            Assert.Equal(AccessPolicyResult.Fail(), result);
        }

        [Theory]
        [InlinePaidOrganizationAutoData(0, 100, null, true, "")]
        [InlinePaidOrganizationAutoData(0, 100, 100, true, "")]
        [InlinePaidOrganizationAutoData(0, null, 100, true, "")]
        [InlinePaidOrganizationAutoData(1, 100, null, true, "")]
        [InlinePaidOrganizationAutoData(1, 100, 100, false, "Cannot invite new users. Seat limit has been reached.")]
        public void CanScale(int seatsToAdd, int? currentSeats, int? maxAutoscaleSeats,
            bool expectedResult, string expectedFailureMessage, Organization organization,
            SutProvider<OrganizationSubscriptionAccessPolicies> sutProvider)
        {
            organization.Seats = currentSeats;
            organization.MaxAutoscaleSeats = maxAutoscaleSeats;

            var result = sutProvider.Sut.CanScale(organization, seatsToAdd);

            Assert.Equal(new AccessPolicyResult(expectedResult, expectedFailureMessage), result);
        }

        [Theory, PaidOrganizationAutoData]
        public void CanScale_FailsOnSelfHosted(Organization organization,
            SutProvider<OrganizationSubscriptionAccessPolicies> sutProvider)
        {
            sutProvider.GetDependency<IGlobalSettings>().SelfHosted.Returns(true);
            var result = sutProvider.Sut.CanScale(organization, 10);

            Assert.Equal(AccessPolicyResult.Fail("Cannot autoscale on self-hosted instance."), result);
        }
    }
}
