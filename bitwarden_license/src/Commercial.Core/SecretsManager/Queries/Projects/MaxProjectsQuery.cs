using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Licenses;
using Bit.Core.Billing.Licenses.Extensions;
using Bit.Core.Billing.Pricing;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Queries.Projects.Interfaces;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;

namespace Bit.Commercial.Core.SecretsManager.Queries.Projects;

public class MaxProjectsQuery : IMaxProjectsQuery
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IGlobalSettings _globalSettings;
    private readonly ILicensingService _licensingService;
    private readonly IPricingClient _pricingClient;

    public MaxProjectsQuery(
        IOrganizationRepository organizationRepository,
        IProjectRepository projectRepository,
        IGlobalSettings globalSettings,
        ILicensingService licensingService,
        IPricingClient pricingClient)
    {
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _globalSettings = globalSettings;
        _licensingService = licensingService;
        _pricingClient = pricingClient;
    }

    public async Task<(short? max, bool? overMax)> GetByOrgIdAsync(Guid organizationId, int projectsToAdd)
    {
        var org = await _organizationRepository.GetByIdAsync(organizationId);
        if (org == null)
        {
            throw new NotFoundException();
        }

        var (planType, maxProjects) = await GetPlanTypeAndMaxProjectsAsync(org);

        if (planType == PlanType.Free)
        {
            var projects = await _projectRepository.GetProjectCountByOrganizationIdAsync(organizationId);
            return ((short? max, bool? overMax))(projects + projectsToAdd > maxProjects ? (maxProjects, true) : (maxProjects, false));
        }

        return (null, null);
    }

    private async Task<(PlanType planType, int maxProjects)> GetPlanTypeAndMaxProjectsAsync(Organization organization)
    {
        if (_globalSettings.SelfHosted)
        {
            var license = await _licensingService.ReadOrganizationLicenseAsync(organization);
            var claimsPrincipal = _licensingService.GetClaimsPrincipalFromLicense(license);
            var maxProjects = claimsPrincipal.GetValue<int?>(OrganizationLicenseConstants.SmMaxProjects);

            if (!maxProjects.HasValue)
            {
                throw new BadRequestException("License does not contain a value for max Secrets Manager projects");
            }

            var planType = claimsPrincipal.GetValue<PlanType>(OrganizationLicenseConstants.PlanType);
            return (planType, maxProjects.Value);
        }

        var plan = await _pricingClient.GetPlan(organization.PlanType);

        if (plan is { SupportsSecretsManager: true })
        {
            return (plan.Type, plan.SecretsManager.MaxProjects);
        }

        throw new BadRequestException("Existing plan not found.");
    }
}
