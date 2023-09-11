using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Queries.Projects.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Utilities;

namespace Bit.Commercial.Core.SecretsManager.Queries.Projects;

public class MaxProjectsQuery : IMaxProjectsQuery
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;

    public MaxProjectsQuery(
        IOrganizationRepository organizationRepository,
        IProjectRepository projectRepository)
    {
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
    }

    public async Task<(short? max, bool? overMax)> GetByOrgIdAsync(Guid organizationId, int projectsToAdd)
    {
        var org = await _organizationRepository.GetByIdAsync(organizationId);
        if (org == null)
        {
            throw new NotFoundException();
        }

        var plan = StaticStore.GetSecretsManagerPlan(org.PlanType);
        if (plan == null)
        {
            throw new BadRequestException("Existing plan not found.");
        }

        if (plan.Type == PlanType.Free)
        {
            var projects = await _projectRepository.GetProjectCountByOrganizationIdAsync(organizationId);
            return projects + projectsToAdd > plan.MaxProjects ? (plan.MaxProjects, true) : (plan.MaxProjects, false);
        }

        return (null, null);
    }
}
