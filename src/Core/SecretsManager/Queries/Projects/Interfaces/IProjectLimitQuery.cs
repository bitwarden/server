namespace Bit.Core.SecretsManager.Queries.Projects.Interfaces;

public interface IProjectLimitQuery
{
    Task<(short? limit, bool? overLimit)> GetByOrgIdAsync(Guid organizationId);
}
