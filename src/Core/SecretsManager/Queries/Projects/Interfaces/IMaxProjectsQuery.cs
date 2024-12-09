namespace Bit.Core.SecretsManager.Queries.Projects.Interfaces;

public interface IMaxProjectsQuery
{
    Task<(short? max, bool? overMax)> GetByOrgIdAsync(Guid organizationId, int projectsToAdd);
}
