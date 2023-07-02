using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSmSubscription.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationSmSubscription;

public class SubscribeOrganziationSmCommand : ISubscribeOrganziationSmCommand
{
    private readonly IGetOrganizationQuery _getOrganizationQuery;
    private readonly ISecretsManagerPlanValidation _secretsManagerPlanValidation;
    private readonly IPaymentService _paymentService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly ICurrentContext _currentContext;
    private readonly IReferenceEventService _referenceEventService;
    
    public SubscribeOrganziationSmCommand(
        IGetOrganizationQuery organizationQuery,
        ISecretsManagerPlanValidation secretsManagerPlanValidation,
        IPaymentService paymentService,
        IOrganizationRepository organizationRepository,
        IApplicationCacheService applicationCacheService,
        ICurrentContext currentContext,
        IReferenceEventService referenceEventService)
    {
        _getOrganizationQuery = organizationQuery;
        _secretsManagerPlanValidation = secretsManagerPlanValidation;
        _paymentService = paymentService;
        _organizationRepository = organizationRepository;
        _applicationCacheService = applicationCacheService;
        _currentContext = currentContext;
        _referenceEventService = referenceEventService;
    }
    public async Task<Tuple<bool, string>> SignUpAsync(Guid organizationId, int additionalSeats,
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
        
        _secretsManagerPlanValidation.ValidateSecretsManagerPlan(plan,organization,additionalSeats,additionalServiceAccounts);
        
        string paymentIntentClientSecret = null;
        var success = true;
        if (string.IsNullOrWhiteSpace(organization.GatewaySubscriptionId))
        {
            
        }
        else
        {
            paymentIntentClientSecret = await  _paymentService.AddSecretsManagerToExistingSubscription(organization, plan, additionalSeats,
                additionalServiceAccounts);
            success = string.IsNullOrWhiteSpace(paymentIntentClientSecret);
        }

        organization.SmSeats = additionalSeats;
        organization.SmServiceAccounts = additionalServiceAccounts;
        organization.UseSecretsManager = true;

        await _organizationRepository.ReplaceAsync(organization);
        await _applicationCacheService.UpsertOrganizationAbilityAsync(organization);

        if (success)
        {
            if (plan != null)
            {
                await _referenceEventService.RaiseEventAsync(
                    new ReferenceEvent(ReferenceEventType.AddSmToExistingSubscription, organization, _currentContext)
                    {
                        PlanName = plan.Name,
                        PlanType = plan.Type,
                        SmSeats = organization.SmSeats,
                        ServiceAccounts = organization.SmServiceAccounts,
                        UseSecretsManager = organization.UseSecretsManager
                    });
            }
        }

        return new Tuple<bool, string>(success, paymentIntentClientSecret);
        
    }
    
}
