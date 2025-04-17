﻿using Bit.Core.Billing.Enums;
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

        // TODO: PRICING -> https://bitwarden.atlassian.net/browse/PM-17122
        var plan = StaticStore.GetPlan(org.PlanType);
        if (plan?.SecretsManager == null)
        {
            throw new BadRequestException("Existing plan not found.");
        }

        if (plan.Type == PlanType.Free)
        {
            var projects = await _projectRepository.GetProjectCountByOrganizationIdAsync(organizationId);
            return ((short? max, bool? overMax))(projects + projectsToAdd > plan.SecretsManager.MaxProjects ? (plan.SecretsManager.MaxProjects, true) : (plan.SecretsManager.MaxProjects, false));
        }

        return (null, null);
    }
}
