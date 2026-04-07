using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Exceptions;
using Bit.Core.Services;

namespace Bit.Core.Billing.Organizations.Commands;

public interface IAddSecretsManagerSubscriptionCommand
{
    Task RunAsync(Organization organization, int additionalSmSeats, int additionalServiceAccounts);
}

public class AddSecretsManagerSubscriptionCommand(
    IOrganizationService organizationService,
    IProviderRepository providerRepository,
    IPricingClient pricingClient,
    IUpdateOrganizationSubscriptionCommand updateOrganizationSubscriptionCommand)
    : IAddSecretsManagerSubscriptionCommand
{
    public async Task RunAsync(
        Organization organization,
        int additionalSmSeats,
        int additionalServiceAccounts)
    {
        if (organization.UseSecretsManager)
        {
            throw new BadRequestException("Organization already uses Secrets Manager.");
        }

        var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);

        if (!plan.SupportsSecretsManager)
        {
            throw new BadRequestException("Invalid Secrets Manager plan selected.");
        }

        if (plan.ProductTier != ProductTierType.Free)
        {
            if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
            {
                throw new BadRequestException("No payment method found.");
            }

            if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
            {
                throw new BadRequestException("No subscription found.");
            }
        }

        var provider = await providerRepository.GetByOrganizationIdAsync(organization.Id);
        if (provider is { Type: ProviderType.Msp })
        {
            throw new BadRequestException("Organizations with a Managed Service Provider do not support Secrets Manager.");
        }

        if (additionalSmSeats < 0)
        {
            throw new BadRequestException("You can't subtract Secrets Manager seats!");
        }

        if (plan.SecretsManager.BaseSeats + additionalSmSeats <= 0)
        {
            throw new BadRequestException("You do not have any Secrets Manager seats!");
        }

        if (!plan.SecretsManager.HasAdditionalServiceAccountOption && additionalServiceAccounts > 0)
        {
            throw new BadRequestException("Plan does not allow additional Machine Accounts.");
        }

        if ((plan.ProductTier == ProductTierType.TeamsStarter &&
             additionalSmSeats > plan.PasswordManager.BaseSeats) ||
            (plan.ProductTier != ProductTierType.TeamsStarter &&
             additionalSmSeats > organization.Seats.GetValueOrDefault()))
        {
            throw new BadRequestException("You cannot have more Secrets Manager seats than Password Manager seats.");
        }

        if (additionalServiceAccounts < 0)
        {
            throw new BadRequestException("You can't subtract Machine Accounts!");
        }

        if (!plan.SecretsManager.HasAdditionalSeatsOption && additionalSmSeats > 0)
        {
            throw new BadRequestException("Plan does not allow additional users.");
        }

        if (plan.SecretsManager.MaxAdditionalSeats.HasValue &&
            additionalSmSeats > plan.SecretsManager.MaxAdditionalSeats.Value)
        {
            throw new BadRequestException(
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
                result.GetValueOrThrow();
            }
        }

        organization.SmSeats = plan.SecretsManager.BaseSeats + additionalSmSeats;
        organization.SmServiceAccounts = plan.SecretsManager.BaseServiceAccount + additionalServiceAccounts;
        organization.UseSecretsManager = true;
        await organizationService.ReplaceAndUpdateCacheAsync(organization);

        // TODO: call ReferenceEventService - see AC-1481
    }
}
