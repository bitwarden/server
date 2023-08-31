using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Enums.Provider;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
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

        var plan = StaticStore.GetSecretsManagerPlan(organization.PlanType);
        var signup = SetOrganizationUpgrade(organization, additionalSmSeats, additionalServiceAccounts);
        _organizationService.ValidateSecretsManagerPlan(plan, signup);

        if (plan.Product != ProductType.Free)
        {
            await _paymentService.AddSecretsManagerToSubscription(organization, plan, additionalSmSeats, additionalServiceAccounts);
        }

        organization.SmSeats = plan.BaseSeats + additionalSmSeats;
        organization.SmServiceAccounts = plan.BaseServiceAccount.GetValueOrDefault() + additionalServiceAccounts;
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

        if (organization.SecretsManagerBeta)
        {
            throw new BadRequestException("Organization is enrolled in Secrets Manager Beta. " +
                                          "Please contact Customer Success to add Secrets Manager to your subscription.");
        }

        if (organization.UseSecretsManager)
        {
            throw new BadRequestException("Organization already uses Secrets Manager.");
        }

        var plan = StaticStore.GetSecretsManagerPlan(organization.PlanType);
        if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId) && plan.Product != ProductType.Free)
        {
            throw new BadRequestException("No payment method found.");
        }

        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId) && plan.Product != ProductType.Free)
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
