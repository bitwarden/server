using Bit.Api.AdminConsole.Authorization;
using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Services.Pam.Api.Endpoints;
using Bit.Services.Pam.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Endpoints;

/// <summary>
/// The access-rule endpoints authorize through the standard authorization middleware rather than in their handler,
/// so nothing in the handler fails closed if a <c>RequireAuthorization</c> call is dropped. These tests read the
/// materialized endpoint metadata to keep that from regressing silently.
/// </summary>
public class AccessRuleEndpointsAuthorizationTests
{
    private const string _getAll = "Pam_AccessRules_GetAll";
    private const string _get = "Pam_AccessRules_Get";
    private const string _post = "Pam_AccessRules_Post";
    private const string _put = "Pam_AccessRules_Put";
    private const string _delete = "Pam_AccessRules_Delete";

    [Theory]
    [InlineData(_getAll)]
    [InlineData(_get)]
    [InlineData(_post)]
    [InlineData(_put)]
    [InlineData(_delete)]
    public void AccessRuleEndpoints_RequireOrganizationMembership(string endpointName)
    {
        var endpoint = GetEndpoint(endpointName);

        Assert.Contains(endpoint.Metadata, m => m is AuthorizeAttribute<MemberRequirement>);
    }

    /// <summary>
    /// Providers must not reach access rules at all. This is easy to regress by "widening" the group requirement to
    /// <c>MemberOrProviderRequirement</c> for parity with other organization endpoints — and it would not show up on
    /// the write endpoints, because <see cref="ManageAccessRulesRequirement"/> authorizes providers on its own.
    /// </summary>
    [Theory]
    [InlineData(_getAll)]
    [InlineData(_get)]
    [InlineData(_post)]
    [InlineData(_put)]
    [InlineData(_delete)]
    public void AccessRuleEndpoints_DoNotAdmitProviders(string endpointName)
    {
        var endpoint = GetEndpoint(endpointName);

        Assert.DoesNotContain(endpoint.Metadata, m => m is AuthorizeAttribute<MemberOrProviderRequirement>);
    }

    [Theory]
    [InlineData(_post)]
    [InlineData(_put)]
    [InlineData(_delete)]
    public void AccessRuleWriteEndpoints_RequireManageAccessRules(string endpointName)
    {
        var endpoint = GetEndpoint(endpointName);

        Assert.Contains(endpoint.Metadata, m => m is AuthorizeAttribute<ManageAccessRulesRequirement>);
    }

    /// <summary>
    /// Reading rules is available to any member; only administration is gated on the permission. If a read ever
    /// picks up the write requirement, members lose access to the list they are supposed to see.
    /// </summary>
    [Theory]
    [InlineData(_getAll)]
    [InlineData(_get)]
    public void AccessRuleReadEndpoints_DoNotRequireManageAccessRules(string endpointName)
    {
        var endpoint = GetEndpoint(endpointName);

        Assert.DoesNotContain(endpoint.Metadata, m => m is AuthorizeAttribute<ManageAccessRulesRequirement>);
    }

    /// <summary>
    /// The organization requirements read <c>orgId</c> off the route and throw when it is absent, so the route
    /// prefix and the requirements have to stay in step.
    /// </summary>
    [Theory]
    [InlineData(_getAll)]
    [InlineData(_get)]
    [InlineData(_post)]
    [InlineData(_put)]
    [InlineData(_delete)]
    public void AccessRuleEndpoints_BindOrgIdFromTheRoute(string endpointName)
    {
        var endpoint = GetEndpoint(endpointName);

        Assert.Contains("{orgId:guid}", endpoint.RoutePattern.RawText);
    }

    private static RouteEndpoint GetEndpoint(string endpointName)
    {
        // Minimal API parameter binding treats a handler parameter as a service only if the container knows about
        // it, so the real registrations have to be present for the endpoints to materialize at all.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRouting();
        services.AddPamServices();

        using var serviceProvider = services.BuildServiceProvider();
        var routeBuilder = new TestEndpointRouteBuilder(serviceProvider);
        routeBuilder.MapPamEndpoints();

        using var dataSource = new CompositeEndpointDataSource(routeBuilder.DataSources);
        var endpoint = dataSource
            .Endpoints
            .OfType<RouteEndpoint>()
            .SingleOrDefault(e => e.Metadata.OfType<IEndpointNameMetadata>()
                .Any(n => n.EndpointName == endpointName));

        Assert.NotNull(endpoint);
        return endpoint;
    }

    /// <summary>
    /// Materializes the PAM route groups into endpoints outside of the request pipeline, so the metadata the
    /// authorization middleware would read can be inspected directly.
    /// </summary>
    private sealed class TestEndpointRouteBuilder(IServiceProvider serviceProvider) : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider { get; } = serviceProvider;
        public ICollection<EndpointDataSource> DataSources { get; } = new List<EndpointDataSource>();
        public IApplicationBuilder CreateApplicationBuilder() => new ApplicationBuilder(ServiceProvider);
    }
}
