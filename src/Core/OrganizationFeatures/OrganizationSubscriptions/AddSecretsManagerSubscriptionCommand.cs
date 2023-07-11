using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
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
    public async Task<Tuple<Organization, OrganizationUser>> SignUpAsync(Organization organization, int additionalSeats,
        int additionalServiceAccounts)
    {
        ValidateOrganization(organization);

        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(p => p.Type == organization.PlanType);

        ValidateSecretsManagerPlan(plan, organization, additionalSeats, additionalServiceAccounts);

        if (plan.Type != PlanType.Free)
        {
            await _paymentService.AddSecretsManagerToSubscription(organization, plan, additionalSeats, additionalServiceAccounts);
        }

        organization.SmSeats = additionalSeats;
        organization.SmServiceAccounts = additionalServiceAccounts;
        organization.UseSecretsManager = true;
        var returnValue = await SignUpAsync(organization);

        // TODO: call ReferenceEventService - see AC-1481

        return returnValue;
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

    private void ValidateSecretsManagerPlan(Plan plan, Organization signup, int additionalSeats, int additionalServiceAccounts)
    {
        if (plan is not { LegacyYear: null })
        {
            throw new BadRequestException($"Invalid Secrets Manager plan selected.");
        }

        if (plan.Disabled)
        {
            throw new BadRequestException($"Secrets Manager Plan is disabled.");
        }

        if (plan.BaseSeats + additionalSeats <= 0)
        {
            throw new BadRequestException($"You do not have any Secrets Manager seats!");
        }

        if (additionalSeats < 0)
        {
            throw new BadRequestException($"You can't subtract Secrets Manager seats!");
        }

        if (!plan.HasAdditionalServiceAccountOption && additionalServiceAccounts > 0)
        {
            throw new BadRequestException("Plan does not allow additional Service Accounts.");
        }

        if (additionalSeats > signup.Seats)
        {
            throw new BadRequestException("You cannot have more Secrets Manager seats than Password Manager seats.");
        }

        if (additionalServiceAccounts < 0)
        {
            throw new BadRequestException("You can't subtract Service Accounts!");
        }

        switch (plan.HasAdditionalSeatsOption)
        {
            case false when additionalSeats > 0:
                throw new BadRequestException("Plan does not allow additional users.");
            case true when plan.MaxAdditionalSeats.HasValue &&
                           additionalSeats > plan.MaxAdditionalSeats.Value:
                throw new BadRequestException($"Selected plan allows a maximum of " +
                                              $"{plan.MaxAdditionalSeats.GetValueOrDefault(0)} additional users.");
        }
    }

    private async Task<Tuple<Organization, OrganizationUser>> SignUpAsync(Organization organization)
    {
        try
        {
            await _organizationService.ReplaceAndUpdateCacheAsync(organization);

            OrganizationUser orgUser = null;
            var ownerUsers =
                await _organizationUserRepository.GetManyByOrganizationAsync(organization.Id,
                    OrganizationUserType.Owner);

            if (ownerUsers.Any())
            {
                foreach (var onwerUser in ownerUsers)
                {
                    onwerUser.AccessSecretsManager = true;
                }
                
                await _organizationUserRepository.ReplaceManyAsync(ownerUsers);
            }

            return new Tuple<Organization, OrganizationUser>(organization, orgUser);
        }
        catch
        {
            if (organization.Id != default)
            {
                organization.SmSeats = 0;
                organization.SmServiceAccounts = 0;
                organization.UseSecretsManager = false;
                await _organizationRepository.ReplaceAsync(organization);
            }

            throw;
        }
    }
}
