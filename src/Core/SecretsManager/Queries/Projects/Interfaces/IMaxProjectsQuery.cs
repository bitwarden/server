namespace Bit.Core.SecretsManager.Queries.Projects.Interfaces;

public interface IMaxProjectsQuery
{
    Task<(short? max, bool? atMax)> GetByOrgIdAsync(Guid organizationId);
}
