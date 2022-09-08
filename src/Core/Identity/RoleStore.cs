using Bit.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Identity;

public class RoleStore : IRoleStore<Role>
{
    public void Dispose() { }

    public Task<IdentityResult> CreateAsync(Role role, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<IdentityResult> DeleteAsync(Role role, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<Role> FindByIdAsync(string roleId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<Role> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetNormalizedRoleNameAsync(Role role, CancellationToken cancellationToken)
    {
        return Task.FromResult(role.Name);
    }

    public Task<string> GetRoleIdAsync(Role role, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetRoleNameAsync(Role role, CancellationToken cancellationToken)
    {
        return Task.FromResult(role.Name);
    }

    public Task SetNormalizedRoleNameAsync(Role role, string normalizedName, CancellationToken cancellationToken)
    {
        return Task.FromResult(0);
    }

    public Task SetRoleNameAsync(Role role, string roleName, CancellationToken cancellationToken)
    {
        role.Name = roleName;
        return Task.FromResult(0);
    }

    public Task<IdentityResult> UpdateAsync(Role role, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
