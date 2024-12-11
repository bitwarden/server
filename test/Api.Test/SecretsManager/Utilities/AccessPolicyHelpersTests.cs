#nullable enable
using Bit.Api.SecretsManager.Utilities;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Core.Test.SecretsManager.AutoFixture.SecretsFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.SecretsManager.Utilities;

[ProjectCustomize]
[SecretCustomize]
public class AccessPolicyHelpersTests
{
    [Theory]
    [BitAutoData]
    public void CheckForDistinctAccessPolicies_DuplicateAccessPolicies_ThrowsBadRequestException(
        UserProjectAccessPolicy userProjectAccessPolicy,
        UserServiceAccountAccessPolicy userServiceAccountAccessPolicy,
        GroupProjectAccessPolicy groupProjectAccessPolicy,
        GroupServiceAccountAccessPolicy groupServiceAccountAccessPolicy,
        ServiceAccountProjectAccessPolicy serviceAccountProjectAccessPolicy
    )
    {
        var accessPolicies = new List<BaseAccessPolicy>
        {
            userProjectAccessPolicy,
            userProjectAccessPolicy,
            userServiceAccountAccessPolicy,
            userServiceAccountAccessPolicy,
            groupProjectAccessPolicy,
            groupProjectAccessPolicy,
            groupServiceAccountAccessPolicy,
            groupServiceAccountAccessPolicy,
            serviceAccountProjectAccessPolicy,
            serviceAccountProjectAccessPolicy,
        };

        Assert.Throws<BadRequestException>(() =>
        {
            AccessPolicyHelpers.CheckForDistinctAccessPolicies(accessPolicies);
        });
    }

    [Fact]
    public void CheckForDistinctAccessPolicies_UnsupportedAccessPolicy_ThrowsArgumentException()
    {
        var accessPolicies = new List<BaseAccessPolicy> { new UnsupportedAccessPolicy() };

        Assert.Throws<ArgumentException>(() =>
        {
            AccessPolicyHelpers.CheckForDistinctAccessPolicies(accessPolicies);
        });
    }

    [Theory]
    [BitAutoData]
    public void CheckForDistinctAccessPolicies_DistinctPolicies_Success(
        UserProjectAccessPolicy userProjectAccessPolicy,
        UserServiceAccountAccessPolicy userServiceAccountAccessPolicy,
        GroupProjectAccessPolicy groupProjectAccessPolicy,
        GroupServiceAccountAccessPolicy groupServiceAccountAccessPolicy,
        ServiceAccountProjectAccessPolicy serviceAccountProjectAccessPolicy
    )
    {
        var accessPolicies = new List<BaseAccessPolicy>
        {
            userProjectAccessPolicy,
            userServiceAccountAccessPolicy,
            groupProjectAccessPolicy,
            groupServiceAccountAccessPolicy,
            serviceAccountProjectAccessPolicy,
        };

        AccessPolicyHelpers.CheckForDistinctAccessPolicies(accessPolicies);
    }

    [Fact]
    public void CheckAccessPoliciesHaveReadPermission_ReadPermissionFalse_ThrowsBadRequestException()
    {
        var accessPolicies = new List<BaseAccessPolicy>
        {
            new UserProjectAccessPolicy { Read = false, Write = true },
            new GroupProjectAccessPolicy { Read = true, Write = false },
        };

        Assert.Throws<BadRequestException>(() =>
        {
            AccessPolicyHelpers.CheckAccessPoliciesHaveReadPermission(accessPolicies);
        });
    }

    [Fact]
    public void CheckAccessPoliciesHaveReadPermission_AllReadIsTrue_Success()
    {
        var accessPolicies = new List<BaseAccessPolicy>
        {
            new UserProjectAccessPolicy { Read = true, Write = true },
            new GroupProjectAccessPolicy { Read = true, Write = false },
        };

        AccessPolicyHelpers.CheckAccessPoliciesHaveReadPermission(accessPolicies);
    }

    private class UnsupportedAccessPolicy : BaseAccessPolicy;
}
