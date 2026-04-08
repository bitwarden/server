using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Pricing;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using StaticStorePlan = Bit.Core.Models.StaticStore.Plan;

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
        var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);
        var provider = await providerRepository.GetByOrganizationIdAsync(organization.Id);

        ValidateOrganization(organization, plan, provider);
        ValidateSecretsManagerPlan(plan, additionalSmSeats, additionalServiceAccounts, organization);

        if (plan.ProductTier != ProductTierType.Free)
        {
            // additionalSmSeats is always > 0 here; ValidatePaidSecretsManagerPlan enforces it.
            var changes = new List<OrganizationSubscriptionChange>
            {
                new AddItem(plan.SecretsManager.StripeSeatPlanId, additionalSmSeats)
            };

            if (additionalServiceAccounts > 0)
            {
                changes.Add(new AddItem(plan.SecretsManager.StripeServiceAccountPlanId, additionalServiceAccounts));
            }

            var changeSet = new OrganizationSubscriptionChangeSet { Changes = changes, ChargeImmediately = true };
            var result = await updateOrganizationSubscriptionCommand.Run(organization, changeSet);
            result.GetValueOrThrow();
        }

        organization.SmSeats = plan.SecretsManager.BaseSeats + additionalSmSeats;
        organization.SmServiceAccounts = plan.SecretsManager.BaseServiceAccount + additionalServiceAccounts;
        organization.UseSecretsManager = true;
        await organizationService.ReplaceAndUpdateCacheAsync(organization);

        // TODO: call ReferenceEventService - see AC-1481
    }

    private static void ValidateOrganization(Organization organization, StaticStorePlan plan, Provider? provider)
    {
        if (organization.UseSecretsManager)
        {
            throw new BadRequestException("Organization already uses Secrets Manager.");
        }

        if (!plan.SupportsSecretsManager)
        {
            throw new BadRequestException("Organization's plan does not support Secrets Manager.");
        }

        if (plan.ProductTier != ProductTierType.Free)
        {
            if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
            {
                throw new ConflictException("No payment method found.");
            }

            if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
            {
                throw new ConflictException("No subscription found.");
            }
        }

        if (provider is { Type: ProviderType.Msp })
        {
            throw new BadRequestException("Organizations with a Managed Service Provider do not support Secrets Manager.");
        }
    }

    private static void ValidateSecretsManagerPlan(
        StaticStorePlan plan,
        int additionalSmSeats,
        int additionalServiceAccounts,
        Organization organization)
    {
        if (additionalSmSeats < 0)
        {
            throw new BadRequestException("You can't add Secrets Manager with a negative number of seats.");
        }

        // All paid SM plans have BaseSeats = 0, so at least one additional seat is required.
        if (plan.ProductTier != ProductTierType.Free && additionalSmSeats <= 0)
        {
            throw new BadRequestException("At least one Secrets Manager seat is required.");
        }

        if (!plan.SecretsManager.HasAdditionalServiceAccountOption && additionalServiceAccounts > 0)
        {
            throw new BadRequestException("Plan does not allow additional Machine Accounts.");
        }

        if (additionalSmSeats > organization.Seats.GetValueOrDefault())
        {
            throw new BadRequestException("You cannot have more Secrets Manager seats than Password Manager seats.");
        }

        if (additionalServiceAccounts < 0)
        {
            throw new BadRequestException("You cannot add Secrets Manager with a negative number of Machine Accounts.");
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
    }
}
