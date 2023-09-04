using Bit.Core.Entities;
using Bit.Core.Enums;
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
    public AddSecretsManagerSubscriptionCommand(
        IPaymentService paymentService,
        IOrganizationService organizationService)
    {
        _paymentService = paymentService;
        _organizationService = organizationService;
    }
    public async Task SignUpAsync(Organization organization, int additionalSmSeats,
        int additionalServiceAccounts)
    {
        ValidateOrganization(organization);

        var plan = StaticStore.Plans.FirstOrDefault(p => p.Type == organization.PlanType && p.SupportsSecretsManager);
        var signup = SetOrganizationUpgrade(organization, additionalSmSeats, additionalServiceAccounts);
        _organizationService.ValidateSecretsManagerPlan(plan, signup);

        if (plan.Product != ProductType.Free)
        {
            await _paymentService.AddSecretsManagerToSubscription(organization, plan, additionalSmSeats, additionalServiceAccounts);
        }

        organization.SmSeats = plan.SecretsManager.BaseSeats + additionalSmSeats;
        organization.SmServiceAccounts = plan.SecretsManager.BaseServiceAccount.GetValueOrDefault() + additionalServiceAccounts;
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

    private static void ValidateOrganization(Organization organization)
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

        if (organization.SecretsManagerBeta)
        {
            throw new BadRequestException("Organization is enrolled in Secrets Manager Beta. " +
                                          "Please contact Customer Success to add Secrets Manager to your subscription.");
        }

        if (organization.UseSecretsManager)
        {
            throw new BadRequestException("Organization already uses Secrets Manager.");
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

        if (organization.SecretsManagerBeta)
        {
            throw new BadRequestException("Organization is enrolled in Secrets Manager Beta. " +
                                          "Please contact Customer Success to add Secrets Manager to your subscription.");
        }

        if (organization.UseSecretsManager)
        {
            throw new BadRequestException("Organization already uses Secrets Manager.");
        }

        var plan = StaticStore.Plans.FirstOrDefault(p => p.Type == organization.PlanType && p.SupportsSecretsManager);
        if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId) && plan.Product != ProductType.Free)
        {
            throw new BadRequestException("No payment method found.");
        }

        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId) && plan.Product != ProductType.Free)
        {
            throw new BadRequestException("No subscription found.");
        }
    }
}
