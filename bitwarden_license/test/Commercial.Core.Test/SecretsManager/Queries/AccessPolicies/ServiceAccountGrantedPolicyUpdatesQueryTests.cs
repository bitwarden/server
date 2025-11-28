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
public class ServiceAccountGrantedPolicyUpdatesQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task GetAsync_NoCurrentGrantedPolicies_ReturnsAllCreates(
        SutProvider<ServiceAccountGrantedPolicyUpdatesQuery> sutProvider,
        ServiceAccountGrantedPolicies data)
    {
        sutProvider.GetDependency<IAccessPolicyRepository>()
            .GetServiceAccountGrantedPoliciesAsync(data.ServiceAccountId)
            .ReturnsNullForAnyArgs();

        var result = await sutProvider.Sut.GetAsync(data);

        Assert.Equal(data.ServiceAccountId, result.ServiceAccountId);
        Assert.Equal(data.OrganizationId, result.OrganizationId);
        Assert.Equal(data.ProjectGrantedPolicies.Count(), result.ProjectGrantedPolicyUpdates.Count());
        Assert.All(result.ProjectGrantedPolicyUpdates, p =>
        {
            Assert.Equal(AccessPolicyOperation.Create, p.Operation);
            Assert.Contains(data.ProjectGrantedPolicies, x => x == p.AccessPolicy);
        });
    }

    [Theory]
    [BitAutoData]
    public async Task GetAsync_CurrentGrantedPolicies_ReturnsChanges(
        SutProvider<ServiceAccountGrantedPolicyUpdatesQuery> sutProvider,
        ServiceAccountGrantedPolicies data, ServiceAccountProjectAccessPolicy currentPolicyToDelete)
    {
        foreach (var grantedPolicy in data.ProjectGrantedPolicies)
        {
            grantedPolicy.ServiceAccountId = data.ServiceAccountId;
        }

        currentPolicyToDelete.ServiceAccountId = data.ServiceAccountId;

        var updatePolicy = new ServiceAccountProjectAccessPolicy
        {
            ServiceAccountId = data.ServiceAccountId,
            GrantedProjectId = data.ProjectGrantedPolicies.First().GrantedProjectId,
            Read = !data.ProjectGrantedPolicies.First().Read,
            Write = !data.ProjectGrantedPolicies.First().Write
        };

        var currentPolicies = new ServiceAccountGrantedPolicies
        {
            ServiceAccountId = data.ServiceAccountId,
            OrganizationId = data.OrganizationId,
            ProjectGrantedPolicies = [updatePolicy, currentPolicyToDelete]
        };

        sutProvider.GetDependency<IAccessPolicyRepository>()
            .GetServiceAccountGrantedPoliciesAsync(data.ServiceAccountId)
            .ReturnsForAnyArgs(currentPolicies);

        var result = await sutProvider.Sut.GetAsync(data);

        Assert.Equal(data.ServiceAccountId, result.ServiceAccountId);
        Assert.Equal(data.OrganizationId, result.OrganizationId);
        Assert.Single(result.ProjectGrantedPolicyUpdates.Where(x =>
            x.Operation == AccessPolicyOperation.Delete && x.AccessPolicy == currentPolicyToDelete));
        Assert.Single(result.ProjectGrantedPolicyUpdates.Where(x =>
            x.Operation == AccessPolicyOperation.Update &&
            x.AccessPolicy.GrantedProjectId == updatePolicy.GrantedProjectId));
        Assert.Equal(result.ProjectGrantedPolicyUpdates.Count() - 2,
            result.ProjectGrantedPolicyUpdates.Count(x => x.Operation == AccessPolicyOperation.Create));
    }
}
