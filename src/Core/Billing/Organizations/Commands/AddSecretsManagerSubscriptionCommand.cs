using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
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
            throw new BadRequestException(new OrganizationAlreadyUsesSecretsManagerError().Message);
        }

        if (!plan.SupportsSecretsManager)
        {
            throw new BadRequestException(new OrganizationPlanDoesNotSupportSecretsManagerError().Message);
        }

        if (plan.ProductTier != ProductTierType.Free)
        {
            if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
            {
                throw new ConflictException(new SecretsManagerPaymentMethodNotFoundError().Message);
            }

            if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
            {
                throw new ConflictException(new SecretsManagerSubscriptionNotFoundError().Message);
            }
        }

        if (provider is { Type: ProviderType.Msp })
        {
            throw new BadRequestException(new SecretsManagerMspUnsupportedError().Message);
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
            throw new BadRequestException(new CannotAddSecretsManagerWithNegativeSeatsError().Message);
        }

        // All paid SM plans have BaseSeats = 0, so at least one additional seat is required.
        if (plan.ProductTier != ProductTierType.Free && additionalSmSeats <= 0)
        {
            throw new BadRequestException(new AtLeastOneSecretsManagerSeatRequiredError().Message);
        }

        if (!plan.SecretsManager.HasAdditionalServiceAccountOption && additionalServiceAccounts > 0)
        {
            throw new BadRequestException(new PlanDoesNotAllowAdditionalMachineAccountsError().Message);
        }

        if (additionalSmSeats > organization.Seats.GetValueOrDefault())
        {
            throw new BadRequestException(new SecretsManagerSeatsMustNotExceedPasswordManagerSeatsError().Message);
        }

        if (additionalServiceAccounts < 0)
        {
            throw new BadRequestException(new CannotAddSecretsManagerWithNegativeMachineAccountsError().Message);
        }

        if (!plan.SecretsManager.HasAdditionalSeatsOption && additionalSmSeats > 0)
        {
            throw new BadRequestException(new PlanDoesNotAllowAdditionalUsersError().Message);
        }

        if (plan.SecretsManager.MaxAdditionalSeats.HasValue &&
            additionalSmSeats > plan.SecretsManager.MaxAdditionalSeats.Value)
        {
            throw new BadRequestException(new PlanMaxAdditionalUsersExceededError(plan.SecretsManager.MaxAdditionalSeats.GetValueOrDefault(0)).Message);
        }
    }
}
