using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.OrganizationConnectionConfigs;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models.Sales;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptions;

public class UpgradeOrganizationPlanCommand : IUpgradeOrganizationPlanCommand
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IPaymentService _paymentService;
    private readonly IPolicyRepository _policyRepository;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly IReferenceEventService _referenceEventService;
    private readonly IOrganizationConnectionRepository _organizationConnectionRepository;
    private readonly ICurrentContext _currentContext;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IFeatureService _featureService;
    private readonly IOrganizationBillingService _organizationBillingService;

    public UpgradeOrganizationPlanCommand(
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository,
        IGroupRepository groupRepository,
        IPaymentService paymentService,
        IPolicyRepository policyRepository,
        ISsoConfigRepository ssoConfigRepository,
        IReferenceEventService referenceEventService,
        IOrganizationConnectionRepository organizationConnectionRepository,
        ICurrentContext currentContext,
        IServiceAccountRepository serviceAccountRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationService organizationService,
        IFeatureService featureService,
        IOrganizationBillingService organizationBillingService)
    {
        _organizationUserRepository = organizationUserRepository;
        _collectionRepository = collectionRepository;
        _groupRepository = groupRepository;
        _paymentService = paymentService;
        _policyRepository = policyRepository;
        _ssoConfigRepository = ssoConfigRepository;
        _referenceEventService = referenceEventService;
        _organizationConnectionRepository = organizationConnectionRepository;
        _currentContext = currentContext;
        _serviceAccountRepository = serviceAccountRepository;
        _organizationRepository = organizationRepository;
        _organizationService = organizationService;
        _featureService = featureService;
        _organizationBillingService = organizationBillingService;
    }

    public async Task<Tuple<bool, string>> UpgradePlanAsync(Guid organizationId, OrganizationUpgrade upgrade)
    {
        var organization = await GetOrgById(organizationId);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
        {
            throw new BadRequestException("Your account has no payment method available.");
        }

        var existingPlan = StaticStore.GetPlan(organization.PlanType);
        if (existingPlan == null)
        {
            throw new BadRequestException("Existing plan not found.");
        }

        var newPlan = StaticStore.Plans.FirstOrDefault(p => p.Type == upgrade.Plan && !p.Disabled);
        if (newPlan == null)
        {
            throw new BadRequestException("Plan not found.");
        }

        if (existingPlan.Type == newPlan.Type)
        {
            throw new BadRequestException("Organization is already on this plan.");
        }

        if (existingPlan.UpgradeSortOrder >= newPlan.UpgradeSortOrder)
        {
            throw new BadRequestException("You cannot upgrade to this plan.");
        }

        _organizationService.ValidatePasswordManagerPlan(newPlan, upgrade);

        if (upgrade.UseSecretsManager)
        {
            _organizationService.ValidateSecretsManagerPlan(newPlan, upgrade);
        }

        var updatedPasswordManagerSeats = (short)(newPlan.PasswordManager.BaseSeats +
                                                  (newPlan.PasswordManager.HasAdditionalSeatsOption ? upgrade.AdditionalSeats : 0));
        if (!organization.Seats.HasValue || organization.Seats.Value > updatedPasswordManagerSeats)
        {
            var occupiedSeats =
                await _organizationUserRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id);
            if (occupiedSeats > updatedPasswordManagerSeats)
            {
                throw new BadRequestException($"Your organization currently has {occupiedSeats} seats filled. " +
                                              $"Your new plan only has ({updatedPasswordManagerSeats}) seats. Remove some users.");
            }
        }

        if (newPlan.PasswordManager.MaxCollections.HasValue && (!organization.MaxCollections.HasValue ||
                                                               organization.MaxCollections.Value >
                                                               newPlan.PasswordManager.MaxCollections.Value))
        {
            var collectionCount = await _collectionRepository.GetCountByOrganizationIdAsync(organization.Id);
            if (collectionCount > newPlan.PasswordManager.MaxCollections.Value)
            {
                throw new BadRequestException($"Your organization currently has {collectionCount} collections. " +
                                              $"Your new plan allows for a maximum of ({newPlan.PasswordManager.MaxCollections.Value}) collections. " +
                                              "Remove some collections.");
            }
        }

        if (!newPlan.HasGroups && organization.UseGroups)
        {
            var groups = await _groupRepository.GetManyByOrganizationIdAsync(organization.Id);
            if (groups.Any())
            {
                throw new BadRequestException($"Your new plan does not allow the groups feature. " +
                                              $"Remove your groups.");
            }
        }

        if (!newPlan.HasPolicies && organization.UsePolicies)
        {
            var policies = await _policyRepository.GetManyByOrganizationIdAsync(organization.Id);
            if (policies.Any(p => p.Enabled))
            {
                throw new BadRequestException($"Your new plan does not allow the policies feature. " +
                                              $"Disable your policies.");
            }
        }

        if (!newPlan.HasSso && organization.UseSso)
        {
            var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(organization.Id);
            if (ssoConfig != null && ssoConfig.Enabled)
            {
                throw new BadRequestException($"Your new plan does not allow the SSO feature. " +
                                              $"Disable your SSO configuration.");
            }
        }

        if (!newPlan.HasKeyConnector && organization.UseKeyConnector)
        {
            var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(organization.Id);
            if (ssoConfig != null && ssoConfig.GetData().MemberDecryptionType == MemberDecryptionType.KeyConnector)
            {
                throw new BadRequestException("Your new plan does not allow the Key Connector feature. " +
                                              "Disable your Key Connector.");
            }
        }

        if (!newPlan.HasResetPassword && organization.UseResetPassword)
        {
            var resetPasswordPolicy =
                await _policyRepository.GetByOrganizationIdTypeAsync(organization.Id, PolicyType.ResetPassword);
            if (resetPasswordPolicy != null && resetPasswordPolicy.Enabled)
            {
                throw new BadRequestException("Your new plan does not allow the Password Reset feature. " +
                                              "Disable your Password Reset policy.");
            }
        }

        if (!newPlan.HasScim && organization.UseScim)
        {
            var scimConnections = await _organizationConnectionRepository.GetByOrganizationIdTypeAsync(organization.Id,
                OrganizationConnectionType.Scim);
            if (scimConnections != null && scimConnections.Any(c => c.GetConfig<ScimConfig>()?.Enabled == true))
            {
                throw new BadRequestException("Your new plan does not allow the SCIM feature. " +
                                              "Disable your SCIM configuration.");
            }
        }

        if (!newPlan.HasCustomPermissions && organization.UseCustomPermissions)
        {
            var organizationCustomUsers =
                await _organizationUserRepository.GetManyByOrganizationAsync(organization.Id,
                    OrganizationUserType.Custom);
            if (organizationCustomUsers.Any())
            {
                throw new BadRequestException("Your new plan does not allow the Custom Permissions feature. " +
                                              "Disable your Custom Permissions configuration.");
            }
        }

        if (upgrade.UseSecretsManager)
        {
            await ValidateSecretsManagerSeatsAndServiceAccountAsync(upgrade, organization, newPlan);
        }

        // TODO: Check storage?
        string paymentIntentClientSecret = null;
        var success = true;

        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
        {
            if (_featureService.IsEnabled(FeatureFlagKeys.AC2476_DeprecateStripeSourcesAPI))
            {
                var sale = OrganizationSale.From(organization, upgrade);
                await _organizationBillingService.Finalize(sale);
            }
            else
            {
                try
                {
                    paymentIntentClientSecret = await _paymentService.UpgradeFreeOrganizationAsync(organization,
                        newPlan, upgrade);
                    success = string.IsNullOrWhiteSpace(paymentIntentClientSecret);
                }
                catch
                {
                    await _paymentService.CancelAndRecoverChargesAsync(organization);
                    organization.GatewayCustomerId = null;
                    await _organizationService.ReplaceAndUpdateCacheAsync(organization);
                    throw;
                }
            }
        }
        else
        {
            paymentIntentClientSecret = await _paymentService.AdjustSubscription(
                organization,
                newPlan,
                upgrade.AdditionalSeats,
                upgrade.UseSecretsManager,
                upgrade.AdditionalSmSeats,
                upgrade.AdditionalServiceAccounts,
                upgrade.AdditionalStorageGb);

            success = string.IsNullOrEmpty(paymentIntentClientSecret);
        }

        organization.BusinessName = upgrade.BusinessName;
        organization.PlanType = newPlan.Type;
        organization.Seats = (short)(newPlan.PasswordManager.BaseSeats + upgrade.AdditionalSeats);
        organization.MaxCollections = newPlan.PasswordManager.MaxCollections;
        organization.UseGroups = newPlan.HasGroups;
        organization.UseDirectory = newPlan.HasDirectory;
        organization.UseEvents = newPlan.HasEvents;
        organization.UseTotp = newPlan.HasTotp;
        organization.Use2fa = newPlan.Has2fa;
        organization.UseApi = newPlan.HasApi;
        organization.SelfHost = newPlan.HasSelfHost;
        organization.UsePolicies = newPlan.HasPolicies;
        organization.MaxStorageGb = !newPlan.PasswordManager.BaseStorageGb.HasValue
            ? (short?)null
            : (short)(newPlan.PasswordManager.BaseStorageGb.Value + upgrade.AdditionalStorageGb);
        organization.UseGroups = newPlan.HasGroups;
        organization.UseDirectory = newPlan.HasDirectory;
        organization.UseEvents = newPlan.HasEvents;
        organization.UseTotp = newPlan.HasTotp;
        organization.Use2fa = newPlan.Has2fa;
        organization.UseApi = newPlan.HasApi;
        organization.UseSso = newPlan.HasSso;
        organization.UseKeyConnector = newPlan.HasKeyConnector;
        organization.UseScim = newPlan.HasScim;
        organization.UseResetPassword = newPlan.HasResetPassword;
        organization.SelfHost = newPlan.HasSelfHost;
        organization.UsersGetPremium = newPlan.UsersGetPremium || upgrade.PremiumAccessAddon;
        organization.UseCustomPermissions = newPlan.HasCustomPermissions;
        organization.Plan = newPlan.Name;
        organization.Enabled = success;
        organization.PublicKey = upgrade.PublicKey;
        organization.PrivateKey = upgrade.PrivateKey;
        organization.UsePasswordManager = true;
        organization.UseSecretsManager = upgrade.UseSecretsManager;

        if (upgrade.UseSecretsManager)
        {
            organization.SmSeats = newPlan.SecretsManager.BaseSeats + upgrade.AdditionalSmSeats.GetValueOrDefault();
            organization.SmServiceAccounts = newPlan.SecretsManager.BaseServiceAccount +
                                             upgrade.AdditionalServiceAccounts.GetValueOrDefault();
        }

        await _organizationService.ReplaceAndUpdateCacheAsync(organization);

        if (success)
        {
            var upgradePath = GetUpgradePath(existingPlan.ProductTier, newPlan.ProductTier);
            await _referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.UpgradePlan, organization, _currentContext)
                {
                    PlanName = newPlan.Name,
                    PlanType = newPlan.Type,
                    OldPlanName = existingPlan.Name,
                    OldPlanType = existingPlan.Type,
                    Seats = organization.Seats,
                    SignupInitiationPath = "Upgrade in-product",
                    PlanUpgradePath = upgradePath,
                    Storage = organization.MaxStorageGb,
                    // TODO: add reference events for SmSeats and Service Accounts - see AC-1481
                });
        }

        return new Tuple<bool, string>(success, paymentIntentClientSecret);
    }

    private async Task ValidateSecretsManagerSeatsAndServiceAccountAsync(OrganizationUpgrade upgrade, Organization organization,
        Models.StaticStore.Plan newSecretsManagerPlan)
    {
        var newPlanSmSeats = (short)(newSecretsManagerPlan.SecretsManager.BaseSeats +
                                     (newSecretsManagerPlan.SecretsManager.HasAdditionalSeatsOption
                                         ? upgrade.AdditionalSmSeats
                                         : 0));
        var occupiedSmSeats =
            await _organizationUserRepository.GetOccupiedSmSeatCountByOrganizationIdAsync(organization.Id);

        if (!organization.SmSeats.HasValue || organization.SmSeats.Value > newPlanSmSeats)
        {
            if (occupiedSmSeats > newPlanSmSeats)
            {
                throw new BadRequestException(
                    $"Your organization currently has {occupiedSmSeats} Secrets Manager seats filled. " +
                    $"Your new plan only has {newPlanSmSeats} seats. Remove some users or increase your subscription.");
            }
        }

        var additionalServiceAccounts = newSecretsManagerPlan.SecretsManager.HasAdditionalServiceAccountOption
            ? upgrade.AdditionalServiceAccounts
            : 0;
        var newPlanServiceAccounts = newSecretsManagerPlan.SecretsManager.BaseServiceAccount + additionalServiceAccounts;

        if (!organization.SmServiceAccounts.HasValue || organization.SmServiceAccounts.Value > newPlanServiceAccounts)
        {
            var currentServiceAccounts =
                await _serviceAccountRepository.GetServiceAccountCountByOrganizationIdAsync(organization.Id);
            if (currentServiceAccounts > newPlanServiceAccounts)
            {
                throw new BadRequestException(
                    $"Your organization currently has {currentServiceAccounts} machine accounts. " +
                    $"Your new plan only allows {newSecretsManagerPlan.SecretsManager.MaxServiceAccounts} machine accounts. " +
                    "Remove some machine accounts or increase your subscription.");
            }
        }
    }

    private async Task<Organization> GetOrgById(Guid id)
    {
        return await _organizationRepository.GetByIdAsync(id);
    }

    private static string GetUpgradePath(ProductTierType oldProductTierType, ProductTierType newProductTierType)
    {
        var oldDescription = _upgradePath.TryGetValue(oldProductTierType, out var description)
            ? description
            : $"{oldProductTierType:G}";

        var newDescription = _upgradePath.TryGetValue(newProductTierType, out description)
            ? description
            : $"{newProductTierType:G}";

        return $"{oldDescription} → {newDescription}";
    }

    private static readonly Dictionary<ProductTierType, string> _upgradePath = new()
    {
        [ProductTierType.Free] = "2-person org",
        [ProductTierType.Families] = "Families",
        [ProductTierType.TeamsStarter] = "Teams Starter",
        [ProductTierType.Teams] = "Teams",
        [ProductTierType.Enterprise] = "Enterprise"
    };
}
