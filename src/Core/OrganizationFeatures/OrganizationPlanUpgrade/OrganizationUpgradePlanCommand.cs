using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationPlanUpgrade.Interface;
using Bit.Core.OrganizationFeatures.OrganizationSignUp;
using Bit.Core.OrganizationFeatures.OrganizationSignUp.Interfaces;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;

namespace Bit.Core.OrganizationFeatures.OrganizationPlanUpgrade;

public class OrganizationUpgradePlanCommand : IOrganizationUpgradePlanCommand
{
    private readonly IOrganizationUpgradeQuery _organizationUpgradeQuery;
    private readonly IValidateUpgradeCommand _validateUpgradeCommand;
    private readonly IOrganizationService _organizationService;
    private readonly IReferenceEventService _referenceEventService;
    private readonly ICurrentContext _currentContext;
    private readonly IPaymentService _paymentService;
    private IOrganizationSignUpValidationStrategy _organizationSignUpValidationStrategy;

    public OrganizationUpgradePlanCommand(
        IOrganizationUpgradeQuery organizationUpgradeQuery
        , IValidateUpgradeCommand validateUpgradeCommand
        , IOrganizationService organizationService
        , IReferenceEventService referenceEventService
        , ICurrentContext currentContext
        , IPaymentService paymentService
        , IOrganizationSignUpValidationStrategy organizationSignUpValidationStrategy)
    {
        _organizationUpgradeQuery = organizationUpgradeQuery;
        _validateUpgradeCommand = validateUpgradeCommand;
        _organizationService = organizationService;
        _referenceEventService = referenceEventService;
        _currentContext = currentContext;
        _paymentService = paymentService;
        _organizationSignUpValidationStrategy = organizationSignUpValidationStrategy;
    }

    public async Task<Tuple<bool, string>> UpgradePlanAsync(Guid organizationId, OrganizationUpgrade upgrade)
    {
        var organization = await _organizationUpgradeQuery.GetOrgById(organizationId);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
        {
            throw new BadRequestException("Your account has no payment method available.");
        }

        var existingPlan = _organizationUpgradeQuery.ExistingPlan(organization.PlanType);

        var newPlans = _organizationUpgradeQuery.NewPlans(upgrade.Plan);
        var passwordManagerPlan = newPlans.FirstOrDefault(x => x.BitwardenProduct == BitwardenProductType.PasswordManager);
        var secretsManagerPlan = newPlans.FirstOrDefault(x => x.BitwardenProduct == BitwardenProductType.SecretsManager);

        _validateUpgradeCommand.ValidatePlan(passwordManagerPlan, existingPlan);

        foreach (var plan in newPlans)
        {
            _organizationSignUpValidationStrategy = plan.BitwardenProduct switch
            {
                BitwardenProductType.PasswordManager => new PasswordManagerSignUpValidationStrategy(),
                _ => new SecretsManagerSignUpValidationStrategy()
            };

            _organizationSignUpValidationStrategy.Validate(plan, upgrade);
        }

        await _validateUpgradeCommand.ValidateSeatsAsync(organization, passwordManagerPlan, upgrade);
        await _validateUpgradeCommand.ValidateCollectionsAsync(organization, passwordManagerPlan);
        await _validateUpgradeCommand.ValidateGroupsAsync(organization, passwordManagerPlan);
        await _validateUpgradeCommand.ValidatePoliciesAsync(organization, passwordManagerPlan);
        await _validateUpgradeCommand.ValidateSsoAsync(organization, passwordManagerPlan);
        await _validateUpgradeCommand.ValidateKeyConnectorAsync(organization, passwordManagerPlan);
        await _validateUpgradeCommand.ValidateResetPasswordAsync(organization, passwordManagerPlan);
        await _validateUpgradeCommand.ValidateScimAsync(organization, passwordManagerPlan);
        await _validateUpgradeCommand.ValidateCustomPermissionsAsync(organization, passwordManagerPlan);
        await _validateUpgradeCommand.ValidateSmSeatsAsync(organization, secretsManagerPlan, upgrade);
        await _validateUpgradeCommand.ValidateServiceAccountAsync(organization, secretsManagerPlan, upgrade);

        // TODO: Check storage?

        string paymentIntentClientSecret;
        var success = true;
        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
        {
            paymentIntentClientSecret = await _paymentService.UpgradeFreeOrganizationAsync(organization, passwordManagerPlan,
                upgrade.AdditionalStorageGb, upgrade.AdditionalSeats, upgrade.PremiumAccessAddon, upgrade.TaxInfo);
            success = string.IsNullOrWhiteSpace(paymentIntentClientSecret);
        }
        else
        {
            // TODO: Update existing sub
            throw new BadRequestException("You can only upgrade from the free plan. Contact support.");
        }

        UpdateOrganizationProperties(organization, passwordManagerPlan, upgrade
            , success, secretsManagerPlan);

        await _organizationService.ReplaceAndUpdateCacheAsync(organization);
        if (success)
        {
            await _referenceEventService.RaiseEventAsync(
                new ReferenceEvent(ReferenceEventType.UpgradePlan, organization, _currentContext)
                {
                    PlanName = passwordManagerPlan.Name,
                    PlanType = passwordManagerPlan.Type,
                    OldPlanName = existingPlan.Name,
                    OldPlanType = existingPlan.Type,
                    Seats = organization.Seats,
                    Storage = organization.MaxStorageGb,
                    SmSeats = organization.SmSeats,
                    ServiceAccounts = organization.SmServiceAccounts
                });
        }

        return new Tuple<bool, string>(success, paymentIntentClientSecret);
    }

    private static void UpdateOrganizationProperties(Organization organization, Plan passwordManagerPlan, OrganizationUpgrade upgrade
        , bool success, Plan secretManagerPlan)
    {
        organization.BusinessName = upgrade.BusinessName;
        organization.PlanType = passwordManagerPlan.Type;
        organization.Seats = (short)(passwordManagerPlan.BaseSeats + upgrade.AdditionalSeats);
        organization.MaxCollections = passwordManagerPlan.MaxCollections;
        organization.UseGroups = passwordManagerPlan.HasGroups;
        organization.UseDirectory = passwordManagerPlan.HasDirectory;
        organization.UseEvents = passwordManagerPlan.HasEvents;
        organization.UseTotp = passwordManagerPlan.HasTotp;
        organization.Use2fa = passwordManagerPlan.Has2fa;
        organization.UseApi = passwordManagerPlan.HasApi;
        organization.SelfHost = passwordManagerPlan.HasSelfHost;
        organization.UsePolicies = passwordManagerPlan.HasPolicies;
        organization.MaxStorageGb = !passwordManagerPlan.BaseStorageGb.HasValue ?
            null : (short)(passwordManagerPlan.BaseStorageGb.Value + upgrade.AdditionalStorageGb);
        organization.UseGroups = passwordManagerPlan.HasGroups;
        organization.UseDirectory = passwordManagerPlan.HasDirectory;
        organization.UseEvents = passwordManagerPlan.HasEvents;
        organization.UseTotp = passwordManagerPlan.HasTotp;
        organization.Use2fa = passwordManagerPlan.Has2fa;
        organization.UseApi = passwordManagerPlan.HasApi;
        organization.UseSso = passwordManagerPlan.HasSso;
        organization.UseKeyConnector = passwordManagerPlan.HasKeyConnector;
        organization.UseScim = passwordManagerPlan.HasScim;
        organization.UseResetPassword = passwordManagerPlan.HasResetPassword;
        organization.SelfHost = passwordManagerPlan.HasSelfHost;
        organization.UsersGetPremium = passwordManagerPlan.UsersGetPremium || upgrade.PremiumAccessAddon;
        organization.UseCustomPermissions = passwordManagerPlan.HasCustomPermissions;
        organization.Plan = passwordManagerPlan.Name;
        organization.Enabled = success;
        organization.PublicKey = upgrade.PublicKey;
        organization.PrivateKey = upgrade.PrivateKey;
        organization.SmSeats = (short)(secretManagerPlan.BaseSeats + upgrade.AdditionalSmSeats);
        organization.SmServiceAccounts = (int)(secretManagerPlan.BaseServiceAccount + upgrade.AdditionalServiceAccount);
    }
}
