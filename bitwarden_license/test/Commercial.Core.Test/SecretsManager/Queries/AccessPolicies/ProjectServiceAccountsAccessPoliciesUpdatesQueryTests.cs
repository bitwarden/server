#nullable enable
using Bit.Commercial.Core.SecretsManager.Queries.AccessPolicies;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Enums.AccessPolicies;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Queries.AccessPolicies;

[SutProviderCustomize]
[ProjectCustomize]
public class ProjectServiceAccountsAccessPoliciesUpdatesQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task GetAsync_NoCurrentAccessPolicies_ReturnsAllCreates(
        SutProvider<ProjectServiceAccountsAccessPoliciesUpdatesQuery> sutProvider,
        ProjectServiceAccountsAccessPolicies data)
    {
        sutProvider.GetDependency<IAccessPolicyRepository>()
            .GetProjectServiceAccountsAccessPoliciesAsync(data.ProjectId)
            .ReturnsNullForAnyArgs();

        var result = await sutProvider.Sut.GetAsync(data);

        Assert.Equal(data.ProjectId, result.ProjectId);
        Assert.Equal(data.OrganizationId, result.OrganizationId);
        Assert.Equal(data.ServiceAccountAccessPolicies.Count(), result.ServiceAccountAccessPolicyUpdates.Count());
        Assert.All(result.ServiceAccountAccessPolicyUpdates, p =>
        {
            Assert.Equal(AccessPolicyOperation.Create, p.Operation);
            Assert.Contains(data.ServiceAccountAccessPolicies, x => x == p.AccessPolicy);
        });
    }

    [Theory]
    [BitAutoData]
    public async Task GetAsync_CurrentAccessPolicies_ReturnsChanges(
        SutProvider<ProjectServiceAccountsAccessPoliciesUpdatesQuery> sutProvider,
        ProjectServiceAccountsAccessPolicies data, ServiceAccountProjectAccessPolicy currentPolicyToDelete)
    {
        foreach (var policy in data.ServiceAccountAccessPolicies)
        {
            policy.GrantedProjectId = data.ProjectId;
        }

        currentPolicyToDelete.GrantedProjectId = data.ProjectId;

        var updatePolicy = new ServiceAccountProjectAccessPolicy
        {
            ServiceAccountId = data.ServiceAccountAccessPolicies.First().ServiceAccountId,
            GrantedProjectId = data.ProjectId,
            Read = !data.ServiceAccountAccessPolicies.First().Read,
            Write = !data.ServiceAccountAccessPolicies.First().Write
        };

        var currentPolicies = new ProjectServiceAccountsAccessPolicies
        {
            ProjectId = data.ProjectId,
            OrganizationId = data.OrganizationId,
            ServiceAccountAccessPolicies = [updatePolicy, currentPolicyToDelete]
        };

        sutProvider.GetDependency<IAccessPolicyRepository>()
            .GetProjectServiceAccountsAccessPoliciesAsync(data.ProjectId)
            .ReturnsForAnyArgs(currentPolicies);

        var result = await sutProvider.Sut.GetAsync(data);

        Assert.Equal(data.ProjectId, result.ProjectId);
        Assert.Equal(data.OrganizationId, result.OrganizationId);
        Assert.Single(result.ServiceAccountAccessPolicyUpdates.Where(x =>
            x.Operation == AccessPolicyOperation.Delete && x.AccessPolicy == currentPolicyToDelete));
        Assert.Single(result.ServiceAccountAccessPolicyUpdates.Where(x =>
            x.Operation == AccessPolicyOperation.Update &&
            x.AccessPolicy.GrantedProjectId == updatePolicy.GrantedProjectId));
        Assert.Equal(result.ServiceAccountAccessPolicyUpdates.Count() - 2,
            result.ServiceAccountAccessPolicyUpdates.Count(x => x.Operation == AccessPolicyOperation.Create));
    }
}
