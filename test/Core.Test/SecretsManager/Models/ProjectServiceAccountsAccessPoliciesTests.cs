#nullable enable
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Enums.AccessPolicies;
using Bit.Core.SecretsManager.Models.Data;
using Xunit;

namespace Bit.Core.Test.SecretsManager.Models;

public class ProjectServiceAccountsAccessPoliciesTests
{
    [Fact]
    public void GetPolicyUpdates_NoChanges_ReturnsEmptyList()
    {
        var serviceAccountId1 = Guid.NewGuid();
        var serviceAccountId2 = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var existing = new ProjectServiceAccountsAccessPolicies
        {
            ServiceAccountAccessPolicies = new List<ServiceAccountProjectAccessPolicy>
            {
                new() { ServiceAccountId = serviceAccountId1, GrantedProjectId = projectId, Read = true, Write = true },
                new() {  ServiceAccountId = serviceAccountId2, GrantedProjectId = projectId, Read = false, Write = true }
            }
        };

        var result = existing.GetPolicyUpdates(existing);

        Assert.Empty(result.ServiceAccountAccessPolicyUpdates);
    }

    [Fact]
    public void GetPolicyUpdates_ReturnsCorrectPolicyChanges()
    {
        var serviceAccountId1 = Guid.NewGuid();
        var serviceAccountId2 = Guid.NewGuid();
        var serviceAccountId3 = Guid.NewGuid();
        var serviceAccountId4 = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var existing = new ProjectServiceAccountsAccessPolicies
        {
            ServiceAccountAccessPolicies = new List<ServiceAccountProjectAccessPolicy>
            {
                new() { ServiceAccountId = serviceAccountId1, GrantedProjectId = projectId, Read = true, Write = true },
                new() { ServiceAccountId = serviceAccountId3, GrantedProjectId = projectId, Read = true, Write = true },
                new() { ServiceAccountId = serviceAccountId4, GrantedProjectId = projectId, Read = true, Write = true }
            }
        };

        var requested = new ProjectServiceAccountsAccessPolicies
        {
            ServiceAccountAccessPolicies = new List<ServiceAccountProjectAccessPolicy>
            {
                new() { ServiceAccountId = serviceAccountId1, GrantedProjectId = projectId, Read = true, Write = false },
                new() { ServiceAccountId = serviceAccountId2, GrantedProjectId = projectId, Read = false, Write = true },
                new() { ServiceAccountId = serviceAccountId3, GrantedProjectId = projectId, Read = true, Write = true }
            }
        };


        var result = existing.GetPolicyUpdates(requested);

        Assert.Contains(serviceAccountId2, result.ServiceAccountAccessPolicyUpdates
            .Where(pu => pu.Operation == AccessPolicyOperation.Create)
            .Select(pu => pu.AccessPolicy.ServiceAccountId!.Value));

        Assert.Contains(serviceAccountId4, result.ServiceAccountAccessPolicyUpdates
            .Where(pu => pu.Operation == AccessPolicyOperation.Delete)
            .Select(pu => pu.AccessPolicy.ServiceAccountId!.Value));

        Assert.Contains(serviceAccountId1, result.ServiceAccountAccessPolicyUpdates
            .Where(pu => pu.Operation == AccessPolicyOperation.Update)
            .Select(pu => pu.AccessPolicy.ServiceAccountId!.Value));

        Assert.DoesNotContain(serviceAccountId3, result.ServiceAccountAccessPolicyUpdates
            .Select(pu => pu.AccessPolicy.ServiceAccountId!.Value));
    }
}
