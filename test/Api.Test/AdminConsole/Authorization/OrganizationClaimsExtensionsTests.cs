using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
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

        var permissions = GetTestCustomPermissions();
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

    private static IEnumerable<Permissions> GetTestCustomPermissions()
    {
        yield return new Permissions { AccessEventLogs = true };
        yield return new Permissions { AccessImportExport = true };
        yield return new Permissions { AccessReports = true };
        yield return new Permissions { CreateNewCollections = true };
        yield return new Permissions { EditAnyCollection = true };
        yield return new Permissions { DeleteAnyCollection = true };
        yield return new Permissions { ManageGroups = true };
        yield return new Permissions { ManagePolicies = true };
        yield return new Permissions { ManageSso = true };
        yield return new Permissions { ManageUsers = true };
        yield return new Permissions { ManageResetPassword = true };
        yield return new Permissions { ManageScim = true };
    }
}
