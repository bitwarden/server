using Bit.Core.Entities;
using Bit.Core.Enums;
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
    private readonly IOrganizationUserRepository _organizationUserRepository;
    public AddSecretsManagerSubscriptionCommand(
        IPaymentService paymentService,
        IOrganizationService organizationService,
        IOrganizationUserRepository organizationUserRepository)
    {
        _paymentService = paymentService;
        _organizationService = organizationService;
        _organizationUserRepository = organizationUserRepository;
    }
    public async Task<Organization> SignUpAsync(Organization organization, int additionalSmSeats,
        int additionalServiceAccounts)
    {
        ValidateOrganization(organization);

        var plan = StaticStore.GetSecretsManagerPlan(organization.PlanType);
        var signup = SetOrganizationUpgrade(organization, additionalSmSeats, additionalServiceAccounts);
        _organizationService.ValidateSecretsManagerPlan(plan, signup);

        if (plan.Type != PlanType.Free)
        {
            await _paymentService.AddSecretsManagerToSubscription(organization, plan, additionalSmSeats, additionalServiceAccounts);
        }

        organization.SmSeats = plan.BaseSeats + additionalSmSeats;
        organization.SmServiceAccounts = plan.BaseServiceAccount.GetValueOrDefault() + additionalServiceAccounts;
        organization.UseSecretsManager = true;

        await _organizationService.ReplaceAndUpdateCacheAsync(organization);

        // TODO: call ReferenceEventService - see AC-1481

        return organization;
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

        if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId) && organization.PlanType != PlanType.Free)
        {
            throw new BadRequestException("No payment method found.");
        }

        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId) && organization.PlanType != PlanType.Free)
        {
            throw new BadRequestException("No subscription found.");
        }
    }
}
