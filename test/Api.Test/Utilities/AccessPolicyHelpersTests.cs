using Bit.Api.SecretsManager.Utilities;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Xunit;

public class AccessPolicyHelpersTests
{
    [Fact]
    public void CheckForDistinctAccessPolicies_ThrowsExceptionWhenDuplicateExists()
    {
        var duplicatePolicy = new UserProjectAccessPolicy
        {
            OrganizationUserId = Guid.NewGuid(),
            GrantedProjectId = Guid.NewGuid()
        };

        var accessPolicies = new List<BaseAccessPolicy>
            {
                duplicatePolicy,
                duplicatePolicy // Duplicate policy
            };

        Assert.Throws<BadRequestException>(() =>
        {
            AccessPolicyHelpers.CheckForDistinctAccessPolicies(accessPolicies);
        });
    }

    [Fact]
    public void CheckForDistinctAccessPolicies_Success()
    {
        var accessPolicies = new List<BaseAccessPolicy>
            {
                new UserProjectAccessPolicy
                {
                    OrganizationUserId = Guid.NewGuid(),
                    GrantedProjectId = Guid.NewGuid()
                },
                new GroupProjectAccessPolicy
                {
                    GroupId = Guid.NewGuid(),
                    GrantedProjectId = Guid.NewGuid()
                }
            };

        var exception = Record.Exception(() => AccessPolicyHelpers.CheckForDistinctAccessPolicies(accessPolicies));
        Assert.Null(exception);
    }

    [Fact]
    public void CheckAccessPoliciesHasReadPermission_ThrowsExceptionWhenReadPermissionIsFalse()
    {
        var accessPolicies = new List<BaseAccessPolicy>
            {
                new UserProjectAccessPolicy { Read = false, Write = true },
                new GroupProjectAccessPolicy { Read = true, Write = false }
            };

        Assert.Throws<BadRequestException>(() =>
        {
            AccessPolicyHelpers.CheckAccessPoliciesHasReadPermission(accessPolicies);
        });
    }

    [Fact]
    public void CheckAccessPoliciesHasReadPermission_Success()
    {
        var accessPolicies = new List<BaseAccessPolicy>
            {
                new UserProjectAccessPolicy { Read = true, Write = true },
                new GroupProjectAccessPolicy { Read = true, Write = false }
            };

        var exception = Record.Exception(() => AccessPolicyHelpers.CheckAccessPoliciesHasReadPermission(accessPolicies));
        Assert.Null(exception);
    }
}
