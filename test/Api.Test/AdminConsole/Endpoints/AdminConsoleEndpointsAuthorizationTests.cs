using Bit.Api.AdminConsole.Endpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Endpoints;

public class AdminConsoleEndpointsAuthorizationTests
{
    [Fact]
    public void AllAdminConsoleEndpoints_RequireAuthorization()
    {
        var builder = WebApplication.CreateBuilder();
        var handlerTypes = typeof(AdminConsoleEndpoints).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace == "Bit.Api.AdminConsole.Endpoints.Handlers");
        foreach (var handlerType in handlerTypes)
        {
            builder.Services.AddScoped(handlerType);
        }
        var app = builder.Build();
        app.MapAdminConsoleEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(ds => ds.Endpoints).ToList();

        Assert.NotEmpty(endpoints);
        foreach (var endpoint in endpoints)
        {
            var hasAuthorization = endpoint.Metadata.GetMetadata<IAuthorizeData>() != null
                || endpoint.Metadata.GetMetadata<IAllowAnonymous>() != null;
            Assert.True(hasAuthorization, $"{endpoint.DisplayName} has no authorization metadata.");
        }
    }
}
