using Bit.Api.Pam.AccessConnector;
using Bit.Api.Vault.Models.Request;
using Bit.Api.Vault.Models.Response;
#if !OSS
using Bit.Core.Auth.Identity;
using Bit.Core.Context;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector;
using Bit.Services.Pam.Api.Endpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
#endif
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
#if !OSS
using Microsoft.Extensions.DependencyInjection;
#endif
using Xunit;

namespace Bit.Api.Test.Pam.AccessConnector;

public class RotationAttemptCipherEndpointsTests
{
    [Fact]
    public void MapRotationAttemptCipherEndpoints_KeepsRouteNamesTagAndSchemas()
    {
        var builder = WebApplication.CreateSlimBuilder();
        var app = builder.Build();
        app.MapGroup("/access-connectors/rotation/attempts").MapRotationAttemptCipherEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(d => d.Endpoints).OfType<RouteEndpoint>().ToList();

        Assert.Equal(2, endpoints.Count);
        var get = Assert.Single(endpoints,
            e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName ==
                 "Pam_AccessConnectors_Rotation_Attempts_GetCipher");
        var put = Assert.Single(endpoints,
            e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName ==
                 "Pam_AccessConnectors_Rotation_Attempts_PutCipher");
        Assert.Equal("/access-connectors/rotation/attempts/{id:guid}/cipher", get.RoutePattern.RawText);
        Assert.Equal("/access-connectors/rotation/attempts/{id:guid}/cipher", put.RoutePattern.RawText);
        Assert.Contains("GET", get.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
        Assert.Contains("PUT", put.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
        Assert.All(endpoints, e => Assert.Contains("PamAccessConnectorRotationAttempts",
            e.Metadata.GetMetadata<ITagsMetadata>()!.Tags));

        // The shared schema, not one for the sealed Full* subclass.
        Assert.Contains(get.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>(),
            p => p.StatusCode == StatusCodes.Status200OK && p.Type == typeof(CipherMiniResponseModel));
        Assert.Contains(put.Metadata.GetOrderedMetadata<IAcceptsMetadata>(),
            a => a.RequestType == typeof(CipherRequestModel));
    }

#if !OSS
    [Fact]
    public async Task MapRotationAttemptCipherEndpoints_WithMachineDefaults_GetsPolicyDocAndHeartbeat()
    {
        // Must carry the same policy, doc and heartbeat as the rest of the machine surface.
        var currentContext = Substitute.For<ICurrentContext>();
        var builder = WebApplication.CreateSlimBuilder();
        var app = builder.Build();
        app.MapGroup("/access-connectors/rotation/attempts")
            .WithPamAccessConnectorMachineDefaults()
            .MapRotationAttemptCipherEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(d => d.Endpoints).OfType<RouteEndpoint>().ToList();

        Assert.Equal(2, endpoints.Count);
        foreach (var endpoint in endpoints)
        {
            Assert.Equal("internal", endpoint.Metadata.GetMetadata<IEndpointGroupNameMetadata>()?.EndpointGroupName);
            Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                data => data.Policy == Policies.AccessConnector);

            // Without a PamAccessConnectorId the heartbeat filter 404s ahead of the handler.
            var services = new ServiceCollection();
            services.AddLogging();
            var hostEnvironment = Substitute.For<IHostEnvironment>();
            hostEnvironment.EnvironmentName.Returns(Environments.Production);
            services.AddSingleton(hostEnvironment);
            services.AddSingleton(currentContext);
            services.AddSingleton(Substitute.For<IPamAccessConnectorRepository>());
            services.AddSingleton(Options.Create(new PamRotationOptions()));
            services.AddSingleton(TimeProvider.System);
            currentContext.PamAccessConnectorId.Returns((Guid?)null);
            var httpContext = new DefaultHttpContext
            {
                RequestServices = services.BuildServiceProvider(),
                Response = { Body = new MemoryStream() },
            };

            await endpoint.RequestDelegate!(httpContext);

            Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        }
    }
#endif
}
