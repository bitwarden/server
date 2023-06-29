using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Commands.ServiceAccounts.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;

namespace Bit.Commercial.Core.SecretsManager.Commands.ServiceAccounts;

public class AutoscaleServiceAccountsCommand : IAutoscaleServiceAccountsCommand
{
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IReferenceEventService _referenceEventService;
    private readonly IPaymentService _paymentService;
    private readonly ICurrentContext _currentContext;

    public AutoscaleServiceAccountsCommand(
        IServiceAccountRepository serviceAccountRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationService organizationService,
        IReferenceEventService referenceEventService,
        IPaymentService paymentService,
        ICurrentContext currentContext)
    {
        _serviceAccountRepository = serviceAccountRepository;
        _organizationRepository = organizationRepository;
        _organizationService = organizationService;
        _referenceEventService = referenceEventService;
        _paymentService = paymentService;
        _currentContext = currentContext;
    }

    public async Task<string> AutoscaleServiceAccountsAsync(Guid organizationId, int serviceAccountsToAdd)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        if (organization.SmServiceAccounts == null)
        {
            throw new BadRequestException("Organization has no Secrets Manager Service Accounts limit, no need to adjust seats");
        }

        if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
        {
            throw new BadRequestException("No payment method found.");
        }

        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
        {
            throw new BadRequestException("No subscription found.");
        }

        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(p => p.Type == organization.PlanType);
        if (plan == null)
        {
            throw new BadRequestException("Existing plan not found.");
        }

        if (!plan.HasAdditionalServiceAccountOption)
        {
            throw new BadRequestException("Plan does not allow additional service accounts.");
        }

        var newServiceAccountSlotsTotal = organization.SmServiceAccounts.Value + serviceAccountsToAdd;
        if (plan.BaseServiceAccount > newServiceAccountSlotsTotal)
        {
            throw new BadRequestException($"Plan has a minimum of {plan.BaseServiceAccount} service account slots.");
        }

        if (newServiceAccountSlotsTotal <= 0)
        {
            throw new BadRequestException("You must have at least 1 service account slot.");
        }

        var additionalServiceAccountSlots = newServiceAccountSlotsTotal - plan.BaseServiceAccount.GetValueOrDefault();
        if (plan.MaxAdditionalServiceAccount.HasValue && additionalServiceAccountSlots > plan.MaxAdditionalServiceAccount.Value)
        {
            throw new BadRequestException($"Organization plan allows a maximum of " +
                $"{plan.MaxAdditionalServiceAccount.Value} additional service accounts.");
        }

        if (!organization.SmServiceAccounts.HasValue || organization.SmServiceAccounts.Value > newServiceAccountSlotsTotal)
        {
            var occupiedServiceAccountCount = await _serviceAccountRepository.GetServiceAccountCountByOrganizationIdAsync(organization.Id);
            if (occupiedServiceAccountCount > newServiceAccountSlotsTotal)
            {
                throw new BadRequestException($"Your organization currently has {occupiedServiceAccountCount} service account slots filled. " +
                    $"Your new plan only has ({newServiceAccountSlotsTotal}) slots. Remove some service accounts.");
            }
        }

        var paymentIntentClientSecret = await _paymentService.AdjustSeatsAsync(organization, plan, additionalServiceAccountSlots);
        await _referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.AdjustServiceAccounts, organization, _currentContext)
            {
                PlanName = plan.Name,
                PlanType = plan.Type,
                Seats = newServiceAccountSlotsTotal,
                PreviousSeats = organization.SmServiceAccounts
            });
        organization.SmServiceAccounts = (short?)newServiceAccountSlotsTotal;

        await _organizationService.ReplaceAndUpdateCacheAsync(organization);

        return paymentIntentClientSecret;
    }
}
