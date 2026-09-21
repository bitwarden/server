using System.Reflection;
using Bit.IntegrationTestCommon;
using Bit.Scim.IntegrationTest.Factories;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Bit.Scim.IntegrationTest;

public class OrganizationIdFromRouteTests(ScimApplicationFactory factory)
    : RouteIdFromRouteTestsBase(factory.Services), IClassFixture<ScimApplicationFactory>
{
    private const string _scimPolicy = "Scim";
    private static readonly string[] _organizationIdRouteNames = ["orgId", "organizationId"];

    /// <summary>
    /// IF the endpoint is guarded by the "Scim" authorization policy
    /// AND has an orgId or organizationId parameter
    /// THEN that parameter must have [FromRoute].
    ///
    /// SCIM authorization reads the id from route values only. [FromRoute] forces the binding to that.
    /// </summary>
    [Fact]
    public void AllScimEndpoints_BindOrganizationIdFromRoute() =>
        AssertAllGuardedEndpointsBindIdFromRoute();

    protected override bool IsGuardedEndpoint(Endpoint endpoint) =>
        endpoint.Metadata
            .OfType<IAuthorizeData>()
            .Any(data => data.Policy == _scimPolicy);

    protected override bool IsRouteIdParameter(ParameterInfo parameter) =>
        _organizationIdRouteNames.Contains(parameter.Name, StringComparer.OrdinalIgnoreCase);

    protected override string FailureSummary(int violationCount) =>
        $"{violationCount} SCIM endpoint(s) declare an 'orgId' or 'organizationId' parameter without [FromRoute]. " +
        "SCIM authorization reads the organization id from route values only, so a same-named parameter must be " +
        "bound with [FromRoute] or a form body value can shadow it and diverge from the authorized organization";
}
