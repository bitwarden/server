using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSmSubscription.Interface;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationSmSubscription;

public class SubscribeOrganziationSmCommand : ISubscribeOrganziationSmCommand
{
    private readonly IGetOrganizationQuery _getOrganizationQuery;
    private readonly ISecretsManagerPlanValidation _secretsManagerPlanValidation;
    
    public SubscribeOrganziationSmCommand(
        IGetOrganizationQuery organizationQuery,
        ISecretsManagerPlanValidation secretsManagerPlanValidation)
    {
        _getOrganizationQuery = organizationQuery;
        _secretsManagerPlanValidation = secretsManagerPlanValidation;
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
        
        _secretsManagerPlanValidation.ValidateSecretsManagerPlan(plan,organization,additionalSeats,additionalServiceAccounts);
    }
    
}
