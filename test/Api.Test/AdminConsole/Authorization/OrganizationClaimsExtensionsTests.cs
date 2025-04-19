using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Test.AdminConsole.Helpers;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

public class OrganizationClaimsExtensionsTests
{
    [Theory, BitMemberAutoData(nameof(GetTestOrganizations))]
    public void GetCurrentContextOrganization_ParsesOrganizationFromClaims(CurrentContextOrganization expected, User user)
    {
        var claims = CoreHelpers.BuildIdentityClaims(user, [expected], [], false)
            .Select(c => new Claim(c.Key, c.Value));

        var claimsPrincipal = new ClaimsPrincipal();
        claimsPrincipal.AddIdentities([new ClaimsIdentity(claims)]);

        var actual = claimsPrincipal.GetCurrentContextOrganization(expected.Id);

        AssertHelper.AssertPropertyEqual(expected, actual);
    }

    public static IEnumerable<object[]> GetTestOrganizations()
    {
        var roles = new List<OrganizationUserType> { OrganizationUserType.Owner, OrganizationUserType.Admin, OrganizationUserType.User };
        foreach (var role in roles)
        {
            yield return
            [
                new CurrentContextOrganization
                {
                    Id = Guid.NewGuid(),
                    Type = role,
                    AccessSecretsManager = true
                }
            ];
        }

        var permissions = PermissionsHelpers.GetAllPermissions();
        foreach (var permission in permissions)
        {
            yield return
            [
                new CurrentContextOrganization
                {
                    Id = Guid.NewGuid(),
                    Type = OrganizationUserType.Custom,
                    Permissions = permission
                }
            ];
        }
    }
}
