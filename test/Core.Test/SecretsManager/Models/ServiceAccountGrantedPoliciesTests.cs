#nullable enable
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Enums.AccessPolicies;
using Bit.Core.SecretsManager.Models.Data;
using Xunit;

namespace Bit.Core.Test.SecretsManager.Models;

public class ServiceAccountGrantedPoliciesTests
{
    [Fact]
    public void GetPolicyUpdates_NoChanges_ReturnsEmptyLists()
    {
        var projectId1 = Guid.NewGuid();
        var projectId2 = Guid.NewGuid();

        var existing = new ServiceAccountGrantedPolicies
        {
            ProjectGrantedPolicies = new List<ServiceAccountProjectAccessPolicy>
            {
                new() { GrantedProjectId = projectId1, Read = true, Write = true },
                new() { GrantedProjectId = projectId2, Read = false, Write = true }
            }
        };

        var result = existing.GetPolicyUpdates(existing);

        Assert.Empty(result.ProjectGrantedPolicyUpdates);
    }

    [Fact]
    public void GetPolicyUpdates_ReturnsCorrectPolicyChanges()
    {
        var projectId1 = Guid.NewGuid();
        var projectId2 = Guid.NewGuid();
        var projectId3 = Guid.NewGuid();
        var projectId4 = Guid.NewGuid();

        var existing = new ServiceAccountGrantedPolicies
        {
            ProjectGrantedPolicies = new List<ServiceAccountProjectAccessPolicy>
            {
                new() { GrantedProjectId = projectId1, Read = true, Write = true },
                new() { GrantedProjectId = projectId3, Read = true, Write = true },
                new() { GrantedProjectId = projectId4, Read = true, Write = true }
            }
        };

        var requested = new ServiceAccountGrantedPolicies
        {
            ProjectGrantedPolicies = new List<ServiceAccountProjectAccessPolicy>
            {
                new() { GrantedProjectId = projectId1, Read = true, Write = false },
                new() { GrantedProjectId = projectId2, Read = false, Write = true },
                new() { GrantedProjectId = projectId3, Read = true, Write = true }
            }
        };


        var result = existing.GetPolicyUpdates(requested);

        Assert.Contains(projectId2, result.ProjectGrantedPolicyUpdates
            .Where(pu => pu.Operation == AccessPolicyOperation.Create)
            .Select(pu => pu.AccessPolicy.GrantedProjectId!.Value));

        Assert.Contains(projectId4, result.ProjectGrantedPolicyUpdates
            .Where(pu => pu.Operation == AccessPolicyOperation.Delete)
            .Select(pu => pu.AccessPolicy.GrantedProjectId!.Value));

        Assert.Contains(projectId1, result.ProjectGrantedPolicyUpdates
            .Where(pu => pu.Operation == AccessPolicyOperation.Update)
            .Select(pu => pu.AccessPolicy.GrantedProjectId!.Value));

        Assert.DoesNotContain(projectId3, result.ProjectGrantedPolicyUpdates
            .Select(pu => pu.AccessPolicy.GrantedProjectId!.Value));
    }
}
