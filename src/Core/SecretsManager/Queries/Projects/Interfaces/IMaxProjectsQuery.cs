namespace Bit.Core.SecretsManager.Queries.Projects.Interfaces;

#nullable enable

public interface IMaxProjectsQuery
{
    Task<(short? max, bool? overMax)> GetByOrgIdAsync(Guid organizationId, int projectsToAdd);
}
