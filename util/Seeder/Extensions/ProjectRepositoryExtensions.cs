using Bit.Core.SecretsManager.Repositories;

namespace Bit.Seeder.Extensions;

internal static class ProjectRepositoryExtensions
{
    public static async Task ThrowIfProjectsNotInOrganizationAsync(
        this IProjectRepository projectRepository, IEnumerable<Guid>? projectIds, Guid organizationId)
    {
        var ids = projectIds?.ToList();
        if (ids is not { Count: > 0 })
        {
            return;
        }

        if (!await projectRepository.ProjectsAreInOrganization(ids, organizationId))
        {
            throw new InvalidOperationException(
                $"One or more projects are not in organization {organizationId}.");
        }
    }
}
