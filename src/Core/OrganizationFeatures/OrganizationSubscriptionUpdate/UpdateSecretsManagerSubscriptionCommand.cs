#nullable enable
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate.Interface;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate;

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

        if (update.SeatAdjustment != 0)
        {
            await AdjustSeatsAsync(organization, update, plan);
        }

        if (update.ServiceAccountsAdjustment != 0)
        {
            await AdjustServiceAccountsAsync(organization, update, plan);
        }

        if (update.MaxAutoscaleSeats.HasValue && update.MaxAutoscaleSeats != organization.MaxAutoscaleSmSeats.GetValueOrDefault())
        {
            UpdateSeatsAutoscaling(organization, update.MaxAutoscaleSeats.Value, plan);
        }

        if (update.MaxAutoscaleServiceAccounts.HasValue && update.MaxAutoscaleServiceAccounts != organization.MaxAutoscaleSmServiceAccounts.GetValueOrDefault())
        {
            UpdateServiceAccountAutoscaling(organization, update.MaxAutoscaleServiceAccounts.Value, plan);
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
        if (update.AdjustingSeats)
        {
            await ProcessChargesAndRaiseEventsForAdjustSeatsAsync(organization, plan, update);
        }

        if (update.AdjustingServiceAccounts)
        {
            await ProcessChargesAndRaiseEventsForAdjustServiceAccountsAsync(organization, plan, update);
        }

        await _organizationService.ReplaceAndUpdateCacheAsync(organization);
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

    private async Task AdjustSeatsAsync(Organization organization, SecretsManagerSubscriptionUpdate update, Plan plan)
    {
        if (organization.SmSeats == null)
        {
            throw new BadRequestException("Organization has no Secrets Manager seat limit, no need to adjust seats");
        }

        if (update.MaxAutoscaleSeats.HasValue && update.NewTotalSeats > update.MaxAutoscaleSeats.Value)
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

        if (plan.BaseSeats > update.NewTotalSeats)
        {
            throw new BadRequestException($"Plan has a minimum of {plan.BaseSeats} Secrets Manager  seats.");
        }

        if (update.NewTotalSeats <= 0)
        {
            throw new BadRequestException("You must have at least 1 Secrets Manager seat.");
        }

        if (plan.MaxAdditionalSeats.HasValue && update.NewAdditionalSeats > plan.MaxAdditionalSeats.Value)
        {
            throw new BadRequestException($"Organization plan allows a maximum of " +
                                          $"{plan.MaxAdditionalSeats.Value} additional Secrets Manager seats.");
        }

        if (!organization.SmSeats.HasValue || organization.SmSeats.Value > update.NewTotalSeats)
        {
            var currentSeats = await _organizationUserRepository.GetOccupiedSmSeatCountByOrganizationIdAsync(organization.Id);
            if (currentSeats > update.NewTotalSeats)
            {
                throw new BadRequestException($"Your organization currently has {currentSeats} Secrets Manager seats. " +
                                              $"Your plan only allows ({update.NewTotalSeats}) Secrets Manager seats. Remove some Secrets Manager users.");
            }
        }
    }

    private async Task ProcessChargesAndRaiseEventsForAdjustSeatsAsync(Organization organization, Plan plan,
        SecretsManagerSubscriptionUpdate update)
    {
        await _paymentService.AdjustSeatsAsync(organization, plan, update.NewAdditionalSeats);

        // TODO: call ReferenceEventService - see AC-1481

        organization.SmSeats = update.NewTotalSeats;
    }

    private async Task AdjustServiceAccountsAsync(Organization organization, SecretsManagerSubscriptionUpdate update, Plan plan)
    {
        if (organization.SmServiceAccounts == null)
        {
            throw new BadRequestException("Organization has no Service Accounts limit, no need to adjust Service Accounts");
        }

        if (update.MaxAutoscaleServiceAccounts.HasValue && update.NewTotalServiceAccounts > update.MaxAutoscaleServiceAccounts.Value)
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

        if (plan.BaseServiceAccount > update.NewTotalServiceAccounts)
        {
            throw new BadRequestException($"Plan has a minimum of {plan.BaseServiceAccount} Service Accounts.");
        }

        if (update.NewTotalServiceAccounts <= 0)
        {
            throw new BadRequestException("You must have at least 1 Service Accounts.");
        }

        if (plan.MaxAdditionalServiceAccount.HasValue && update.NewAdditionalServiceAccounts > plan.MaxAdditionalServiceAccount.Value)
        {
            throw new BadRequestException($"Organization plan allows a maximum of " +
                                          $"{plan.MaxAdditionalServiceAccount.Value} additional Service Accounts.");
        }

        if (!organization.SmServiceAccounts.HasValue || organization.SmServiceAccounts.Value > update.NewTotalServiceAccounts)
        {
            var currentServiceAccounts = await _serviceAccountRepository.GetServiceAccountCountByOrganizationIdAsync(organization.Id);
            if (currentServiceAccounts > update.NewTotalServiceAccounts)
            {
                throw new BadRequestException($"Your organization currently has {currentServiceAccounts} Service Accounts. " +
                                              $"Your plan only allows ({update.NewTotalServiceAccounts}) Service Accounts. Remove some Service Accounts.");
            }
        }
    }

    private async Task ProcessChargesAndRaiseEventsForAdjustServiceAccountsAsync(Organization organization, Plan plan,
        SecretsManagerSubscriptionUpdate update)
    {
        await _paymentService.AdjustServiceAccountsAsync(organization, plan,
                update.NewAdditionalServiceAccounts);

        // TODO: call ReferenceEventService - see AC-1481

        organization.SmServiceAccounts = update.NewTotalServiceAccounts;
    }

    private void UpdateSeatsAutoscaling(Organization organization, int maxAutoscaleSeats, Plan plan)
    {
        if (organization.SmSeats.HasValue && maxAutoscaleSeats < organization.SmSeats.Value)
        {
            throw new BadRequestException($"Cannot set max Secrets Manager seat autoscaling below current Secrets Manager seat count.");
        }

        if (plan.MaxUsers.HasValue && maxAutoscaleSeats > plan.MaxUsers)
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

        organization.MaxAutoscaleSmSeats = maxAutoscaleSeats;
    }

    private void UpdateServiceAccountAutoscaling(Organization organization, int maxAutoscaleServiceAccounts, Plan plan)
    {
        if (organization.SmServiceAccounts.HasValue && maxAutoscaleServiceAccounts < organization.SmServiceAccounts.Value)
        {
            throw new BadRequestException(
                $"Cannot set max Service Accounts autoscaling below current Service Accounts count.");
        }

        if (!plan.AllowServiceAccountsAutoscale)
        {
            throw new BadRequestException("Your plan does not allow Service Accounts autoscaling.");
        }

        if (plan.MaxServiceAccounts.HasValue && maxAutoscaleServiceAccounts > plan.MaxServiceAccounts)
        {
            throw new BadRequestException(string.Concat(
                $"Your plan has a Service Accounts limit of {plan.MaxServiceAccounts}, ",
                $"but you have specified a max autoscale count of {maxAutoscaleServiceAccounts}.",
                "Reduce your max autoscale count."));
        }
        organization.MaxAutoscaleSmServiceAccounts = maxAutoscaleServiceAccounts;
    }
}
