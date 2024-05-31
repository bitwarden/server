using Bit.Billing.Constants;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Microsoft.Data.SqlClient;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class ChargeSucceededHandler : IChargeSucceededHandler
{
    private readonly ILogger<ChargeSucceededHandler> _logger;
    private readonly IStripeEventService _stripeEventService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IStripeEventUtilityService _stripeEventUtilityService;

    public ChargeSucceededHandler(
        ILogger<ChargeSucceededHandler> logger,
        IStripeEventService stripeEventService,
        ITransactionRepository transactionRepository,
        IStripeEventUtilityService stripeEventUtilityService)
    {
        _logger = logger;
        _stripeEventService = stripeEventService;
        _transactionRepository = transactionRepository;
        _stripeEventUtilityService = stripeEventUtilityService;
    }

    /// <summary>
    /// Handles the <see cref="HandledStripeWebhook.ChargeSucceeded"/> event type from Stripe.
    /// </summary>
    /// <param name="parsedEvent"></param>
    public async Task HandleAsync(Event parsedEvent)
    {
        var charge = await _stripeEventService.GetCharge(parsedEvent);
        var existingTransaction = await _transactionRepository.GetByGatewayIdAsync(GatewayType.Stripe, charge.Id);
        if (existingTransaction is not null)
        {
            _logger.LogInformation("Charge success already processed. {ChargeId}", charge.Id);
            return;
        }

        var (organizationId, userId, providerId) = await _stripeEventUtilityService.GetEntityIdsFromChargeAsync(charge);
        if (!organizationId.HasValue && !userId.HasValue && !providerId.HasValue)
        {
            _logger.LogWarning("Charge success has no subscriber ids. {ChargeId}", charge.Id);
            return;
        }

        var transaction = _stripeEventUtilityService.FromChargeToTransaction(charge, organizationId, userId, providerId);
        if (!transaction.PaymentMethodType.HasValue)
        {
            _logger.LogWarning("Charge success from unsupported source/method. {ChargeId}", charge.Id);
            return;
        }

        try
        {
            await _transactionRepository.CreateAsync(transaction);
        }
        catch (SqlException e) when (e.Number == 547)
        {
            _logger.LogWarning(
                "Charge success could not create transaction as entity may have been deleted. {ChargeId}",
                charge.Id);
        }
    }
}
