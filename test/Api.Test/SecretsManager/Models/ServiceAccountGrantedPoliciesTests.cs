using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Xunit;

namespace Bit.Api.Test.SecretsManager.Models;

public class ServiceAccountGrantedPoliciesTests
{
    [Fact]
    public void GetPolicyChanges_NoChanges_ReturnsEmptyLists()
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

        var result = existing.GetPolicyChanges(existing);

        Assert.Empty(result.ProjectIdsToCreate);
        Assert.Empty(result.ProjectIdsToDelete);
        Assert.Empty(result.ProjectIdsToUpdate);
    }

    [Fact]
    public void GetPolicyChanges_ReturnsCorrectPolicyChanges()
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


        var result = existing.GetPolicyChanges(requested);

        Assert.Equal(projectId2, result.ProjectIdsToCreate.First());
        Assert.Equal(projectId4, result.ProjectIdsToDelete.First());
        Assert.Equal(projectId1, result.ProjectIdsToUpdate.First());
        Assert.DoesNotContain(projectId3, result.ProjectIdsToCreate);
        Assert.DoesNotContain(projectId3, result.ProjectIdsToDelete);
        Assert.DoesNotContain(projectId3, result.ProjectIdsToUpdate);
    }

    [Fact]
    public void ToBaseAccessPolicies_NoPolicies_ReturnsEmptyLists()
    {
        var sut = new ServiceAccountGrantedPolicies
        {
            ProjectGrantedPolicies = new List<ServiceAccountProjectAccessPolicy>()
        };

        var result = sut.ToBaseAccessPolicies();

        Assert.Empty(result);
    }

    [Fact]
    public void ToBaseAccessPolicies_Policies_ReturnsBasePolicies()
    {
        var accessPolicyId = new Guid();
        var accessPolicyId2 = new Guid();

        var sut = new ServiceAccountGrantedPolicies
        {
            ProjectGrantedPolicies = new List<ServiceAccountProjectAccessPolicy>
            {
                new() { Id = accessPolicyId, GrantedProjectId = new Guid(), Read = true, Write = false },
                new() { Id = accessPolicyId2, GrantedProjectId = new Guid(), Read = false, Write = true },
            }
        };


        var result = sut.ToBaseAccessPolicies().ToList();

        Assert.NotEmpty(result);
        Assert.Contains(accessPolicyId, result.Select(x => x.Id));
        Assert.Contains(accessPolicyId2, result.Select(x => x.Id));
    }
}
