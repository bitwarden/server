using System.Linq;
using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.Subscription
{
    public class OrganizationSubscriptionAccessPolicies : BaseAccessPolicies, IOrganizationSubscriptionAccessPolicies
    {
        readonly IOrganizationUserRepository _organizationUserRepository;
        readonly ICollectionRepository _collectionRepository;
        readonly IGroupRepository _groupRepository;
        readonly IPolicyRepository _policyRepository;
        readonly ISsoConfigRepository _ssoConfigRepository;
        readonly IGlobalSettings _globalSettings;

        public OrganizationSubscriptionAccessPolicies(IGlobalSettings globalSettings)
        {
            _globalSettings = globalSettings;
        }

        public AccessPolicyResult CanCancel(Organization organization)
        {
            if (organization == null)
            {
                return Fail();
            }

            return Success;
        }

        public AccessPolicyResult CanReinstate(Organization organization)
        {
            if (organization == null)
            {
                return Fail();
            }

            return Success;
        }

        public AccessPolicyResult CanScale(Organization organization, int seatsToAdd)
        {
            if (seatsToAdd < 1)
            {
                return Success;
            }

            if (_globalSettings.SelfHosted)
            {
                return Fail("Cannot autoscale on self-hosted instance.");
            }

            if (organization.Seats.HasValue &&
                organization.MaxAutoscaleSeats.HasValue &&
                organization.MaxAutoscaleSeats.Value < organization.Seats.Value + seatsToAdd)
            {
                return Fail($"Cannot invite new users. Seat limit has been reached.");
            }

            return Success;
        }

        public AccessPolicyResult CanAdjustSeats(Organization organization, int seatAdjustment,
            int currentUserCount)
        {
            if (organization.Seats == null)
            {
                return Fail("Organization has no seat limit, no need to adjust seats");
            }

            if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
            {
                return Fail("No payment method found.");
            }

            if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
            {
                return Fail("No subscription found.");
            }

            var plan = StaticStore.Plans.FirstOrDefault(p => p.Type == organization.PlanType);
            if (plan == null)
            {
                return Fail("Existing plan not found.");
            }

            if (!plan.HasAdditionalSeatsOption)
            {
                return Fail("Plan does not allow additional seats.");
            }

            var newSeatTotal = organization.Seats.Value + seatAdjustment;
            if (plan.BaseSeats > newSeatTotal)
            {
                return Fail($"Plan has a minimum of {plan.BaseSeats} seats.");
            }

            if (newSeatTotal <= 0)
            {
                return Fail("You must have at least 1 seat.");
            }

            var additionalSeats = newSeatTotal - plan.BaseSeats;
            if (plan.MaxAdditionalSeats.HasValue && additionalSeats > plan.MaxAdditionalSeats.Value)
            {
                return Fail($"Organization plan allows a maximum of " +
                    $"{plan.MaxAdditionalSeats.Value} additional seats.");
            }

            if (!organization.Seats.HasValue || organization.Seats.Value > newSeatTotal)
            {
                if (currentUserCount > newSeatTotal)
                {
                    return Fail($"Your organization currently has {currentUserCount} seats filled. " +
                        $"Your new plan only has ({newSeatTotal}) seats. Remove some users.");
                }
            }

            return Success;
        }

        public AccessPolicyResult CanAdjustStorage(Organization organization)
        {
            if (organization == null)
            {
                return Fail();
            }

            var plan = StaticStore.Plans.FirstOrDefault(p => p.Type == organization.PlanType);
            if (plan == null)
            {
                return Fail("Existing plan not found.");
            }

            if (!plan.HasAdditionalStorageOption)
            {
                return Fail("Plan does not allow additional storage.");
            }

            // TODO: check amount of storage currently in use?

            return Success;
        }


        public AccessPolicyResult CanUpdateSubscription(Organization organization, int seatAdjustment, int? maxAutoscaleSeats)
        {
            if (organization == null)
            {
                return Fail();
            }

            var newSeatCount = organization.Seats + seatAdjustment;
            // TODO: possible bug: newSeatCount may be null
            if (maxAutoscaleSeats.HasValue && newSeatCount > maxAutoscaleSeats.Value)
            {
                return Fail("Cannot set max seat autoscaling below seat count.");
            }

            return Success;
        }

        public async Task<AccessPolicyResult> CanUpgradePlanAsync(Organization organization, OrganizationUpgrade upgrade)
        {
            if (organization == null)
            {
                return Fail();
            }

            if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
            {
                return Fail("Your account has no payment method available.");
            }

            var existingPlan = StaticStore.Plans.FirstOrDefault(p => p.Type == organization.PlanType);
            if (existingPlan == null)
            {
                return Fail("Existing plan not found.");
            }

            var newPlan = StaticStore.Plans.FirstOrDefault(p => p.Type == upgrade.Plan && !p.Disabled);
            if (newPlan == null)
            {
                return Fail("Plan not found.");
            }

            if (existingPlan.Type == newPlan.Type)
            {
                return Fail("Organization is already on this plan.");
            }

            if (existingPlan.UpgradeSortOrder >= newPlan.UpgradeSortOrder)
            {
                return Fail("You cannot upgrade to this plan.");
            }

            if (existingPlan.Type != PlanType.Free)
            {
                return Fail("You can only upgrade from the free plan. Contact support.");
            }

            if (!string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
            {
                // TODO: Update existing sub
                return Fail("You can only upgrade from the free plan. Contact support.");
            }

            return await ValidateOrganizationUpgradeParameters(newPlan, upgrade)
                .AndAsync(() => ValidateOrganizationCompliantWithNewPlanAsync(organization, newPlan, upgrade));
        }

        public AccessPolicyResult CanUpdateAutoscaling(Organization organization, int? maxAutoscaleSeats)
        {
            if (organization == null)
            {
                return Fail();
            }

            if (maxAutoscaleSeats.HasValue &&
                    organization.Seats.HasValue &&
                    maxAutoscaleSeats.Value < organization.Seats.Value)
            {
                return Fail($"Cannot set max seat autoscaling below current seat count.");
            }

            var plan = StaticStore.Plans.FirstOrDefault(p => p.Type == organization.PlanType);
            if (plan == null)
            {
                return Fail("Existing plan not found.");
            }

            if (!plan.AllowSeatAutoscale)
            {
                return Fail("Your plan does not allow seat autoscaling.");
            }

            if (plan.MaxUsers.HasValue && maxAutoscaleSeats.HasValue &&
                maxAutoscaleSeats > plan.MaxUsers)
            {
                return Fail(string.Concat($"Your plan has a seat limit of {plan.MaxUsers}, ",
                    $"but you have specified a max autoscale count of {maxAutoscaleSeats}.",
                    "Reduce your max autoscale seat count."));
            }

            return Success;
        }

        private static AccessPolicyResult ValidateOrganizationUpgradeParameters(Plan plan, OrganizationUpgrade upgrade)
        {
            if (!plan.HasAdditionalStorageOption && upgrade.AdditionalStorageGb > 0)
            {
                return Fail("Plan does not allow additional storage.");
            }

            if (upgrade.AdditionalStorageGb < 0)
            {
                return Fail("You can't subtract storage!");
            }

            if (!plan.HasPremiumAccessOption && upgrade.PremiumAccessAddon)
            {
                return Fail("This plan does not allow you to buy the premium access addon.");
            }

            if (plan.BaseSeats + upgrade.AdditionalSeats <= 0)
            {
                return Fail("You do not have any seats!");
            }

            if (upgrade.AdditionalSeats < 0)
            {
                return Fail("You can't subtract seats!");
            }

            if (!plan.HasAdditionalSeatsOption && upgrade.AdditionalSeats > 0)
            {
                return Fail("Plan does not allow additional users.");
            }

            if (plan.HasAdditionalSeatsOption && plan.MaxAdditionalSeats.HasValue &&
                upgrade.AdditionalSeats > plan.MaxAdditionalSeats.Value)
            {
                return Fail($"Selected plan allows a maximum of " +
                    $"{plan.MaxAdditionalSeats.GetValueOrDefault(0)} additional users.");
            }

            return Success;
        }

        private async Task<AccessPolicyResult> ValidateOrganizationCompliantWithNewPlanAsync(Organization organization, Plan newPlan, OrganizationUpgrade upgrade)
        {
            var newPlanSeats = (short)(newPlan.BaseSeats +
                (newPlan.HasAdditionalSeatsOption ? upgrade.AdditionalSeats : 0));
            if (!organization.Seats.HasValue || organization.Seats.Value > newPlanSeats)
            {
                var userCount = await _organizationUserRepository.GetCountByOrganizationIdAsync(organization.Id);
                if (userCount > newPlanSeats)
                {
                    return Fail($"Your organization currently has {userCount} seats filled. " +
                        $"Your new plan only has ({newPlanSeats}) seats. Remove some users.");
                }
            }

            if (newPlan.MaxCollections.HasValue && (!organization.MaxCollections.HasValue ||
                organization.MaxCollections.Value > newPlan.MaxCollections.Value))
            {
                var collectionCount = await _collectionRepository.GetCountByOrganizationIdAsync(organization.Id);
                if (collectionCount > newPlan.MaxCollections.Value)
                {
                    return Fail($"Your organization currently has {collectionCount} collections. " +
                        $"Your new plan allows for a maximum of ({newPlan.MaxCollections.Value}) collections. " +
                        "Remove some collections.");
                }
            }

            if (!newPlan.HasGroups && organization.UseGroups)
            {
                var groups = await _groupRepository.GetManyByOrganizationIdAsync(organization.Id);
                if (groups.Any())
                {
                    return Fail($"Your new plan does not allow the groups feature. " +
                        $"Remove your groups.");
                }
            }

            if (!newPlan.HasPolicies && organization.UsePolicies)
            {
                var policies = await _policyRepository.GetManyByOrganizationIdAsync(organization.Id);
                if (policies.Any(p => p.Enabled))
                {
                    return Fail($"Your new plan does not allow the policies feature. " +
                        $"Disable your policies.");
                }
            }

            if (!newPlan.HasSso && organization.UseSso)
            {
                var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(organization.Id);
                if (ssoConfig != null && ssoConfig.Enabled)
                {
                    return Fail($"Your new plan does not allow the SSO feature. " +
                        $"Disable your SSO configuration.");
                }
            }

            if (!newPlan.HasKeyConnector && organization.UseKeyConnector)
            {
                var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(organization.Id);
                if (ssoConfig != null && ssoConfig.GetData().KeyConnectorEnabled)
                {
                    return Fail("Your new plan does not allow the Key Connector feature. " +
                                                  "Disable your Key Connector.");
                }
            }

            if (!newPlan.HasResetPassword && organization.UseResetPassword)
            {
                var resetPasswordPolicy =
                    await _policyRepository.GetByOrganizationIdTypeAsync(organization.Id, PolicyType.ResetPassword);
                if (resetPasswordPolicy != null && resetPasswordPolicy.Enabled)
                {
                    return Fail("Your new plan does not allow the Password Reset feature. " +
                        "Disable your Password Reset policy.");
                }
            }

            // TODO: Check storage?
            return Success;
        }
    }
}
