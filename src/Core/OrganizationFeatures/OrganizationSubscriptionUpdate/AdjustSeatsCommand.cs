using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate.Interface;
using Bit.Core.Tools.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate;

public class AdjustSeatsCommand : IAdjustSeatsCommand
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IPaymentService _paymentService;
    private readonly IReferenceEventService _referenceEventService;
    private readonly ICurrentContext _currentContext;
    private readonly IMailService _mailService;
    private readonly ILogger<AdjustSeatsCommand> _logger;

    public AdjustSeatsCommand(
        IOrganizationService organizationService,
        IOrganizationUserRepository organizationUserRepository,
        IPaymentService paymentService,
        IReferenceEventService referenceEventService,
        ICurrentContext currentContext,
        IMailService mailService,
        ILogger<AdjustSeatsCommand> logger)
    {
        _organizationService = organizationService;
        _organizationUserRepository = organizationUserRepository;
        _paymentService = paymentService;
        _referenceEventService = referenceEventService;
        _currentContext = currentContext;
        _mailService = mailService;
        _logger = logger;
    }

    public async Task<string> AdjustSeatsAsync(Organization organization, int seatAdjustment,
        DateTime? prorationDate = null, IEnumerable<string> ownerEmails = null)
    {
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

        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(p => p.Type == organization.PlanType);

        if (plan == null)
        {
            throw new BadRequestException("Existing plan not found.");
        }

        if (!plan.HasAdditionalSeatsOption)
        {
            throw new BadRequestException("Plan does not allow additional Secrets Manager seats.");
        }

        var newSeatTotal = organization.SmSeats.HasValue
            ? organization.SmSeats.Value + seatAdjustment
            : seatAdjustment;

        if (plan.BaseSeats > newSeatTotal)
        {
            throw new BadRequestException($"Plan has a minimum of {plan.BaseSeats} Secrets Manager  seats.");
        }

        if (newSeatTotal <= 0)
        {
            throw new BadRequestException("You must have at least 1 Secrets Manager seat.");
        }

        var additionalSeats = newSeatTotal - plan.BaseSeats;

        if (plan.MaxAdditionalSeats.HasValue && additionalSeats > plan.MaxAdditionalSeats.Value)
        {
            throw new BadRequestException($"Organization plan allows a maximum of " +
                                          $"{plan.MaxAdditionalSeats.Value} additional Secrets Manager seats.");
        }

        if (!organization.SmSeats.HasValue || organization.SmSeats.Value > newSeatTotal)
        {
            var occupiedSeats = await _organizationUserRepository.GetOccupiedSmSeatCountByOrganizationIdAsync(organization.Id);
            if (occupiedSeats > newSeatTotal)
            {
                throw new BadRequestException($"Your organization currently has {occupiedSeats} seats filled. " +
                                              $"Your new plan only has ({newSeatTotal}) seats. Remove some users.");
            }
        }

        var paymentIntentClientSecret = await _paymentService.AdjustSeatsAsync(organization, plan, additionalSeats, prorationDate);

        await _referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.AdjustSmSeats, organization, _currentContext)
            {
                PlanName = plan.Name,
                PlanType = plan.Type,
                Seats = newSeatTotal,
                PreviousSeats = organization.SmSeats
            });

        organization.SmSeats = (short?)newSeatTotal;

        await _organizationService.ReplaceAndUpdateCacheAsync(organization);

        if (organization.SmSeats.HasValue && organization.MaxAutoscaleSmSeats.HasValue && organization.SmSeats == organization.MaxAutoscaleSmSeats)
        {
            try
            {
                ownerEmails ??= (await _organizationUserRepository.GetManyByMinimumRoleAsync(organization.Id,
                        OrganizationUserType.Owner))
                    .Where(u => u.AccessSecretsManager)
                    .Select(u => u.Email).Distinct();
                await _mailService.SendOrganizationMaxSeatLimitReachedEmailAsync(organization, organization.MaxAutoscaleSmSeats.Value, ownerEmails);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error encountered notifying organization owners of seat limit reached.");
            }
        }

        return paymentIntentClientSecret;
    }
}
