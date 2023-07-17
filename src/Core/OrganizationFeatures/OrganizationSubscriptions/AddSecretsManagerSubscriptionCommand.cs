using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscriptions;

public class AddSecretsManagerSubscriptionCommand : IAddSecretsManagerSubscriptionCommand
{
    private readonly IPaymentService _paymentService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationService _organizationService;
    public AddSecretsManagerSubscriptionCommand(
        IPaymentService paymentService,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationService organizationService)
    {
        _paymentService = paymentService;
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _organizationService = organizationService;
    }
    public async Task<Organization> SignUpAsync(Organization organization, int additionalSeats,
        int additionalServiceAccounts)
    {
        ValidateOrganization(organization);

        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(p => p.Type == organization.PlanType);
        var signup = SetOrganizationUpgrade(organization, additionalSeats, additionalServiceAccounts);
        _organizationService.ValidateSecretsManagerPlan(plan, signup);

        if (plan.Type != PlanType.Free)
        {
            await _paymentService.AddSecretsManagerToSubscription(organization, plan, additionalSeats, additionalServiceAccounts);
        }

        organization.SmSeats = plan.BaseSeats + additionalSeats;
        organization.SmServiceAccounts = plan.BaseServiceAccount + additionalServiceAccounts;
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

        if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
        {
            throw new GatewayException("Not a gateway customer.");
        }

        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
        {
            throw new BadRequestException("No subscription found.");
        }
    }
}
