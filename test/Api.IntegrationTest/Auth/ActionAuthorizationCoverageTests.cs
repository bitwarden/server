using Bit.Api.Controllers;
using Bit.Api.IntegrationTest.Factories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Xunit;

namespace Bit.Api.IntegrationTest.Auth;

public class ActionAuthorizationCoverageTests : IClassFixture<ApiApplicationFactory>
{
    private static readonly HashSet<string> _publicActions =
    [
        // Liveness probe.
        $"{typeof(InfoController).FullName}.{nameof(InfoController.GetAlive)}",
        // Deprecated alias of GetAlive.
        $"{typeof(InfoController).FullName}.GetNow",
        // Server configuration is fetched before login.
        $"{typeof(ConfigController).FullName}.{nameof(ConfigController.GetConfigs)}",
    ];

    private readonly ApiApplicationFactory _factory;

    public ActionAuthorizationCoverageTests(ApiApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void EveryAction_HasAnAuthorizationPolicyOrAllowAnonymous()
    {
        var actions = _factory.GetService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .ToList();

        var unprotected = actions
            .Where(a => !a.EndpointMetadata.OfType<IAllowAnonymous>().Any() &&
                        !a.EndpointMetadata.OfType<IAuthorizeData>().Any(d => !string.IsNullOrEmpty(d.Policy)))
            .Select(a => $"{a.ControllerTypeInfo.FullName}.{a.MethodInfo.Name}")
            .Where(name => !_publicActions.Contains(name))
            .Distinct()
            .Order()
            .ToList();

        Assert.NotEmpty(actions);
        Assert.True(unprotected.Count == 0,
            $"Actions without an authorization policy or [AllowAnonymous]:{Environment.NewLine}" +
            string.Join(Environment.NewLine, unprotected));
    }
}
