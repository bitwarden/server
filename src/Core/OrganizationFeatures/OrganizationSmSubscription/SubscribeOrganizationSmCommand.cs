using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSmSubscription.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationSmSubscription;

public class SubscribeOrganizationSmCommand : ISubscribeOrganziationSmCommand
{
    private readonly IGetOrganizationQuery _getOrganizationQuery;
    private readonly ISecretsManagerPlanValidation _secretsManagerPlanValidation;
    private readonly IPaymentService _paymentService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ICurrentContext _currentContext;
    private readonly IReferenceEventService _referenceEventService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationService _organizationService;

    public SubscribeOrganizationSmCommand(
        IGetOrganizationQuery organizationQuery,
        ISecretsManagerPlanValidation secretsManagerPlanValidation,
        IPaymentService paymentService,
        IOrganizationRepository organizationRepository,
        IOrganizationService organizationService,
        ICurrentContext currentContext,
        IReferenceEventService referenceEventService,
        IOrganizationUserRepository organizationUserRepository)
    {
        _getOrganizationQuery = organizationQuery;
        _secretsManagerPlanValidation = secretsManagerPlanValidation;
        _paymentService = paymentService;
        _organizationRepository = organizationRepository;
        _organizationService = organizationService;
        _currentContext = currentContext;
        _referenceEventService = referenceEventService;
        _organizationUserRepository = organizationUserRepository;
    }
    public async Task<Tuple<Organization, OrganizationUser>> SignUpAsync(Guid organizationId, int additionalSeats,
        int additionalServiceAccounts)
    {
        var organization = await _getOrganizationQuery.GetOrgById(organizationId);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        if (string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
        {
            throw new GatewayException("Not a gateway customer.");
        }

        var plan = StaticStore.SecretManagerPlans.FirstOrDefault(p => p.Type == organization.PlanType);

        _secretsManagerPlanValidation.ValidateSecretsManagerPlan(plan, organization, additionalSeats, additionalServiceAccounts);

        if (plan.Type != PlanType.Free)
        {
            await _paymentService.AddSecretsManagerToSubscription(organization, plan, additionalSeats, additionalServiceAccounts);
        }

        organization.SmSeats = additionalSeats;
        organization.SmServiceAccounts = additionalServiceAccounts;
        organization.UseSecretsManager = true;
        var returnValue = await SignUpAsync(organization);

        await _referenceEventService.RaiseEventAsync(
            new ReferenceEvent(ReferenceEventType.AddSmToExistingSubscription, organization, _currentContext)
            {
                Id = organization.Id,
                PlanName = plan.Name,
                PlanType = plan.Type,
                SmSeats = organization.SmSeats,
                ServiceAccounts = organization.SmServiceAccounts,
                UseSecretsManager = organization.UseSecretsManager
            });

        return returnValue;

    }

    private async Task<Tuple<Organization, OrganizationUser>> SignUpAsync(Organization organization)
    {
        try
        {
            await _organizationRepository.ReplaceAsync(organization);

            OrganizationUser orgUser = null;
            var ownerUsers =
                await _organizationUserRepository.GetManyByOrganizationAsync(organization.Id,
                    OrganizationUserType.Owner);

            if (ownerUsers.Count > 0)
            {
                orgUser = ownerUsers.FirstOrDefault(x => x.Type == OrganizationUserType.Owner);
                if (orgUser != null)
                {
                    orgUser.AccessSecretsManager = true;
                }

                await _organizationUserRepository.ReplaceAsync(orgUser);
            }

            return new Tuple<Organization, OrganizationUser>(organization, orgUser);
        }
        catch
        {
            if (organization.Id != default(Guid))
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
