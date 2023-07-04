using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate.Interface;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate;

public class UpdateSecretsManagerSubscriptionCommand : IUpdateSecretsManagerSubscriptionCommand
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPaymentService _paymentService;
    private readonly IOrganizationService _organizationService;
    private readonly ICurrentContext _currentContext;
    private readonly IMailService _mailService;
    private readonly ILogger<UpdateSecretsManagerSubscriptionCommand> _logger;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly IReferenceEventService _referenceEventService;

    public UpdateSecretsManagerSubscriptionCommand(
        IOrganizationRepository organizationRepository,
        IOrganizationService organizationService,
        IOrganizationUserRepository organizationUserRepository,
        IPaymentService paymentService,
        ICurrentContext currentContext,
        IMailService mailService,
        ILogger<UpdateSecretsManagerSubscriptionCommand> logger,
        IServiceAccountRepository serviceAccountRepository,
        IReferenceEventService referenceEventService)
    {
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _paymentService = paymentService;
        _organizationService = organizationService;
        _currentContext = currentContext;
        _mailService = mailService;
        _logger = logger;
        _serviceAccountRepository = serviceAccountRepository;
        _referenceEventService = referenceEventService;
    }

    public async Task UpdateSecretsManagerSubscription(SecretsManagerSubscriptionUpdate update)
    {
        var organization = await _organizationRepository.GetByIdAsync(update.OrganizationId);

        if (organization == null)
        {
            throw new NotFoundException("Organization is not found");
        }

        if (!organization.UseSecretsManager)
        {
            throw new BadRequestException("Organization has no access to Secrets Manager.");
        }

        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(p => p.Type == organization.PlanType);

        if (plan == null)
        {
            throw new BadRequestException("Existing plan not found.");
        }

        if (update.SeatAdjustment != 0)
        {
            await AdjustSeatsAsync(organization, update, plan);
        }

        if (update.ServiceAccountsAdjustment != 0)
        {
            await AdjustServiceAccountsAsync(organization, update, plan);
        }

        if (update.MaxAutoscaleSeats != organization.MaxAutoscaleSmSeats)
        {
            UpdateSeatsAutoscaling(organization, update.MaxAutoscaleSeats, plan);
        }

        if (update.MaxAutoscaleServiceAccounts != organization.MaxAutoscaleSmServiceAccounts)
        {
            UpdateServiceAccountAutoscaling(organization, update.MaxAutoscaleServiceAccounts, plan);
        }

        await _organizationService.ReplaceAndUpdateCacheAsync(organization);

        if (organization.SmSeats.HasValue && organization.MaxAutoscaleSmSeats.HasValue && organization.SmSeats == organization.MaxAutoscaleSmSeats)
        {
            await SendEmailAsync(organization, organization.MaxAutoscaleSmSeats.Value, "Seats");
        }

        if (organization.SmServiceAccounts.HasValue && organization.MaxAutoscaleSmServiceAccounts.HasValue && organization.SmServiceAccounts == organization.MaxAutoscaleSmServiceAccounts)
        {
            await SendEmailAsync(organization, organization.MaxAutoscaleSmServiceAccounts.Value, "Service Accounts");
        }
    }

    private async Task SendEmailAsync(Organization organization, int MaxAutoscaleValue, string adjustingProduct)
    {
        try
        {
            var ownerEmails = (await _organizationUserRepository.GetManyByMinimumRoleAsync(organization.Id,
                    OrganizationUserType.Owner))
                .Select(u => u.Email).Distinct();

            await _mailService.SendOrganizationMaxSeatLimitReachedEmailAsync(organization, MaxAutoscaleValue, ownerEmails);

        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Error encountered notifying organization owners of {adjustingProduct} limit reached.");
        }

    }

    private async Task<string> AdjustSeatsAsync(Organization organization, SecretsManagerSubscriptionUpdate update, Plan plan)
    {
        var newSeatCount = organization.SmSeats.GetValueOrDefault() + update.SeatAdjustment;

        if (update.MaxAutoscaleSeats.HasValue && newSeatCount > update.MaxAutoscaleSeats.Value)
        {
            throw new BadRequestException("Cannot set max seat autoscaling below seat count.");
        }

        if (organization.SmSeats == null)
        {
            throw new BadRequestException("Organization has no Secrets Manager seat limit, no need to adjust seats");
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

        if (plan.BaseSeats > newSeatCount)
        {
            throw new BadRequestException($"Plan has a minimum of {plan.BaseSeats} Secrets Manager  seats.");
        }

        if (newSeatCount <= 0)
        {
            throw new BadRequestException("You must have at least 1 Secrets Manager seat.");
        }

        var additionalSeats = newSeatCount - plan.BaseSeats;

        if (plan.MaxAdditionalSeats.HasValue && additionalSeats > plan.MaxAdditionalSeats.Value)
        {
            throw new BadRequestException($"Organization plan allows a maximum of " +
                                          $"{plan.MaxAdditionalSeats.Value} additional Secrets Manager seats.");
        }

        if (!organization.SmSeats.HasValue || organization.SmSeats.Value > newSeatCount)
        {
            var currentSeats = await _organizationUserRepository.GetOccupiedSmSeatCountByOrganizationIdAsync(organization.Id);
            if (currentSeats > newSeatCount)
            {
                throw new BadRequestException($"Your organization currently has {currentSeats} Secrets Manager seats. " +
                                              $"Your new plan only allows ({newSeatCount}) Secrets Manager seats. Remove some Secrets Manager users.");
            }
        }

        var paymentIntentClientSecret = await _paymentService.AdjustSeatsAsync(organization, plan, additionalSeats);

        await _referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.AdjustSmSeats, organization, _currentContext)
            {
                Id = organization.Id,
                PlanName = plan.Name,
                PlanType = plan.Type,
                Seats = newSeatCount,
                PreviousSeats = organization.SmSeats
            });

        organization.SmSeats = (short?)newSeatCount;

        return paymentIntentClientSecret;
    }

    private async Task<string> AdjustServiceAccountsAsync(Organization organization, SecretsManagerSubscriptionUpdate update, Plan plan)
    {
        var newServiceAccountsTotal = organization.SmServiceAccounts.GetValueOrDefault() + update.ServiceAccountsAdjustment;

        if (update.MaxAutoscaleServiceAccounts.HasValue && newServiceAccountsTotal > update.MaxAutoscaleServiceAccounts.Value)
        {
            throw new BadRequestException("Cannot set max Service Accounts autoscaling below Service Accounts count.");
        }

        if (organization.SmServiceAccounts == null)
        {
            throw new BadRequestException("Organization has no Service Accounts limit, no need to adjust Service Accounts");
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

        if (plan.BaseServiceAccount > newServiceAccountsTotal)
        {
            throw new BadRequestException($"Plan has a minimum of {plan.BaseServiceAccount} Service Accounts.");
        }

        if (newServiceAccountsTotal <= 0)
        {
            throw new BadRequestException("You must have at least 1 Service Accounts.");
        }

        var additionalServiceAccounts = newServiceAccountsTotal - plan.BaseServiceAccount;

        if (plan.MaxAdditionalServiceAccount.HasValue && additionalServiceAccounts > plan.MaxAdditionalServiceAccount.Value)
        {
            throw new BadRequestException($"Organization plan allows a maximum of " +
                                          $"{plan.MaxAdditionalServiceAccount.Value} additional Service Accounts.");
        }

        if (!organization.SmServiceAccounts.HasValue || organization.SmServiceAccounts.Value > newServiceAccountsTotal)
        {
            var currentServiceAccounts = await _serviceAccountRepository.GetServiceAccountCountByOrganizationIdAsync(organization.Id);
            if (currentServiceAccounts > newServiceAccountsTotal)
            {
                throw new BadRequestException($"Your organization currently has {currentServiceAccounts} Service Accounts. " +
                                              $"Your new plan only allows ({newServiceAccountsTotal}) Service Accounts. Remove some Service Accounts.");
            }
        }

        var paymentIntentClientSecret = await _paymentService.AdjustServiceAccountsAsync(organization, plan, additionalServiceAccounts.GetValueOrDefault());

        await _referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.AdjustServiceAccounts, organization, _currentContext)
            {
                Id = organization.Id,
                PlanName = plan.Name,
                PlanType = plan.Type,
                ServiceAccounts = newServiceAccountsTotal,
                PreviousServiceAccounts = organization.SmServiceAccounts
            });

        organization.SmServiceAccounts = newServiceAccountsTotal;

        return paymentIntentClientSecret;
    }

    private void UpdateSeatsAutoscaling(Organization organization, int? maxAutoscaleSeats, Plan plan)
    {
        if (maxAutoscaleSeats.HasValue && organization.SmSeats.HasValue &&
            maxAutoscaleSeats.Value < organization.SmSeats.Value)
        {
            throw new BadRequestException($"Cannot set max Secrets Manager seat autoscaling below current Secrets Manager seat count.");
        }

        if (!plan.AllowSeatAutoscale)
        {
            throw new BadRequestException("Your plan does not allow Secrets Manager seat autoscaling.");
        }

        if (plan.MaxUsers.HasValue && maxAutoscaleSeats.HasValue &&
            maxAutoscaleSeats > plan.MaxUsers)
        {
            throw new BadRequestException(string.Concat(
                $"Your plan has a Secrets Manager seat limit of {plan.MaxUsers}, ",
                $"but you have specified a max autoscale count of {maxAutoscaleSeats}.",
                "Reduce your max autoscale count."));
        }

        organization.MaxAutoscaleSmSeats = maxAutoscaleSeats;
    }

    private void UpdateServiceAccountAutoscaling(Organization organization, int? maxAutoscaleServiceAccounts, Plan plan)
    {
        if (maxAutoscaleServiceAccounts.HasValue &&
            organization.SmServiceAccounts.HasValue &&
            maxAutoscaleServiceAccounts.Value < organization.SmServiceAccounts.Value)
        {
            throw new BadRequestException(
                $"Cannot set max Service Accounts autoscaling below current Service Accounts count.");
        }

        if (!plan.AllowServiceAccountsAutoscale)
        {
            throw new BadRequestException("Your plan does not allow Service Accounts autoscaling.");
        }

        if (plan.MaxServiceAccounts.HasValue && maxAutoscaleServiceAccounts.HasValue &&
            maxAutoscaleServiceAccounts > plan.MaxServiceAccounts)
        {
            throw new BadRequestException(string.Concat(
                $"Your plan has a Service Accounts limit of {plan.MaxServiceAccounts}, ",
                $"but you have specified a max autoscale count of {maxAutoscaleServiceAccounts}.",
                "Reduce your max autoscale count."));
        }
        organization.MaxAutoscaleSmServiceAccounts = maxAutoscaleServiceAccounts;
    }
}
