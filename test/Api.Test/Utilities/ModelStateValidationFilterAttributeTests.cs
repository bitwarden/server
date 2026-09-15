using System.ComponentModel.DataAnnotations;
using Bit.Api.Utilities;
using Bit.Core;
using Bit.HttpExtensions;
using Bitwarden.Server.Sdk.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using InternalApi = Bit.Core.Models.Api;
using PublicApi = Bit.Api.Models.Public.Response;

namespace Bit.Api.Test.Utilities;

/// <summary>
/// Which body each surface answers a failed model with, and when.
/// </summary>
/// <remarks>
/// The coded document replaces one a client is already parsing, so what matters here is not that it is produced
/// but that nothing produces it until it is switched on, and that the public API never does.
/// </remarks>
public class ModelStateValidationFilterAttributeTests
{
    private sealed class Model
    {
        [Required]
        public string? Name { get; set; }
    }

    [Fact]
    public void InternalApi_WithTheFlagOff_AnswersWithTheLegacyEnvelope()
    {
        var context = Context();

        new ModelStateValidationFilterAttribute(publicApi: false).OnActionExecuting(context);

        var result = Assert.IsType<BadRequestObjectResult>(context.Result);
        Assert.IsType<InternalApi.ErrorResponseModel>(result.Value);
    }

    [Fact]
    public void InternalApi_WithTheFlagOn_AnswersWithTheCodedProblemDocument()
    {
        var context = Context(flagEnabled: true);

        new ModelStateValidationFilterAttribute(publicApi: false).OnActionExecuting(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("application/problem+json", result.ContentTypes);
        var problem = Assert.IsType<BitwardenValidationProblemDetails>(result.Value);
        Assert.Equal("validation_error", problem.Type);
        Assert.Single(problem.Errors);
    }

    [Fact]
    public void PublicApi_WithTheFlagOn_StillAnswersWithItsPublishedShape()
    {
        // The public API's error shape is versioned separately and is not carried along by this flag.
        var context = Context(flagEnabled: true);

        new ModelStateValidationFilterAttribute(publicApi: true).OnActionExecuting(context);

        var result = Assert.IsType<BadRequestObjectResult>(context.Result);
        Assert.IsType<PublicApi.ErrorResponseModel>(result.Value);
    }

    [Fact]
    public void AValidModel_IsLeftAlone()
    {
        var context = Context(flagEnabled: true, invalid: false);

        new ModelStateValidationFilterAttribute(publicApi: false).OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    private static ActionExecutingContext Context(bool flagEnabled = false, bool invalid = true)
    {
        var featureService = Substitute.For<IFeatureService>();
        featureService.IsEnabled(FeatureFlagKeys.CodedValidationProblems).Returns(flagEnabled);

        var services = new ServiceCollection();
        services.AddSingleton(featureService);

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var descriptor = new ControllerActionDescriptor
        {
            Parameters = [new ParameterDescriptor { Name = "model", ParameterType = typeof(Model) }],
        };

        var actionContext = new ActionContext(httpContext, new RouteData(), descriptor);
        if (invalid)
        {
            actionContext.ModelState.AddModelError("Name", "The Name field is required.");
        }

        return new ActionExecutingContext(
            actionContext,
            [],
            new Dictionary<string, object?> { ["model"] = new Model() },
            controller: null!);
    }
}
