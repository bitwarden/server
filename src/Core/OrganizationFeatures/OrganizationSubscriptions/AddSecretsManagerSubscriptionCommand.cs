using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Services;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptions;

public class AddSecretsManagerSubscriptionCommand : IAddSecretsManagerSubscriptionCommand
{
    private readonly IPaymentService _paymentService;
    private readonly IOrganizationService _organizationService;
    private readonly IProviderRepository _providerRepository;

    public AddSecretsManagerSubscriptionCommand(
        IPaymentService paymentService,
        IOrganizationService organizationService,
        IProviderRepository providerRepository)
    {
        _paymentService = paymentService;
        _organizationService = organizationService;
        _providerRepository = providerRepository;
    }
    public async Task SignUpAsync(Organization organization, int additionalSmSeats,
        int additionalServiceAccounts)
    {
        await ValidateOrganization(organization);

        var plan = StaticStore.GetPlan(organization.PlanType);
        var signup = SetOrganizationUpgrade(organization, additionalSmSeats, additionalServiceAccounts);
        _organizationService.ValidateSecretsManagerPlan(plan, signup);

        if (plan.ProductTier != ProductTierType.Free)
        {
            await _paymentService.AddSecretsManagerToSubscription(organization, plan, additionalSmSeats, additionalServiceAccounts);
        }

        organization.SmSeats = plan.SecretsManager.BaseSeats + additionalSmSeats;
        organization.SmServiceAccounts = plan.SecretsManager.BaseServiceAccount + additionalServiceAccounts;
        organization.UseSecretsManager = true;

        await _organizationService.ReplaceAndUpdateCacheAsync(organization);

        // TODO: call ReferenceEventService - see AC-1481
    }

    private static OrganizationUpgrade SetOrganizationUpgrade(Organization organization, int additionalSeats,
        int additionalServiceAccounts)
    {
        var signup = new OrganizationUpgrade
        {
            UseSecretsManager = true,
            AdditionalSmSeats = additionalSeats,
            AdditionalServiceAccounts = additionalServiceAccounts,
            AdditionalSeats = organization.Seats.GetValueOrDefault()
        };
        return signup;
    }

    private async Task ValidateOrganization(Organization organization)
    {
        if (organization == null)
        {
            throw new NotFoundException();
        }

        if (organization.UseSecretsManager)
        {
            throw new BadRequestException("Organization already uses Secrets Manager.");
        }

        var plan = StaticStore.Plans.FirstOrDefault(p => p.Type == organization.PlanType && p.SupportsSecretsManager);
        if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId) && plan.ProductTier != ProductTierType.Free)
        {
            throw new BadRequestException("No payment method found.");
        }

        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId) && plan.ProductTier != ProductTierType.Free)
        {
            throw new BadRequestException("No subscription found.");
        }

        var provider = await _providerRepository.GetByOrganizationIdAsync(organization.Id);
        if (provider is { Type: ProviderType.Msp })
        {
            throw new BadRequestException(
                "Organizations with a Managed Service Provider do not support Secrets Manager.");
        }
    }
}
