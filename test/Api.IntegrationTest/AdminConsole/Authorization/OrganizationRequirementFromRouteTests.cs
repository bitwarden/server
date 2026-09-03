using System.Reflection;
using Bit.Api.AdminConsole.Authorization;
using Bit.Api.IntegrationTest.Factories;
using Bit.IntegrationTestCommon;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Authorization;

public class OrganizationRequirementFromRouteTests(ApiApplicationFactory factory)
    : RouteIdFromRouteTestsBase(factory.Services), IClassFixture<ApiApplicationFactory>
{
    private static readonly string[] _organizationIdRouteNames = ["orgId", "organizationId"];

    private static readonly Type[] _routeReadingRequirements =
    [
        typeof(OrgUserLinkedToUserIdRequirement),
        typeof(OrganizationCollectionManagementAccessRequirement),
    ];

    /// <summary>
    /// IF the endpoint uses:
    ///   <see cref="IOrganizationRequirement"/>
    ///   OR <see cref="OrgUserLinkedToUserIdRequirement"/>
    ///   OR <see cref="OrganizationCollectionManagementAccessRequirement"/>
    /// AND has an orgId or organizationId parameter
    /// THEN that parameter must have [FromRoute].
    ///
    /// The handler reads the id from route values only. [FromRoute] forces the binding to that.
    /// </summary>
    [Fact]
    public void AllOrganizationRequirementEndpoints_BindOrganizationIdFromRoute() =>
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
        _organizationIdRouteNames.Contains(parameter.Name, StringComparer.OrdinalIgnoreCase);

    protected override string FailureSummary(int violationCount) =>
        $"{violationCount} endpoint(s) guarded by an organization requirement declare an 'orgId' or " +
        "'organizationId' parameter without [FromRoute]. The requirement handler reads the organization id from " +
        "route values only, so a same-named parameter must be bound with [FromRoute] or a form body value can " +
        "shadow it and diverge from the authorized id";

    private static bool IsRequirement(IAuthorizationRequirement requirement) =>
        requirement is IOrganizationRequirement || _routeReadingRequirements.Contains(requirement.GetType());
}
