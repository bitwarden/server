#nullable enable
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Enums.AccessPolicies;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.SecretsManager.Models;

[SutProviderCustomize]
[ProjectCustomize]
public class SecretAccessPoliciesTests
{
    [Theory]
    [BitAutoData]
    public void GetPolicyUpdates_NoChanges_ReturnsEmptyList(SecretAccessPolicies data)
    {
        var result = data.GetPolicyUpdates(data);

        Assert.Empty(result.UserAccessPolicyUpdates);
        Assert.Empty(result.GroupAccessPolicyUpdates);
        Assert.Empty(result.ServiceAccountAccessPolicyUpdates);
    }

    [Fact]
    public void GetPolicyUpdates_ReturnsCorrectPolicyChanges()
    {
        var secretId = Guid.NewGuid();
        var updatedId = Guid.NewGuid();
        var createId = Guid.NewGuid();
        var unChangedId = Guid.NewGuid();
        var deleteId = Guid.NewGuid();

        var existing = new SecretAccessPolicies
        {
            UserAccessPolicies = new List<UserSecretAccessPolicy>
            {
                new() { OrganizationUserId = updatedId, GrantedSecretId = secretId, Read = true, Write = true },
                new() { OrganizationUserId = unChangedId, GrantedSecretId = secretId, Read = true, Write = true },
                new() { OrganizationUserId = deleteId, GrantedSecretId = secretId, Read = true, Write = true }
            },
            GroupAccessPolicies = new List<GroupSecretAccessPolicy>
            {
                new() { GroupId = updatedId, GrantedSecretId = secretId, Read = true, Write = true },
                new() { GroupId = unChangedId, GrantedSecretId = secretId, Read = true, Write = true },
                new() { GroupId = deleteId, GrantedSecretId = secretId, Read = true, Write = true }
            },
            ServiceAccountAccessPolicies = new List<ServiceAccountSecretAccessPolicy>
            {
                new() { ServiceAccountId = updatedId, GrantedSecretId = secretId, Read = true, Write = true },
                new() { ServiceAccountId = unChangedId, GrantedSecretId = secretId, Read = true, Write = true },
                new() { ServiceAccountId = deleteId, GrantedSecretId = secretId, Read = true, Write = true }
            }
        };

        var requested = new SecretAccessPolicies
        {
            UserAccessPolicies = new List<UserSecretAccessPolicy>
            {
                new() { OrganizationUserId = updatedId, GrantedSecretId = secretId, Read = true, Write = false },
                new() { OrganizationUserId = createId, GrantedSecretId = secretId, Read = false, Write = true },
                new() { OrganizationUserId = unChangedId, GrantedSecretId = secretId, Read = true, Write = true }
            },
            GroupAccessPolicies = new List<GroupSecretAccessPolicy>
            {
                new() { GroupId = updatedId, GrantedSecretId = secretId, Read = true, Write = false },
                new() { GroupId = createId, GrantedSecretId = secretId, Read = false, Write = true },
                new() { GroupId = unChangedId, GrantedSecretId = secretId, Read = true, Write = true }
            },
            ServiceAccountAccessPolicies = new List<ServiceAccountSecretAccessPolicy>
            {
                new() { ServiceAccountId = updatedId, GrantedSecretId = secretId, Read = true, Write = false },
                new() { ServiceAccountId = createId, GrantedSecretId = secretId, Read = false, Write = true },
                new() { ServiceAccountId = unChangedId, GrantedSecretId = secretId, Read = true, Write = true }
            }
        };


        var result = existing.GetPolicyUpdates(requested);

        Assert.Contains(createId, result.UserAccessPolicyUpdates
            .Where(pu => pu.Operation == AccessPolicyOperation.Create)
            .Select(pu => pu.AccessPolicy.OrganizationUserId!.Value));
        Assert.Contains(createId, result.GroupAccessPolicyUpdates
            .Where(pu => pu.Operation == AccessPolicyOperation.Create)
            .Select(pu => pu.AccessPolicy.GroupId!.Value));
        Assert.Contains(createId, result.ServiceAccountAccessPolicyUpdates
            .Where(pu => pu.Operation == AccessPolicyOperation.Create)
            .Select(pu => pu.AccessPolicy.ServiceAccountId!.Value));

        Assert.Contains(deleteId, result.UserAccessPolicyUpdates
            .Where(pu => pu.Operation == AccessPolicyOperation.Delete)
            .Select(pu => pu.AccessPolicy.OrganizationUserId!.Value));
        Assert.Contains(deleteId, result.GroupAccessPolicyUpdates
            .Where(pu => pu.Operation == AccessPolicyOperation.Delete)
            .Select(pu => pu.AccessPolicy.GroupId!.Value));
        Assert.Contains(deleteId, result.ServiceAccountAccessPolicyUpdates
            .Where(pu => pu.Operation == AccessPolicyOperation.Delete)
            .Select(pu => pu.AccessPolicy.ServiceAccountId!.Value));

        Assert.Contains(updatedId, result.UserAccessPolicyUpdates
            .Where(pu => pu.Operation == AccessPolicyOperation.Update)
            .Select(pu => pu.AccessPolicy.OrganizationUserId!.Value));
        Assert.Contains(updatedId, result.GroupAccessPolicyUpdates
            .Where(pu => pu.Operation == AccessPolicyOperation.Update)
            .Select(pu => pu.AccessPolicy.GroupId!.Value));
        Assert.Contains(updatedId, result.ServiceAccountAccessPolicyUpdates
            .Where(pu => pu.Operation == AccessPolicyOperation.Update)
            .Select(pu => pu.AccessPolicy.ServiceAccountId!.Value));

        Assert.DoesNotContain(unChangedId, result.UserAccessPolicyUpdates
            .Select(pu => pu.AccessPolicy.OrganizationUserId!.Value));
        Assert.DoesNotContain(unChangedId, result.GroupAccessPolicyUpdates
            .Select(pu => pu.AccessPolicy.GroupId!.Value));
        Assert.DoesNotContain(unChangedId, result.ServiceAccountAccessPolicyUpdates
            .Select(pu => pu.AccessPolicy.ServiceAccountId!.Value));
    }
}
