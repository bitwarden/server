using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Queries.Projects.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Settings;

namespace Bit.Commercial.Core.SecretsManager.Queries.Projects;

public class MaxProjectsQuery : IMaxProjectsQuery
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IGlobalSettings _globalSettings;
    private readonly IPricingClient _pricingClient;

    public MaxProjectsQuery(
        IOrganizationRepository organizationRepository,
        IProjectRepository projectRepository,
        IGlobalSettings globalSettings,
        IPricingClient pricingClient)
    {
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _globalSettings = globalSettings;
        _pricingClient = pricingClient;
    }

    public async Task<(short? max, bool? overMax)> GetByOrgIdAsync(Guid organizationId, int projectsToAdd)
    {
        // "MaxProjects" only applies to free 2-person organizations, which can't be self-hosted.
        if (_globalSettings.SelfHosted)
        {
            return (null, null);
        }

        var org = await _organizationRepository.GetByIdAsync(organizationId);
        if (org == null)
        {
            throw new NotFoundException();
        }

        var plan = await _pricingClient.GetPlan(org.PlanType);

        if (plan is not { SecretsManager: not null, Type: PlanType.Free })
        {
            return (null, null);
        }

        var projects = await _projectRepository.GetProjectCountByOrganizationIdAsync(organizationId);
        return ((short? max, bool? overMax))(projects + projectsToAdd > plan.SecretsManager.MaxProjects ? (plan.SecretsManager.MaxProjects, true) : (plan.SecretsManager.MaxProjects, false));
    }
}
