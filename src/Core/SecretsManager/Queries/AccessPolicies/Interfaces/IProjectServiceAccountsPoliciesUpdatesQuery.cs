#nullable enable
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Core.SecretsManager.Queries.AccessPolicies.Interfaces;

public interface IProjectServiceAccountsPoliciesUpdatesQuery
{
    Task<ProjectServiceAccountsPoliciesUpdates> GetAsync(ProjectServiceAccountsAccessPolicies grantedPolicies);
}
