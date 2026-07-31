using Bit.Api.AdminConsole.Endpoints;
using Bit.Api.AdminConsole.Endpoints.Handlers;
using Bit.Core;
using Bit.Core.Auth.Identity;
using Bitwarden.Server.Sdk.Features;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Endpoints;

public class CollectionUserEndpointsTests
{
    [Fact]
    public void MapCollectionUserEndpoints_RequiresApplicationPolicyAndFeatureFlag()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddScoped<CollectionUserEndpointsHandler>();
        var app = builder.Build();
        app.MapCollectionUserEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(ds => ds.Endpoints).ToList();

        Assert.Equal(2, endpoints.Count);
        foreach (var endpoint in endpoints)
        {
            var authorizeData = endpoint.Metadata.GetMetadata<IAuthorizeData>();
            Assert.Equal(Policies.Application, authorizeData?.Policy);

            var featureMetadata = endpoint.Metadata.GetMetadata<IFeatureMetadata>();
            Assert.Equal($"Flag = {FeatureFlagKeys.PM35160CollectionAuthorizationHandlers}", featureMetadata?.ToString());
        }
    }
}
