using System.Security.Claims;
using Bit.Core.AdminConsole.OrganizationFeatures.Shared.Authorization;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Tools.Authorization;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Tools.Authorization;

[SutProviderCustomize]
public class VaultExportAuthorizationHandlerTests
{
    public static IEnumerable<object[]> CanExportEntireVault => new[]
    {
        new CurrentContextOrganization { Type = OrganizationUserType.Owner },
        new CurrentContextOrganization { Type = OrganizationUserType.Admin },
        new CurrentContextOrganization
        {
            Type = OrganizationUserType.Custom, Permissions = new Permissions { AccessImportExport = true }
        }
    }.Select(org => new []{org});

    [Theory]
    [BitMemberAutoData(nameof(CanExportEntireVault))]
    public async Task ExportAll_PermittedRoles_Success(CurrentContextOrganization org, OrganizationScope orgScope, ClaimsPrincipal user,
        SutProvider<VaultExportAuthorizationHandler> sutProvider)
    {
        org.Id = orgScope;
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(orgScope).Returns(org);

        var authContext = new AuthorizationHandlerContext(new[] { VaultExportOperations.ExportAll }, user, orgScope);
        await sutProvider.Sut.HandleAsync(authContext);

        Assert.True(authContext.HasSucceeded);
    }

    public static IEnumerable<object[]> CannotExportEntireVault => new[]
    {
        new CurrentContextOrganization { Type = OrganizationUserType.User },
        new CurrentContextOrganization
        {
            Type = OrganizationUserType.Custom, Permissions = FlipPermissions(new Permissions { AccessImportExport = true })
        }
    }.Select(org => new []{org});

    [Theory]
    [BitMemberAutoData(nameof(CannotExportEntireVault))]
    public async Task ExportAll_NotPermitted_Failure(CurrentContextOrganization org, OrganizationScope orgScope, ClaimsPrincipal user,
        SutProvider<VaultExportAuthorizationHandler> sutProvider)
    {
        org.Id = orgScope;
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(orgScope).Returns(org);

        var authContext = new AuthorizationHandlerContext(new[] { VaultExportOperations.ExportAll }, user, orgScope);
        await sutProvider.Sut.HandleAsync(authContext);

        Assert.False(authContext.HasSucceeded);
    }

    private static Permissions FlipPermissions(Permissions permissions)
    {
        // Get all false boolean properties of input object
        var inputsToFlip = permissions
            .GetType()
            .GetProperties()
            .Where(p =>
                p.PropertyType == typeof(bool) &&
                (bool)p.GetValue(permissions, null)! == false)
            .Select(p => p.Name);

        var result = new Permissions();

        // Set these to true on the result object
        result
            .GetType()
            .GetProperties()
            .Where(p => inputsToFlip.Contains(p.Name))
            .ToList()
            .ForEach(p => p.SetValue(result, true));

        return result;
    }
}
