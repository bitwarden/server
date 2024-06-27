using Bit.Billing.Constants;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Enums;
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
                        (await providerOrganizationRepository.GetManyDetailsByProviderAsync(parsedProviderId))
                        .Where(providerOrganization => providerOrganization.Status == OrganizationStatusType.Managed);

                    var providerPlans = await providerPlanRepository.GetByProviderId(parsedProviderId);

                    var enterpriseProviderPlan =
                        providerPlans.FirstOrDefault(providerPlan => providerPlan.PlanType == PlanType.EnterpriseMonthly);

                    var teamsProviderPlan =
                        providerPlans.FirstOrDefault(providerPlan => providerPlan.PlanType == PlanType.TeamsMonthly);

                    if (enterpriseProviderPlan == null || !enterpriseProviderPlan.IsConfigured() ||
                        teamsProviderPlan == null || !teamsProviderPlan.IsConfigured())
                    {
                        logger.LogError("Provider {ProviderID} is missing or has misconfigured provider plans", parsedProviderId);

                        throw new Exception("Cannot record invoice line items for Provider with missing or misconfigured provider plans");
                    }

                    var enterprisePlan = StaticStore.GetPlan(PlanType.EnterpriseMonthly);

                    var teamsPlan = StaticStore.GetPlan(PlanType.TeamsMonthly);

                    var discountedPercentage = (100 - (invoice.Discount?.Coupon?.PercentOff ?? 0)) / 100;

                    var discountedEnterpriseSeatPrice = enterprisePlan.PasswordManager.ProviderPortalSeatPrice * discountedPercentage;

                    var discountedTeamsSeatPrice = teamsPlan.PasswordManager.ProviderPortalSeatPrice * discountedPercentage;

                    var invoiceItems = clients.Select(client => new ProviderInvoiceItem
                    {
                        ProviderId = parsedProviderId,
                        InvoiceId = invoice.Id,
                        InvoiceNumber = invoice.Number,
                        ClientName = client.OrganizationName,
                        PlanName = client.Plan,
                        AssignedSeats = client.Seats ?? 0,
                        UsedSeats = (client.Seats ?? 0) - (client.AssignedSeats ?? 0),
                        Total = client.Plan == enterprisePlan.Name
                            ? (client.Seats ?? 0) * discountedEnterpriseSeatPrice
                            : (client.Seats ?? 0) * discountedTeamsSeatPrice
                    }).ToList();

                    if (enterpriseProviderPlan.PurchasedSeats is null or 0)
                    {
                        var enterpriseClientSeats = invoiceItems
                            .Where(item => item.PlanName == enterprisePlan.Name)
                            .Sum(item => item.AssignedSeats);

                        var unassignedEnterpriseSeats = enterpriseProviderPlan.SeatMinimum - enterpriseClientSeats ?? 0;

                        if (unassignedEnterpriseSeats > 0)
                        {
                            invoiceItems.Add(new ProviderInvoiceItem
                            {
                                ProviderId = parsedProviderId,
                                InvoiceId = invoice.Id,
                                InvoiceNumber = invoice.Number,
                                ClientName = "Unassigned seats",
                                PlanName = enterprisePlan.Name,
                                AssignedSeats = unassignedEnterpriseSeats,
                                UsedSeats = 0,
                                Total = unassignedEnterpriseSeats * discountedEnterpriseSeatPrice
                            });
                        }
                    }

                    if (teamsProviderPlan.PurchasedSeats is null or 0)
                    {
                        var teamsClientSeats = invoiceItems
                            .Where(item => item.PlanName == teamsPlan.Name)
                            .Sum(item => item.AssignedSeats);

                        var unassignedTeamsSeats = teamsProviderPlan.SeatMinimum - teamsClientSeats ?? 0;

                        if (unassignedTeamsSeats > 0)
                        {
                            invoiceItems.Add(new ProviderInvoiceItem
                            {
                                ProviderId = parsedProviderId,
                                InvoiceId = invoice.Id,
                                InvoiceNumber = invoice.Number,
                                ClientName = "Unassigned seats",
                                PlanName = teamsPlan.Name,
                                AssignedSeats = unassignedTeamsSeats,
                                UsedSeats = 0,
                                Total = unassignedTeamsSeats * discountedTeamsSeatPrice
                            });
                        }
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
