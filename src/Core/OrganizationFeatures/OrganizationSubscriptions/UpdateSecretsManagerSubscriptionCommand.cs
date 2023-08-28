using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptions;

public class UpdateSecretsManagerSubscriptionCommand : IUpdateSecretsManagerSubscriptionCommand
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPaymentService _paymentService;
    private readonly IMailService _mailService;
    private readonly ILogger<UpdateSecretsManagerSubscriptionCommand> _logger;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly IGlobalSettings _globalSettings;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IEventService _eventService;

    public UpdateSecretsManagerSubscriptionCommand(
        IOrganizationUserRepository organizationUserRepository,
        IPaymentService paymentService,
        IMailService mailService,
        ILogger<UpdateSecretsManagerSubscriptionCommand> logger,
        IServiceAccountRepository serviceAccountRepository,
        IGlobalSettings globalSettings,
        IOrganizationRepository organizationRepository,
        IApplicationCacheService applicationCacheService,
        IEventService eventService)
    {
        _organizationUserRepository = organizationUserRepository;
        _paymentService = paymentService;
        _mailService = mailService;
        _logger = logger;
        _serviceAccountRepository = serviceAccountRepository;
        _globalSettings = globalSettings;
        _organizationRepository = organizationRepository;
        _applicationCacheService = applicationCacheService;
        _eventService = eventService;
    }

    public async Task UpdateSubscriptionAsync(SecretsManagerSubscriptionUpdate update)
    {
        await ValidateUpdate(update);

        await FinalizeSubscriptionAdjustmentAsync(update.Organization, update.Plan, update);

        await SendEmailIfAutoscaleLimitReached(update.Organization);
    }

    public async Task AdjustServiceAccountsAsync(Organization organization, int smServiceAccountsAdjustment)
    {
        var update = new SecretsManagerSubscriptionUpdate(
            organization, seatAdjustment: 0, maxAutoscaleSeats: organization?.MaxAutoscaleSmSeats,
            serviceAccountAdjustment: smServiceAccountsAdjustment, maxAutoscaleServiceAccounts: organization?.MaxAutoscaleSmServiceAccounts)
        {
            Autoscaling = true
        };

        await UpdateSubscriptionAsync(update);
    }

    private async Task FinalizeSubscriptionAdjustmentAsync(Organization organization,
        Plan plan, SecretsManagerSubscriptionUpdate update)
    {
        if (update.SmSeatsChanged)
        {
            await ProcessChargesAndRaiseEventsForAdjustSeatsAsync(organization, plan, update);
            organization.SmSeats = update.SmSeats;
        }

        if (update.SmServiceAccountsChanged)
        {
            await ProcessChargesAndRaiseEventsForAdjustServiceAccountsAsync(organization, plan, update);
            organization.SmServiceAccounts = update.SmServiceAccounts;
        }

        if (update.MaxAutoscaleSmSeatsChanged)
        {
            organization.MaxAutoscaleSmSeats = update.MaxAutoscaleSmSeats;
        }

        if (update.MaxAutoscaleSmServiceAccountsChanged)
        {
            organization.MaxAutoscaleSmServiceAccounts = update.MaxAutoscaleSmServiceAccounts;
        }

        await ReplaceAndUpdateCacheAsync(organization);
    }

    private async Task ProcessChargesAndRaiseEventsForAdjustSeatsAsync(Organization organization, Plan plan,
        SecretsManagerSubscriptionUpdate update)
    {
        await _paymentService.AdjustSecretsManagerSeatsAsync(organization, plan, update.SmSeatsExcludingBase, update.ProrationDate);

        // TODO: call ReferenceEventService - see AC-1481
    }

    private async Task ProcessChargesAndRaiseEventsForAdjustServiceAccountsAsync(Organization organization, Plan plan,
        SecretsManagerSubscriptionUpdate update)
    {
        await _paymentService.AdjustServiceAccountsAsync(organization, plan,
            update.SmServiceAccountsExcludingBase, update.ProrationDate);

        // TODO: call ReferenceEventService - see AC-1481
    }

    private async Task SendEmailIfAutoscaleLimitReached(Organization organization)
    {
        if (organization.SmSeats.HasValue && organization.MaxAutoscaleSmSeats.HasValue && organization.SmSeats == organization.MaxAutoscaleSmSeats)
        {
            await SendSeatLimitEmailAsync(organization, organization.MaxAutoscaleSmSeats.Value);
        }

        if (organization.SmServiceAccounts.HasValue && organization.MaxAutoscaleSmServiceAccounts.HasValue && organization.SmServiceAccounts == organization.MaxAutoscaleSmServiceAccounts)
        {
            await SendServiceAccountLimitEmailAsync(organization, organization.MaxAutoscaleSmServiceAccounts.Value);
        }
    }

    private async Task SendSeatLimitEmailAsync(Organization organization, int MaxAutoscaleValue)
    {
        try
        {
            var ownerEmails = (await _organizationUserRepository.GetManyByMinimumRoleAsync(organization.Id,
                    OrganizationUserType.Owner))
                .Select(u => u.Email).Distinct();

            await _mailService.SendSecretsManagerMaxSeatLimitReachedEmailAsync(organization, MaxAutoscaleValue, ownerEmails);

        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Error encountered notifying organization owners of Seats limit reached.");
        }
    }

    private async Task SendServiceAccountLimitEmailAsync(Organization organization, int MaxAutoscaleValue)
    {
        try
        {
            var ownerEmails = (await _organizationUserRepository.GetManyByMinimumRoleAsync(organization.Id,
                    OrganizationUserType.Owner))
                .Select(u => u.Email).Distinct();

            await _mailService.SendSecretsManagerMaxServiceAccountLimitReachedEmailAsync(organization, MaxAutoscaleValue, ownerEmails);

        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Error encountered notifying organization owners of Service Accounts limit reached.");
        }

    }

    public async Task ValidateUpdate(SecretsManagerSubscriptionUpdate update)
    {
        if (_globalSettings.SelfHosted)
        {
            var message = update.Autoscaling
                ? "Cannot autoscale on a self-hosted instance."
                : "Cannot update subscription on a self-hosted instance.";
            throw new BadRequestException(message);
        }

        var organization = update.Organization;
        ValidateOrganization(organization);

        var plan = GetPlanForOrganization(organization);

        if (update.SmSeatsChanged)
        {
            await ValidateSmSeatsUpdateAsync(organization, update, plan);
        }

        if (update.SmServiceAccountsChanged)
        {
            await ValidateSmServiceAccountsUpdateAsync(organization, update, plan);
        }

        if (update.MaxAutoscaleSmSeatsChanged)
        {
            ValidateMaxAutoscaleSmSeatsUpdateAsync(organization, update.MaxAutoscaleSmSeats, plan);
        }

        if (update.MaxAutoscaleSmServiceAccountsChanged)
        {
            ValidateMaxAutoscaleSmServiceAccountUpdate(organization, update.MaxAutoscaleSmServiceAccounts, plan);
        }
    }

    private void ValidateOrganization(Organization organization)
    {
        if (organization == null)
        {
            throw new NotFoundException("Organization is not found.");
        }

        if (!organization.UseSecretsManager)
        {
            throw new BadRequestException("Organization has no access to Secrets Manager.");
        }

        var plan = GetPlanForOrganization(organization);
        if (plan.Product == ProductType.Free)
        {
            // No need to check the organization is set up with Stripe
            return;
        }

        if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
        {
            throw new BadRequestException("No payment method found.");
        }

        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
        {
            throw new BadRequestException("No subscription found.");
        }
    }

    private Plan GetPlanForOrganization(Organization organization)
    {
        var plan = StaticStore.Plans.FirstOrDefault(p => p.Type == organization.PlanType && p.SupportsSecretsManager);
        if (plan == null)
        {
            throw new BadRequestException("Existing plan not found.");
        }
        return plan;
    }

    private async Task ValidateSmSeatsUpdateAsync(Organization organization, SecretsManagerSubscriptionUpdate update, Plan plan)
    {
        // Check if the organization has unlimited seats
        if (organization.SmSeats == null)
        {
            throw new BadRequestException("Organization has no Secrets Manager seat limit, no need to adjust seats");
        }

        if (update.Autoscaling && update.SmSeats.Value < organization.SmSeats.Value)
        {
            throw new BadRequestException("Cannot use autoscaling to subtract seats.");
        }

        // Check plan maximum seats
        if (!plan.SecretsManager.HasAdditionalSeatsOption ||
            (plan.SecretsManager.MaxAdditionalSeats.HasValue && update.SmSeatsExcludingBase > plan.SecretsManager.MaxAdditionalSeats.Value))
        {
            var planMaxSeats = plan.SecretsManager.BaseSeats + plan.SecretsManager.MaxAdditionalSeats.GetValueOrDefault();
            throw new BadRequestException($"You have reached the maximum number of Secrets Manager seats ({planMaxSeats}) for this plan.");
        }

        // Check autoscale maximum seats
        if (update.MaxAutoscaleSmSeats.HasValue && update.SmSeats.Value > update.MaxAutoscaleSmSeats.Value)
        {
            var message = update.Autoscaling
                ? "Secrets Manager seat limit has been reached."
                : "Cannot set max seat autoscaling below seat count.";
            throw new BadRequestException(message);
        }

        // Check minimum seats included with plan
        if (plan.SecretsManager.BaseSeats > update.SmSeats.Value)
        {
            throw new BadRequestException($"Plan has a minimum of {plan.SecretsManager.BaseSeats} Secrets Manager  seats.");
        }

        // Check minimum seats required by business logic
        if (update.SmSeats.Value <= 0)
        {
            throw new BadRequestException("You must have at least 1 Secrets Manager seat.");
        }

        // Check minimum seats currently in use by the organization
        if (organization.SmSeats.Value > update.SmSeats.Value)
        {
            var currentSeats = await _organizationUserRepository.GetOccupiedSmSeatCountByOrganizationIdAsync(organization.Id);
            if (currentSeats > update.SmSeats.Value)
            {
                throw new BadRequestException($"Your organization currently has {currentSeats} Secrets Manager seats. " +
                                              $"Your plan only allows {update.SmSeats} Secrets Manager seats. Remove some Secrets Manager users.");
            }
        }
    }

    private async Task ValidateSmServiceAccountsUpdateAsync(Organization organization, SecretsManagerSubscriptionUpdate update, Plan plan)
    {
        // Check if the organization has unlimited service accounts
        if (organization.SmServiceAccounts == null)
        {
            throw new BadRequestException("Organization has no Service Accounts limit, no need to adjust Service Accounts");
        }

        if (update.Autoscaling && update.SmServiceAccounts.Value < organization.SmServiceAccounts.Value)
        {
            throw new BadRequestException("Cannot use autoscaling to subtract service accounts.");
        }

        // Check plan maximum service accounts
        if (!plan.SecretsManager.HasAdditionalServiceAccountOption ||
            (plan.SecretsManager.MaxAdditionalServiceAccount.HasValue && update.SmServiceAccountsExcludingBase > plan.SecretsManager.MaxAdditionalServiceAccount.Value))
        {
            var planMaxServiceAccounts = plan.SecretsManager.BaseServiceAccount.GetValueOrDefault() +
                                         plan.SecretsManager.MaxAdditionalServiceAccount.GetValueOrDefault();
            throw new BadRequestException($"You have reached the maximum number of service accounts ({planMaxServiceAccounts}) for this plan.");
        }

        // Check autoscale maximum service accounts
        if (update.MaxAutoscaleSmServiceAccounts.HasValue &&
            update.SmServiceAccounts.Value > update.MaxAutoscaleSmServiceAccounts.Value)
        {
            var message = update.Autoscaling
                ? "Secrets Manager service account limit has been reached."
                : "Cannot set max service accounts autoscaling below service account amount.";
            throw new BadRequestException(message);
        }

        // Check minimum service accounts included with plan
        if (plan.SecretsManager.BaseServiceAccount.HasValue && plan.SecretsManager.BaseServiceAccount.Value > update.SmServiceAccounts.Value)
        {
            throw new BadRequestException($"Plan has a minimum of {plan.SecretsManager.BaseServiceAccount} Service Accounts.");
        }

        // Check minimum service accounts required by business logic
        if (update.SmServiceAccounts.Value <= 0)
        {
            throw new BadRequestException("You must have at least 1 Service Account.");
        }

        // Check minimum service accounts currently in use by the organization
        if (!organization.SmServiceAccounts.HasValue || organization.SmServiceAccounts.Value > update.SmServiceAccounts.Value)
        {
            var currentServiceAccounts = await _serviceAccountRepository.GetServiceAccountCountByOrganizationIdAsync(organization.Id);
            if (currentServiceAccounts > update.SmServiceAccounts)
            {
                throw new BadRequestException($"Your organization currently has {currentServiceAccounts} Service Accounts. " +
                                              $"Your plan only allows {update.SmServiceAccounts} Service Accounts. Remove some Service Accounts.");
            }
        }
    }

    private void ValidateMaxAutoscaleSmSeatsUpdateAsync(Organization organization, int? maxAutoscaleSeats, Plan plan)
    {
        if (!maxAutoscaleSeats.HasValue)
        {
            // autoscale limit has been turned off, no validation required
            return;
        }

        if (organization.SmSeats.HasValue && maxAutoscaleSeats.Value < organization.SmSeats.Value)
        {
            throw new BadRequestException($"Cannot set max Secrets Manager seat autoscaling below current Secrets Manager seat count.");
        }

        if (plan.SecretsManager.MaxSeats.HasValue && maxAutoscaleSeats.Value > plan.SecretsManager.MaxSeats)
        {
            throw new BadRequestException(string.Concat(
                $"Your plan has a Secrets Manager seat limit of {plan.SecretsManager.MaxSeats}, ",
                $"but you have specified a max autoscale count of {maxAutoscaleSeats}.",
                "Reduce your max autoscale count."));
        }

        if (!plan.SecretsManager.AllowSeatAutoscale)
        {
            throw new BadRequestException("Your plan does not allow Secrets Manager seat autoscaling.");
        }
    }

    private void ValidateMaxAutoscaleSmServiceAccountUpdate(Organization organization, int? maxAutoscaleServiceAccounts, Plan plan)
    {
        if (!maxAutoscaleServiceAccounts.HasValue)
        {
            // autoscale limit has been turned off, no validation required
            return;
        }

        if (organization.SmServiceAccounts.HasValue && maxAutoscaleServiceAccounts.Value < organization.SmServiceAccounts.Value)
        {
            throw new BadRequestException(
                $"Cannot set max Service Accounts autoscaling below current Service Accounts count.");
        }

        if (!plan.SecretsManager.AllowServiceAccountsAutoscale)
        {
            throw new BadRequestException("Your plan does not allow Service Accounts autoscaling.");
        }

        if (plan.SecretsManager.MaxServiceAccounts.HasValue && maxAutoscaleServiceAccounts.Value > plan.SecretsManager.MaxServiceAccounts)
        {
            throw new BadRequestException(string.Concat(
                $"Your plan has a Service Accounts limit of {plan.SecretsManager.MaxServiceAccounts}, ",
                $"but you have specified a max autoscale count of {maxAutoscaleServiceAccounts}.",
                "Reduce your max autoscale count."));
        }
    }

    // TODO: This is a temporary duplication of OrganizationService.ReplaceAndUpdateCache to avoid a circular dependency.
    // TODO: This should no longer be necessary when user-related methods are extracted from OrganizationService: see PM-1880
    private async Task ReplaceAndUpdateCacheAsync(Organization org, EventType? orgEvent = null)
    {
        await _organizationRepository.ReplaceAsync(org);
        await _applicationCacheService.UpsertOrganizationAbilityAsync(org);

        if (orgEvent.HasValue)
        {
            await _eventService.LogOrganizationEventAsync(org, orgEvent.Value);
        }
    }
}
