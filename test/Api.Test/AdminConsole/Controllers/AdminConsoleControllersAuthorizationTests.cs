using Bit.Api.AdminConsole.Controllers;
using Bit.Api.Test.Utilities;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Controllers;

public class AdminConsoleControllersAuthorizationTests
{
    /// <summary>
    /// Controllers that have not yet been migrated to use method-level authorization attributes.
    /// TODO: Remove controllers from this list as they are migrated to use [Authorize] or [AllowAnonymous] on all methods.
    /// </summary>
    private static readonly HashSet<Type> _controllersNotYetMigrated =
    [
        typeof(OrganizationAuthRequestsController),
        typeof(OrganizationConnectionsController),
        typeof(OrganizationDomainController),
        typeof(OrganizationsController),
        typeof(OrganizationUsersController),
        typeof(CollectionsController),
    ];

    public static IEnumerable<object[]> GetAllAdminConsoleControllers()
    {
        // This is just a convenient way to get the assembly reference - it does
        // not actually require that all controllers extend this base class
        var assembly = typeof(BaseAdminConsoleController).Assembly;
        return assembly.GetTypes()
            .Where(t => t.IsClass
                && !t.IsAbstract
                && typeof(ControllerBase).IsAssignableFrom(t)
                && t.Namespace == "Bit.Api.AdminConsole.Controllers")
            .Except(_controllersNotYetMigrated)
            .Select(t => new object[] { t });
    }

    /// <summary>
    /// Automatically finds all controllers in the Bit.Api.AdminConsole.Controllers namespace
    /// and ensures that they have [Authorize] or [AllowAnonymous] attributes on all methods.
    /// </summary>
    /// <remarks>
    /// See <see cref="_controllersNotYetMigrated"/> for an exemption list of existing controllers
    /// that aren't using these attributes yet (but should be).
    /// See <see cref="ControllerAuthorizationTestHelpers.AssertAllHttpMethodsHaveAuthorization"/>
    /// for more information about what this test requires to pass.
    /// </remarks>
    [Theory]
    [MemberData(nameof(GetAllAdminConsoleControllers))]
    public void AllControllers_HaveAuthorizationOnAllMethods(Type controllerType)
    {
        ControllerAuthorizationTestHelpers.AssertAllHttpMethodsHaveAuthorization(controllerType);
    }
}
