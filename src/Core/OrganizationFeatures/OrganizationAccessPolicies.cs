using System;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.AccessPolicies;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures
{
    public class OrganizationAccessPolicies : BaseAccessPolicies, IOrganizationAccessPolicies
    {
        private readonly ILicensingService _licensingService;
        private readonly ICollectionRepository _collectionRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly ISsoConfigRepository _ssoConfigRepository;
        private readonly IPolicyRepository _policyRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IGlobalSettings _globalSettings;

        public OrganizationAccessPolicies(
            IPolicyRepository policyRepository,
            IOrganizationUserRepository organizationUserRepository,
            IGlobalSettings globalSettings,
            ILicensingService licensingService,
            IOrganizationRepository organizationRepository,
            ICollectionRepository collectionRepository,
            IGroupRepository groupRepository,
            ISsoConfigRepository ssoConfigRepository)
        {
            _policyRepository = policyRepository;
            _organizationUserRepository = organizationUserRepository;
            _globalSettings = globalSettings;
            _licensingService = licensingService;
            _organizationRepository = organizationRepository;
            _collectionRepository = collectionRepository;
            _groupRepository = groupRepository;
            _ssoConfigRepository = ssoConfigRepository;
        }

        public AccessPolicyResult CanReplacePaymentMethod(Organization organization)
        {
            return organization == null ? Fail() : Success;
        }

        public AccessPolicyResult CanVerifyBank(Organization organization)
        {
            if (organization == null)
            {
                return Fail();
            }

            if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
            {
                return Fail("Not a gateway customer.");
            }

            return Success;
        }

        public async Task<AccessPolicyResult> CanSignUp(OrganizationSignup signup, Plan plan, bool provider)
        {
            if (!(plan is { LegacyYear: null }))
            {
                return Fail("Invalid plan selected.");
            }

            if (plan.Disabled)
            {
                return Fail("Plan not found.");
            }

            var currentResult = Success;
            if (!provider)
            {
                currentResult = currentResult.LazyAnd(await ValidateSignUpPoliciesAsync(signup.Owner.Id));
            }

            return await currentResult
                .LazyAnd(ValidateOrganizationUpgradeParameters(plan, signup))
                .AndAsync(() => CanMakeFreeOrgAsync(plan, signup));
        }

        public async Task<AccessPolicyResult> CanSelfHostedSignUpAsync(OrganizationLicense license, User owner)
        {
            if (license == null || !_licensingService.VerifyLicense(license))
            {
                return Fail("Invalid license.");
            }

            if (!license.CanUse(_globalSettings))
            {
                return Fail("Invalid license. Make sure your license allows for on-premise " +
                            "hosting of organizations and that the installation id matches your current installation.");
            }

            if (license.PlanType != PlanType.Custom &&
                StaticStore.Plans.FirstOrDefault(p => p.Type == license.PlanType && !p.Disabled) == null)
            {
                return Fail("Plan not found.");
            }

            var enabledOrgs = await _organizationRepository.GetManyByEnabledAsync();
            if (enabledOrgs.Any(o => o.LicenseKey.Equals(license.LicenseKey)))
            {
                return Fail("License is already in use by another organization.");
            }

            return await ValidateSignUpPoliciesAsync(owner.Id);
        }

        public async Task<AccessPolicyResult> CanUpdateLicenseAsync(Organization organization,
            OrganizationLicense license)
        {
            if (organization == null)
            {
                throw new NotFoundException();
            }

            if (license == null || !_licensingService.VerifyLicense(license))
            {
                return Fail("Invalid license.");
            }

            if (!license.CanUse(_globalSettings))
            {
                return Fail("Invalid license. Make sure your license allows for on-premise " +
                            "hosting of organizations and that the installation id matches your current installation.");
            }

            var enabledOrgs = await _organizationRepository.GetManyByEnabledAsync();
            if (enabledOrgs.Any(o => o.LicenseKey.Equals(license.LicenseKey) && o.Id != organization.Id))
            {
                return Fail("License is already in use by another organization.");
            }

            if (license.Seats.HasValue &&
                (!organization.Seats.HasValue || organization.Seats.Value > license.Seats.Value))
            {
                var userCount = await _organizationUserRepository.GetCountByOrganizationIdAsync(organization.Id);
                if (userCount > license.Seats.Value)
                {
                    return Fail($"Your organization currently has {userCount} seats filled. " +
                                $"Your new license only has ({license.Seats.Value}) seats. Remove some users.");
                }
            }

            if (license.MaxCollections.HasValue && (!organization.MaxCollections.HasValue ||
                                                    organization.MaxCollections.Value > license.MaxCollections.Value))
            {
                var collectionCount = await _collectionRepository.GetCountByOrganizationIdAsync(organization.Id);
                if (collectionCount > license.MaxCollections.Value)
                {
                    return Fail($"Your organization currently has {collectionCount} collections. " +
                                $"Your new license allows for a maximum of ({license.MaxCollections.Value}) collections. " +
                                "Remove some collections.");
                }
            }

            if (!license.UseGroups && organization.UseGroups)
            {
                var groups = await _groupRepository.GetManyByOrganizationIdAsync(organization.Id);
                if (groups.Count > 0)
                {
                    return Fail($"Your organization currently has {groups.Count} groups. " +
                                $"Your new license does not allow for the use of groups. Remove all groups.");
                }
            }

            if (!license.UsePolicies && organization.UsePolicies)
            {
                var policies = await _policyRepository.GetManyByOrganizationIdAsync(organization.Id);
                if (policies.Any(p => p.Enabled))
                {
                    return Fail($"Your organization currently has {policies.Count} enabled " +
                                $"policies. Your new license does not allow for the use of policies. Disable all policies.");
                }
            }

            if (!license.UseSso && organization.UseSso)
            {
                var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(organization.Id);
                if (ssoConfig != null && ssoConfig.Enabled)
                {
                    return Fail($"Your organization currently has a SSO configuration. " +
                                $"Your new license does not allow for the use of SSO. Disable your SSO configuration.");
                }
            }

            if (!license.UseKeyConnector && organization.UseKeyConnector)
            {
                var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(organization.Id);
                if (ssoConfig != null && ssoConfig.GetData().KeyConnectorEnabled)
                {
                    return Fail($"Your organization currently has Key Connector enabled. " +
                                $"Your new license does not allow for the use of Key Connector. Disable your Key Connector.");
                }
            }

            if (!license.UseResetPassword && organization.UseResetPassword)
            {
                var resetPasswordPolicy =
                    await _policyRepository.GetByOrganizationIdTypeAsync(organization.Id, PolicyType.ResetPassword);
                if (resetPasswordPolicy != null && resetPasswordPolicy.Enabled)
                {
                    return Fail("Your new license does not allow the Password Reset feature. "
                                + "Disable your Password Reset policy.");
                }
            }

            return Success;
        }

        private async Task<AccessPolicyResult> CanMakeFreeOrgAsync(Plan plan, OrganizationSignup signup)
        {
            if (plan.Type == PlanType.Free)
            {
                if (await _organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(signup.Owner.Id) > 0)
                {
                    return Fail("You can only be an admin of one free organization.");
                }
            }

            return Success;
        }

        private async Task<AccessPolicyResult> ValidateSignUpPoliciesAsync(Guid ownerId)
        {
            var singleOrgPolicyCount =
                await _policyRepository.GetCountByTypeApplicableToUserIdAsync(ownerId, PolicyType.SingleOrg);
            if (singleOrgPolicyCount > 0)
            {
                return Fail(string.Concat("You may not create an organization. You belong to an organization",
                    "which has a policy that prohibits you from being a member of any other organization."));
            }

            return Success;
        }

        private AccessPolicyResult ValidateOrganizationUpgradeParameters(Plan plan, OrganizationUpgrade upgrade)
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
                return Fail(string.Concat("Selected plan allows a maximum of",
                    $"{plan.MaxAdditionalSeats.GetValueOrDefault(0)} additional users."));
            }

            return Success;
        }
    }
}
