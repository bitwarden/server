using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf.Types;

namespace Bit.Core.Billing.Organizations.Commands;

public interface IAddSecretsManagerSubscriptionCommand
{
    Task<BillingCommandResult<None>> Run(Organization organization, int additionalSmSeats, int additionalServiceAccounts);
}

public class AddSecretsManagerSubscriptionCommand(
    ILogger<AddSecretsManagerSubscriptionCommand> logger,
    IOrganizationService organizationService,
    IProviderRepository providerRepository,
    IPricingClient pricingClient,
    IUpdateOrganizationSubscriptionCommand updateOrganizationSubscriptionCommand)
    : BaseBillingCommand<AddSecretsManagerSubscriptionCommand>(logger), IAddSecretsManagerSubscriptionCommand
{
    public Task<BillingCommandResult<None>> Run(
        Organization organization,
        int additionalSmSeats,
        int additionalServiceAccounts) => HandleAsync(async () =>
    {
        if (organization.UseSecretsManager)
        {
            return new BadRequest("Organization already uses Secrets Manager.");
        }

        var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);

        if (!plan.SupportsSecretsManager)
        {
            return new BadRequest("Invalid Secrets Manager plan selected.");
        }

        if (plan.ProductTier != ProductTierType.Free)
        {
            if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
            {
                return new BadRequest("No payment method found.");
            }

            if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
            {
                return new BadRequest("No subscription found.");
            }
        }

        var provider = await providerRepository.GetByOrganizationIdAsync(organization.Id);
        if (provider is { Type: ProviderType.Msp })
        {
            return new BadRequest("Organizations with a Managed Service Provider do not support Secrets Manager.");
        }

        if (additionalSmSeats < 0)
        {
            return new BadRequest("You can't subtract Secrets Manager seats!");
        }

        if (plan.SecretsManager.BaseSeats + additionalSmSeats <= 0)
        {
            return new BadRequest("You do not have any Secrets Manager seats!");
        }

        if (!plan.SecretsManager.HasAdditionalServiceAccountOption && additionalServiceAccounts > 0)
        {
            return new BadRequest("Plan does not allow additional Machine Accounts.");
        }

        if ((plan.ProductTier == ProductTierType.TeamsStarter &&
             additionalSmSeats > plan.PasswordManager.BaseSeats) ||
            (plan.ProductTier != ProductTierType.TeamsStarter &&
             additionalSmSeats > organization.Seats.GetValueOrDefault()))
        {
            return new BadRequest("You cannot have more Secrets Manager seats than Password Manager seats.");
        }

        if (additionalServiceAccounts < 0)
        {
            return new BadRequest("You can't subtract Machine Accounts!");
        }

        if (!plan.SecretsManager.HasAdditionalSeatsOption && additionalSmSeats > 0)
        {
            return new BadRequest("Plan does not allow additional users.");
        }

        if (plan.SecretsManager.MaxAdditionalSeats.HasValue &&
            additionalSmSeats > plan.SecretsManager.MaxAdditionalSeats.Value)
        {
            return new BadRequest(
                $"Selected plan allows a maximum of {plan.SecretsManager.MaxAdditionalSeats.GetValueOrDefault(0)} additional users.");
        }

        if (plan.ProductTier != ProductTierType.Free)
        {
            var changes = new List<OrganizationSubscriptionChange>();

            if (additionalSmSeats > 0)
            {
                changes.Add(new AddItem(plan.SecretsManager.StripeSeatPlanId, additionalSmSeats));
            }

            if (additionalServiceAccounts > 0)
            {
                changes.Add(new AddItem(plan.SecretsManager.StripeServiceAccountPlanId, additionalServiceAccounts));
            }

            if (changes.Count > 0)
            {
                // ChargeImmediately = true is intentional: the old StripePaymentService.AddSecretsManagerToSubscription
                // passed invoiceNow = true to FinalizeSubscriptionChangeAsync, which maps to AlwaysInvoice proration.
                // UpdateOrganizationSubscriptionCommand maps ChargeImmediately = true → AlwaysInvoice identically.
                // Omitting it (defaulting to false) would silently switch to CreateProrations — a billing regression.
                var changeSet = new OrganizationSubscriptionChangeSet { Changes = changes, ChargeImmediately = true };
                var result = await updateOrganizationSubscriptionCommand.Run(organization, changeSet);
                if (!result.Success)
                {
                    return result.Map(_ => new None());
                }
            }
        }

        organization.SmSeats = plan.SecretsManager.BaseSeats + additionalSmSeats;
        organization.SmServiceAccounts = plan.SecretsManager.BaseServiceAccount + additionalServiceAccounts;
        organization.UseSecretsManager = true;
        await organizationService.ReplaceAndUpdateCacheAsync(organization);

        // TODO: call ReferenceEventService - see AC-1481
        return new None();
    });
}
