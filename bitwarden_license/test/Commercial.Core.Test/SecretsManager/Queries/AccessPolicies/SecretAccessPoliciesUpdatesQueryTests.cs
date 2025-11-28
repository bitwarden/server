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
public class SecretAccessPoliciesUpdatesQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task GetAsync_NoCurrentAccessPolicies_ReturnsAllCreates(
        SutProvider<SecretAccessPoliciesUpdatesQuery> sutProvider,
        SecretAccessPolicies data,
        Guid userId)
    {
        sutProvider.GetDependency<IAccessPolicyRepository>()
            .GetSecretAccessPoliciesAsync(data.SecretId, userId)
            .ReturnsNullForAnyArgs();

        var result = await sutProvider.Sut.GetAsync(data, userId);

        Assert.Equal(data.SecretId, result.SecretId);
        Assert.Equal(data.OrganizationId, result.OrganizationId);

        Assert.Equal(data.UserAccessPolicies.Count(), result.UserAccessPolicyUpdates.Count());
        Assert.All(result.UserAccessPolicyUpdates, p =>
        {
            Assert.Equal(AccessPolicyOperation.Create, p.Operation);
            Assert.Contains(data.UserAccessPolicies, x => x == p.AccessPolicy);
        });

        Assert.Equal(data.GroupAccessPolicies.Count(), result.GroupAccessPolicyUpdates.Count());
        Assert.All(result.GroupAccessPolicyUpdates, p =>
        {
            Assert.Equal(AccessPolicyOperation.Create, p.Operation);
            Assert.Contains(data.GroupAccessPolicies, x => x == p.AccessPolicy);
        });

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
        SutProvider<SecretAccessPoliciesUpdatesQuery> sutProvider,
        SecretAccessPolicies data,
        Guid userId,
        UserSecretAccessPolicy userPolicyToDelete,
        GroupSecretAccessPolicy groupPolicyToDelete,
        ServiceAccountSecretAccessPolicy serviceAccountPolicyToDelete)
    {
        data = SetupSecretAccessPolicies(data);
        var userPolicyChanges = SetupUserAccessPolicies(data, userPolicyToDelete);
        var groupPolicyChanges = SetupGroupAccessPolicies(data, groupPolicyToDelete);
        var serviceAccountPolicyChanges = SetupServiceAccountAccessPolicies(data, serviceAccountPolicyToDelete);

        var currentPolicies = new SecretAccessPolicies
        {
            SecretId = data.SecretId,
            OrganizationId = data.OrganizationId,
            UserAccessPolicies = [userPolicyChanges.Update, userPolicyChanges.Delete],
            GroupAccessPolicies = [groupPolicyChanges.Update, groupPolicyChanges.Delete],
            ServiceAccountAccessPolicies = [serviceAccountPolicyChanges.Update, serviceAccountPolicyChanges.Delete]
        };

        sutProvider.GetDependency<IAccessPolicyRepository>()
            .GetSecretAccessPoliciesAsync(data.SecretId, userId)
            .ReturnsForAnyArgs(currentPolicies);

        var result = await sutProvider.Sut.GetAsync(data, userId);

        Assert.Equal(data.SecretId, result.SecretId);
        Assert.Equal(data.OrganizationId, result.OrganizationId);

        Assert.Single(result.UserAccessPolicyUpdates.Where(x =>
            x.Operation == AccessPolicyOperation.Delete && x.AccessPolicy == userPolicyChanges.Delete));
        Assert.Single(result.UserAccessPolicyUpdates.Where(x =>
            x.Operation == AccessPolicyOperation.Update &&
            x.AccessPolicy.OrganizationUserId == userPolicyChanges.Update.OrganizationUserId));
        Assert.Equal(result.UserAccessPolicyUpdates.Count() - 2,
            result.UserAccessPolicyUpdates.Count(x => x.Operation == AccessPolicyOperation.Create));

        Assert.Single(result.GroupAccessPolicyUpdates.Where(x =>
            x.Operation == AccessPolicyOperation.Delete && x.AccessPolicy == groupPolicyChanges.Delete));
        Assert.Single(result.GroupAccessPolicyUpdates.Where(x =>
            x.Operation == AccessPolicyOperation.Update &&
            x.AccessPolicy.GroupId == groupPolicyChanges.Update.GroupId));
        Assert.Equal(result.GroupAccessPolicyUpdates.Count() - 2,
            result.GroupAccessPolicyUpdates.Count(x => x.Operation == AccessPolicyOperation.Create));

        Assert.Single(result.ServiceAccountAccessPolicyUpdates.Where(x =>
            x.Operation == AccessPolicyOperation.Delete && x.AccessPolicy == serviceAccountPolicyChanges.Delete));
        Assert.Single(result.ServiceAccountAccessPolicyUpdates.Where(x =>
            x.Operation == AccessPolicyOperation.Update &&
            x.AccessPolicy.ServiceAccountId == serviceAccountPolicyChanges.Update.ServiceAccountId));
        Assert.Equal(result.ServiceAccountAccessPolicyUpdates.Count() - 2,
            result.ServiceAccountAccessPolicyUpdates.Count(x => x.Operation == AccessPolicyOperation.Create));
    }

    private static (UserSecretAccessPolicy Update, UserSecretAccessPolicy Delete) SetupUserAccessPolicies(
        SecretAccessPolicies data, UserSecretAccessPolicy currentPolicyToDelete)
    {
        currentPolicyToDelete.GrantedSecretId = data.SecretId;

        var updatePolicy = new UserSecretAccessPolicy
        {
            OrganizationUserId = data.UserAccessPolicies.First().OrganizationUserId,
            GrantedSecretId = data.SecretId,
            Read = !data.UserAccessPolicies.First().Read,
            Write = !data.UserAccessPolicies.First().Write
        };

        return (updatePolicy, currentPolicyToDelete);
    }

    private static (GroupSecretAccessPolicy Update, GroupSecretAccessPolicy Delete) SetupGroupAccessPolicies(
        SecretAccessPolicies data, GroupSecretAccessPolicy currentPolicyToDelete)
    {
        currentPolicyToDelete.GrantedSecretId = data.SecretId;

        var updatePolicy = new GroupSecretAccessPolicy
        {
            GroupId = data.GroupAccessPolicies.First().GroupId,
            GrantedSecretId = data.SecretId,
            Read = !data.GroupAccessPolicies.First().Read,
            Write = !data.GroupAccessPolicies.First().Write
        };

        return (updatePolicy, currentPolicyToDelete);
    }

    private static (ServiceAccountSecretAccessPolicy Update, ServiceAccountSecretAccessPolicy Delete)
        SetupServiceAccountAccessPolicies(SecretAccessPolicies data,
            ServiceAccountSecretAccessPolicy currentPolicyToDelete)
    {
        currentPolicyToDelete.GrantedSecretId = data.SecretId;

        var updatePolicy = new ServiceAccountSecretAccessPolicy
        {
            ServiceAccountId = data.ServiceAccountAccessPolicies.First().ServiceAccountId,
            GrantedSecretId = data.SecretId,
            Read = !data.ServiceAccountAccessPolicies.First().Read,
            Write = !data.ServiceAccountAccessPolicies.First().Write
        };

        return (updatePolicy, currentPolicyToDelete);
    }

    private static SecretAccessPolicies SetupSecretAccessPolicies(SecretAccessPolicies data)
    {
        foreach (var policy in data.UserAccessPolicies)
        {
            policy.GrantedSecretId = data.SecretId;
        }

        foreach (var policy in data.GroupAccessPolicies)
        {
            policy.GrantedSecretId = data.SecretId;
        }

        foreach (var policy in data.ServiceAccountAccessPolicies)
        {
            policy.GrantedSecretId = data.SecretId;
        }

        return data;
    }
}
