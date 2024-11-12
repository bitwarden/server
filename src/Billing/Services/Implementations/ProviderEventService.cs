using Bit.Billing.Constants;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Repositories;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Stripe;

namespace Bit.Billing.Services.Implementations;

public class ProviderEventService(
    ILogger<ProviderEventService> logger,
    IProviderInvoiceItemRepository providerInvoiceItemRepository,
    IProviderOrganizationRepository providerOrganizationRepository,
    IProviderPlanRepository providerPlanRepository,
    IStripeEventService stripeEventService,
    IStripeFacade stripeFacade) : IProviderEventService
{
    public async Task TryRecordInvoiceLineItems(Event parsedEvent)
    {
        if (parsedEvent.Type is not HandledStripeWebhook.InvoiceCreated and not HandledStripeWebhook.InvoiceFinalized)
        {
            return;
        }

        var invoice = await stripeEventService.GetInvoice(parsedEvent);

        var metadata = (await stripeFacade.GetSubscription(invoice.SubscriptionId)).Metadata ?? new Dictionary<string, string>();

        var hasProviderId = metadata.TryGetValue("providerId", out var providerId);

        if (!hasProviderId)
        {
            return;
        }

        var parsedProviderId = Guid.Parse(providerId);

        switch (parsedEvent.Type)
        {
            case HandledStripeWebhook.InvoiceCreated:
                {
                    var clients =
                        await providerOrganizationRepository.GetManyDetailsByProviderAsync(parsedProviderId);

                    var providerPlans = await providerPlanRepository.GetByProviderId(parsedProviderId);

                    var invoiceItems = new List<ProviderInvoiceItem>();

                    foreach (var client in clients)
                    {
                        if (client.Status != OrganizationStatusType.Managed)
                        {
                            continue;
                        }

                        var plan = StaticStore.Plans.Single(x => x.Name == client.Plan && providerPlans.Any(y => y.PlanType == x.Type));

                        var discountedPercentage = (100 - (invoice.Discount?.Coupon?.PercentOff ?? 0)) / 100;

                        var discountedSeatPrice = plan.PasswordManager.ProviderPortalSeatPrice * discountedPercentage;

                        invoiceItems.Add(new ProviderInvoiceItem
                        {
                            ProviderId = parsedProviderId,
                            InvoiceId = invoice.Id,
                            InvoiceNumber = invoice.Number,
                            ClientId = client.OrganizationId,
                            ClientName = client.OrganizationName,
                            PlanName = client.Plan,
                            AssignedSeats = client.Seats ?? 0,
                            UsedSeats = client.OccupiedSeats ?? 0,
                            Total = (client.Seats ?? 0) * discountedSeatPrice
                        });
                    }

                    foreach (var providerPlan in providerPlans.Where(x => x.PurchasedSeats is null or 0))
                    {
                        var plan = StaticStore.GetPlan(providerPlan.PlanType);

                        var clientSeats = invoiceItems
                            .Where(item => item.PlanName == plan.Name)
                            .Sum(item => item.AssignedSeats);

                        var unassignedSeats = providerPlan.SeatMinimum - clientSeats ?? 0;

                        var discountedPercentage = (100 - (invoice.Discount?.Coupon?.PercentOff ?? 0)) / 100;

                        var discountedSeatPrice = plan.PasswordManager.ProviderPortalSeatPrice * discountedPercentage;

                        invoiceItems.Add(new ProviderInvoiceItem
                        {
                            ProviderId = parsedProviderId,
                            InvoiceId = invoice.Id,
                            InvoiceNumber = invoice.Number,
                            ClientName = "Unassigned seats",
                            PlanName = plan.Name,
                            AssignedSeats = unassignedSeats,
                            UsedSeats = 0,
                            Total = unassignedSeats * discountedSeatPrice
                        });
                    }

                    await Task.WhenAll(invoiceItems.Select(providerInvoiceItemRepository.CreateAsync));

                    break;
                }
            case HandledStripeWebhook.InvoiceFinalized:
                {
                    var invoiceItems = await providerInvoiceItemRepository.GetByInvoiceId(invoice.Id);

                    if (invoiceItems.Count != 0)
                    {
                        await Task.WhenAll(invoiceItems.Select(invoiceItem =>
                        {
                            invoiceItem.InvoiceNumber = invoice.Number;
                            return providerInvoiceItemRepository.ReplaceAsync(invoiceItem);
                        }));
                    }

                    break;
                }
        }
    }
}
