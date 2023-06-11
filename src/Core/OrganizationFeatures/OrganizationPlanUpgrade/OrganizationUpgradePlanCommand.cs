using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationPlanUpgrade.Interface;
using Bit.Core.OrganizationFeatures.OrganizationSignUp;
using Bit.Core.OrganizationFeatures.OrganizationSignUp.Interfaces;
using Bit.Core.Services;
using Bit.Core.Services.Implementations.UpgradeOrganizationPlan.Commands;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;

namespace Bit.Core.OrganizationFeatures.OrganizationPlanUpgrade;

public class UpgradePlanCommand
{
    private readonly IOrganizationUpgradeQuery _organizationUpgradeQuery;
    private readonly IValidateUpgradeCommand _validateUpgradeCommand;
    private readonly IOrganizationService _organizationService;
    private readonly IReferenceEventService _referenceEventService;
    private readonly ICurrentContext _currentContext;
    private readonly IFeatureService _featureService;
    private readonly IPaymentService _paymentService;
    private IOrganizationSignUpValidationStrategy _organizationSignUpValidationStrategy;
    
    public UpgradePlanCommand(
        IOrganizationUpgradeQuery organizationUpgradeQuery
        ,IValidateUpgradeCommand validateUpgradeCommand
        ,IOrganizationService organizationService
        ,IReferenceEventService referenceEventService
        ,ICurrentContext currentContext
        ,IFeatureService featureService
        ,IPaymentService paymentService
        ,IOrganizationSignUpValidationStrategy organizationSignUpValidationStrategy)
    {
        _organizationUpgradeQuery = organizationUpgradeQuery;
        _validateUpgradeCommand = validateUpgradeCommand;
        _organizationService = organizationService;
        _referenceEventService = referenceEventService;
        _currentContext = currentContext;
        _featureService = featureService;
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
        var secretsManagerPlan =  newPlans.FirstOrDefault(x => x.BitwardenProduct == BitwardenProductType.SecretsManager);
        
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
        
        await _validateUpgradeCommand.ValidateSeatsAsync(organization, passwordManagerPlan,upgrade);
        await _validateUpgradeCommand.ValidateCollectionsAsync(organization, passwordManagerPlan);
        await _validateUpgradeCommand.ValidateGroupsAsync(organization, passwordManagerPlan);
        await _validateUpgradeCommand.ValidatePoliciesAsync(organization, passwordManagerPlan);
        await _validateUpgradeCommand.ValidateSsoAsync(organization, passwordManagerPlan);
        await _validateUpgradeCommand.ValidateKeyConnectorAsync(organization, passwordManagerPlan);
        await _validateUpgradeCommand.ValidateResetPasswordAsync(organization, passwordManagerPlan);
        await _validateUpgradeCommand.ValidateScimAsync(organization, passwordManagerPlan);
        await _validateUpgradeCommand.ValidateCustomPermissionsAsync(organization, passwordManagerPlan);
        if (_featureService.IsEnabled(FeatureFlagKeys.SecretManagerGaBilling, _currentContext) &&
            organization.UseSecretsManager)
        {
            await _validateUpgradeCommand.ValidateSmSeatsAsync(organization, secretsManagerPlan,upgrade);
            await _validateUpgradeCommand.ValidateServiceAccountAsync(organization, secretsManagerPlan,upgrade);
        }
        
        // TODO: Check storage?

        string paymentIntentClientSecret = null;
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
        
        UpdateOrganizationPropertiesCommand.UpdateOrganizationProperties(organization, passwordManagerPlan, upgrade, success,secretsManagerPlan);
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
}
