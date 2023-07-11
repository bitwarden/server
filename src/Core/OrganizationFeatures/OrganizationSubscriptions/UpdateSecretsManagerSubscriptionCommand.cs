#nullable enable
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptions;

public class UpdateSecretsManagerSubscriptionCommand : IUpdateSecretsManagerSubscriptionCommand
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPaymentService _paymentService;
    private readonly IOrganizationService _organizationService;
    private readonly IMailService _mailService;
    private readonly ILogger<UpdateSecretsManagerSubscriptionCommand> _logger;
    private readonly IServiceAccountRepository _serviceAccountRepository;

    public UpdateSecretsManagerSubscriptionCommand(
        IOrganizationRepository organizationRepository,
        IOrganizationService organizationService,
        IOrganizationUserRepository organizationUserRepository,
        IPaymentService paymentService,
        IMailService mailService,
        ILogger<UpdateSecretsManagerSubscriptionCommand> logger,
        IServiceAccountRepository serviceAccountRepository)
    {
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _paymentService = paymentService;
        _organizationService = organizationService;
        _mailService = mailService;
        _logger = logger;
        _serviceAccountRepository = serviceAccountRepository;
    }

    public async Task UpdateSecretsManagerSubscription(SecretsManagerSubscriptionUpdate update)
    {
        var organization = await _organizationRepository.GetByIdAsync(update.OrganizationId);

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

        await FinalizeSubscriptionAdjustmentAsync(organization, plan, update);

        await SendEmailIfAutoscaleLimitReached(organization);
    }

    private Plan GetPlanForOrganization(Organization organization)
    {
        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(p => p.Type == organization.PlanType);
        if (plan == null)
        {
            throw new BadRequestException("Existing plan not found.");
        }
        return plan;
    }

    private static void ValidateOrganization(Organization organization)
    {
        if (organization == null)
        {
            throw new NotFoundException("Organization is not found");
        }

        if (!organization.UseSecretsManager)
        {
            throw new BadRequestException("Organization has no access to Secrets Manager.");
        }
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

        await _organizationService.ReplaceAndUpdateCacheAsync(organization);
    }

    private async Task ProcessChargesAndRaiseEventsForAdjustSeatsAsync(Organization organization, Plan plan,
        SecretsManagerSubscriptionUpdate update)
    {
        await _paymentService.AdjustSeatsAsync(organization, plan, update.SmSeatsExcludingBase);

        // TODO: call ReferenceEventService - see AC-1481
    }

    private async Task ProcessChargesAndRaiseEventsForAdjustServiceAccountsAsync(Organization organization, Plan plan,
        SecretsManagerSubscriptionUpdate update)
    {
        await _paymentService.AdjustServiceAccountsAsync(organization, plan,
            update.SmServiceAccountsExcludingBase);

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

    private async Task ValidateSmSeatsUpdateAsync(Organization organization, SecretsManagerSubscriptionUpdate update, Plan plan)
    {
        if (organization.SmSeats == null)
        {
            throw new BadRequestException("Organization has no Secrets Manager seat limit, no need to adjust seats");
        }

        if (update.MaxAutoscaleSmSeats.HasValue && update.SmSeats > update.MaxAutoscaleSmSeats.Value)
        {
            throw new BadRequestException("Cannot set max seat autoscaling below seat count.");
        }

        if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
        {
            throw new BadRequestException("No payment method found.");
        }

        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
        {
            throw new BadRequestException("No subscription found.");
        }

        if (!plan.HasAdditionalSeatsOption)
        {
            throw new BadRequestException("Plan does not allow additional Secrets Manager seats.");
        }

        if (plan.BaseSeats > update.SmSeats)
        {
            throw new BadRequestException($"Plan has a minimum of {plan.BaseSeats} Secrets Manager  seats.");
        }

        if (update.SmSeats <= 0)
        {
            throw new BadRequestException("You must have at least 1 Secrets Manager seat.");
        }

        if (plan.MaxAdditionalSeats.HasValue && update.SmSeatsExcludingBase > plan.MaxAdditionalSeats.Value)
        {
            throw new BadRequestException($"Organization plan allows a maximum of " +
                                          $"{plan.MaxAdditionalSeats.Value} additional Secrets Manager seats.");
        }

        if (organization.SmSeats.Value > update.SmSeats)
        {
            var currentSeats = await _organizationUserRepository.GetOccupiedSmSeatCountByOrganizationIdAsync(organization.Id);
            if (currentSeats > update.SmSeats)
            {
                throw new BadRequestException($"Your organization currently has {currentSeats} Secrets Manager seats. " +
                                              $"Your plan only allows {update.SmSeats} Secrets Manager seats. Remove some Secrets Manager users.");
            }
        }
    }

    private async Task ValidateSmServiceAccountsUpdateAsync(Organization organization, SecretsManagerSubscriptionUpdate update, Plan plan)
    {
        if (organization.SmServiceAccounts == null)
        {
            throw new BadRequestException("Organization has no Service Accounts limit, no need to adjust Service Accounts");
        }

        if (update.MaxAutoscaleSmServiceAccounts.HasValue && update.SmServiceAccounts > update.MaxAutoscaleSmServiceAccounts.Value)
        {
            throw new BadRequestException("Cannot set max Service Accounts autoscaling below Service Accounts count.");
        }

        if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
        {
            throw new BadRequestException("No payment method found.");
        }

        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
        {
            throw new BadRequestException("No subscription found.");
        }

        if (!plan.HasAdditionalServiceAccountOption)
        {
            throw new BadRequestException("Plan does not allow additional Service Accounts.");
        }

        if (plan.BaseServiceAccount > update.SmServiceAccounts)
        {
            throw new BadRequestException($"Plan has a minimum of {plan.BaseServiceAccount} Service Accounts.");
        }

        if (update.SmServiceAccounts <= 0)
        {
            throw new BadRequestException("You must have at least 1 Service Account.");
        }

        if (plan.MaxAdditionalServiceAccount.HasValue && update.SmServiceAccountsExcludingBase > plan.MaxAdditionalServiceAccount.Value)
        {
            throw new BadRequestException($"Organization plan allows a maximum of " +
                                          $"{plan.MaxAdditionalServiceAccount.Value} additional Service Accounts.");
        }

        if (!organization.SmServiceAccounts.HasValue || organization.SmServiceAccounts.Value > update.SmServiceAccounts)
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

        if (plan.MaxUsers.HasValue && maxAutoscaleSeats.Value > plan.MaxUsers)
        {
            throw new BadRequestException(string.Concat(
                $"Your plan has a Secrets Manager seat limit of {plan.MaxUsers}, ",
                $"but you have specified a max autoscale count of {maxAutoscaleSeats}.",
                "Reduce your max autoscale count."));
        }

        if (!plan.AllowSeatAutoscale)
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

        if (!plan.AllowServiceAccountsAutoscale)
        {
            throw new BadRequestException("Your plan does not allow Service Accounts autoscaling.");
        }

        if (plan.MaxServiceAccounts.HasValue && maxAutoscaleServiceAccounts.Value > plan.MaxServiceAccounts)
        {
            throw new BadRequestException(string.Concat(
                $"Your plan has a Service Accounts limit of {plan.MaxServiceAccounts}, ",
                $"but you have specified a max autoscale count of {maxAutoscaleServiceAccounts}.",
                "Reduce your max autoscale count."));
        }
    }
}
