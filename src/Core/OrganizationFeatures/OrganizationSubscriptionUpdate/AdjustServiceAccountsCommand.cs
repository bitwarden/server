using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate.Interface;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate;

public class AdjustServiceAccountsCommand : IAdjustServiceAccountsCommand
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IPaymentService _paymentService;
    private readonly IReferenceEventService _referenceEventService;
    private readonly ICurrentContext _currentContext; 
    private readonly IMailService _mailService;
    private readonly ILogger<AdjustServiceAccountsCommand> _logger;
    private readonly IServiceAccountRepository _serviceAccountRepository;
    
    public AdjustServiceAccountsCommand(
        IOrganizationService organizationService,
        IOrganizationUserRepository organizationUserRepository,
        IPaymentService paymentService,
        IReferenceEventService referenceEventService,
        ICurrentContext currentContext,
        IMailService mailService,
        ILogger<AdjustServiceAccountsCommand> logger,
        IServiceAccountRepository serviceAccountRepository)
    {
        _organizationService = organizationService;
        _organizationUserRepository = organizationUserRepository;
        _paymentService = paymentService;
        _referenceEventService = referenceEventService;
        _currentContext = currentContext;
        _mailService = mailService;
        _logger = logger;
        _serviceAccountRepository = serviceAccountRepository;
    }
    
    public async Task<string> AdjustServiceAccountsAsync(Organization organization, int serviceAccountAdjustment, IEnumerable<string> ownerEmails = null,
        DateTime? prorationDate = null)
    { 
        if (organization.SmServiceAccounts == null)
        {
            throw new BadRequestException("Organization has no Service Account limit, no need to adjust Service Account");
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
            throw new BadRequestException("Plan does not allow additional Service Account.");
        }

        var newServiceAccountsTotal = organization.SmServiceAccounts.HasValue
            ? organization.SmServiceAccounts.Value + serviceAccountAdjustment
            : serviceAccountAdjustment;

        if (plan.BaseServiceAccount > newServiceAccountsTotal)
        {
            throw new BadRequestException($"Plan has a minimum of {plan.BaseServiceAccount} Service Account.");
        }

        if (newServiceAccountsTotal <= 0)
        {
            throw new BadRequestException("You must have at least 1 Service Account.");
        }

        var additionalServiceAccounts = newServiceAccountsTotal - plan.BaseServiceAccount;

        if (plan.MaxAdditionalServiceAccount.HasValue && additionalServiceAccounts > plan.MaxAdditionalServiceAccount.Value)
        {
            throw new BadRequestException($"Organization plan allows a maximum of " +
                                          $"{plan.MaxAdditionalServiceAccount.Value} additional Service Account.");
        }

        if (!organization.SmServiceAccounts.HasValue || organization.SmServiceAccounts.Value > newServiceAccountsTotal)
        {
            var occupiedServiceAccounts = await _serviceAccountRepository.GetServiceAccountCountByOrganizationIdAsync(organization.Id);
            if (occupiedServiceAccounts > newServiceAccountsTotal)
            {
                throw new BadRequestException($"Your organization currently has {occupiedServiceAccounts} seats filled. " +
                                              $"Your new plan only has ({occupiedServiceAccounts}) seats. Remove some users.");
            }
        }

        var paymentIntentClientSecret = await _paymentService.AdjustServiceAccountsAsync(organization, plan, serviceAccountAdjustment, prorationDate);

        await _referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.AdjustServiceAccounts, organization, _currentContext)
            {
                PlanName = plan.Name,
                PlanType = plan.Type,
                ServiceAccounts = newServiceAccountsTotal,
                PreviousServiceAccounts = organization.SmServiceAccounts
            });

        organization.SmServiceAccounts = newServiceAccountsTotal;

        await _organizationService.ReplaceAndUpdateCacheAsync(organization);

        if (organization.SmServiceAccounts.HasValue && organization.MaxAutoscaleSmServiceAccounts.HasValue && organization.SmServiceAccounts == organization.MaxAutoscaleSmServiceAccounts
            && serviceAccountAdjustment > 0)
        {
            try
            {
                if (ownerEmails == null)
                {
                    ownerEmails ??= (await _organizationUserRepository.GetManyByMinimumRoleAsync(organization.Id,
                            OrganizationUserType.Owner))
                        .Where(u => u.AccessSecretsManager == true)
                        .Select(u => u.Email).Distinct();
                }
                await _mailService.SendOrganizationMaxSeatLimitReachedEmailAsync(organization, organization.MaxAutoscaleSeats.Value, ownerEmails);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error encountered notifying organization owners of Service Account limit reached.");
            }
        }

        return paymentIntentClientSecret;
    }
}
