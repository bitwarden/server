using Bit.SharedWeb.Swagger;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Routing;

namespace SharedWeb.Test;

public class SwaggerGenOptionsExtTest
{
    [Fact]
    public void BuildOperationId_Controller_UsesControllerAndAction()
    {
        var apiDescription = new ApiDescription
        {
            ActionDescriptor = new ActionDescriptor
            {
                RouteValues = new Dictionary<string, string?>
                {
                    ["controller"] = "AccessRequests",
                    ["action"] = "GetInbox",
                },
                EndpointMetadata = new List<object>(),
            },
        };

        Assert.Equal("AccessRequests_GetInbox", SwaggerGenOptionsExt.BuildOperationId(apiDescription));
    }

    [Fact]
    public void BuildOperationId_MinimalApi_FallsBackToEndpointName()
    {
        // Minimal API endpoints carry no controller/action route values, only the name set via .WithName(...).
        var apiDescription = new ApiDescription
        {
            ActionDescriptor = new ActionDescriptor
            {
                RouteValues = new Dictionary<string, string?>(),
                EndpointMetadata = new List<object> { new EndpointNameMetadata("Pam_Leases_GetActive") },
            },
        };

        Assert.Equal("Pam_Leases_GetActive", SwaggerGenOptionsExt.BuildOperationId(apiDescription));
    }

    [Fact]
    public void BuildOperationId_NoRouteValuesOrEndpointName_FallsBackToRouteAndMethod()
    {
        // A Minimal API endpoint mapped without .WithName() has neither route values nor an endpoint name.
        // BuildOperationId must still return a stable, non-empty id: Swashbuckle writes it straight onto
        // operation.OperationId, and a null/empty id collapses distinct endpoints together, which
        // CheckDuplicateOperationIdsDocumentFilter rejects, aborting spec generation.
        var apiDescription = new ApiDescription
        {
            HttpMethod = "GET",
            RelativePath = "api/leases/active",
            ActionDescriptor = new ActionDescriptor
            {
                RouteValues = new Dictionary<string, string?>(),
                EndpointMetadata = new List<object>(),
            },
        };

        Assert.Equal("GET_api_leases_active", SwaggerGenOptionsExt.BuildOperationId(apiDescription));
    }

    [Fact]
    public void BuildOperationId_DistinctUnnamedEndpoints_ProduceDistinctNonEmptyIds()
    {
        static ApiDescription Unnamed(string method, string path) => new()
        {
            HttpMethod = method,
            RelativePath = path,
            ActionDescriptor = new ActionDescriptor
            {
                RouteValues = new Dictionary<string, string?>(),
                EndpointMetadata = new List<object>(),
            },
        };

        var first = SwaggerGenOptionsExt.BuildOperationId(Unnamed("GET", "api/widgets"));
        var second = SwaggerGenOptionsExt.BuildOperationId(Unnamed("POST", "api/widgets"));

        Assert.False(string.IsNullOrEmpty(first));
        Assert.False(string.IsNullOrEmpty(second));
        Assert.NotEqual(first, second);
    }
}
