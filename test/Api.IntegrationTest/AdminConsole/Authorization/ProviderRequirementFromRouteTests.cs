using System.Reflection;
using Bit.Api.AdminConsole.Authorization.Providers;
using Bit.Api.IntegrationTest.Factories;
using Bit.IntegrationTestCommon;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Authorization;

public class ProviderRequirementFromRouteTests(ApiApplicationFactory factory)
    : RouteIdFromRouteTestsBase(factory.Services), IClassFixture<ApiApplicationFactory>
{
    private const string _providerIdRouteName = "providerId";

    /// <summary>
    /// IF the endpoint uses:
    ///   <see cref="IProviderRequirement"/>
    /// AND has a providerId parameter
    /// THEN that parameter must have [FromRoute].
    ///
    /// The handler reads the id from route values only. [FromRoute] forces the binding to that.
    /// </summary>
    [Fact]
    public void AllProviderRequirementEndpoints_BindProviderIdFromRoute() =>
        AssertAllGuardedEndpointsBindIdFromRoute();

    protected override bool IsGuardedEndpoint(Endpoint endpoint) =>
        endpoint.Metadata
            .OfType<IAuthorizationRequirementData>()
            .SelectMany(data => data.GetRequirements())
            .Any(IsRequirement);

    protected override string DescribeSuffix(Endpoint endpoint)
    {
        var requirements = endpoint.Metadata
            .OfType<IAuthorizationRequirementData>()
            .SelectMany(data => data.GetRequirements())
            .Where(IsRequirement)
            .Select(requirement => requirement.GetType().Name)
            .Distinct();

        return $" [{string.Join(", ", requirements)}]";
    }

    protected override bool IsRouteIdParameter(ParameterInfo parameter) =>
        string.Equals(parameter.Name, _providerIdRouteName, StringComparison.OrdinalIgnoreCase);

    protected override string FailureSummary(int violationCount) =>
        $"{violationCount} endpoint(s) guarded by an IProviderRequirement declare a 'providerId' parameter " +
        "without [FromRoute]. The ProviderRequirementHandler reads the provider id from route values only, so a " +
        "same-named parameter must be bound with [FromRoute] or a form body value can shadow it and diverge from " +
        "the authorized id";

    private static bool IsRequirement(IAuthorizationRequirement requirement) =>
        requirement is IProviderRequirement;
}
