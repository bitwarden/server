using Bit.Api.Billing.Attributes;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Api;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Billing.Attributes;

public class InjectOrganizationAttributeTests
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ActionExecutionDelegate _next;
    private readonly ActionExecutingContext _context;
    private readonly Organization _organization;
    private readonly Guid _organizationId;

    public InjectOrganizationAttributeTests()
    {
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _organizationId = Guid.NewGuid();
        _organization = new Organization { Id = _organizationId };

        var httpContext = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddScoped(_ => _organizationRepository);
        httpContext.RequestServices = services.BuildServiceProvider();

        var routeData = new RouteData { Values = { ["organizationId"] = _organizationId.ToString() } };

        var actionContext = new ActionContext(
            httpContext,
            routeData,
            new ActionDescriptor(),
            new ModelStateDictionary()
        );

        _next = () => Task.FromResult(new ActionExecutedContext(
            actionContext,
            new List<IFilterMetadata>(),
            new object()));

        _context = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object>(),
            new object());
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithExistingOrganization_InjectsOrganization()
    {
        var attribute = new InjectOrganizationAttribute();
        _organizationRepository.GetByIdAsync(_organizationId)
            .Returns(_organization);

        var parameter = new ParameterDescriptor
        {
            Name = "organization",
            ParameterType = typeof(Organization)
        };
        _context.ActionDescriptor.Parameters = [parameter];

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.Equal(_organization, _context.ActionArguments["organization"]);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithNonExistentOrganization_ReturnsNotFound()
    {
        var attribute = new InjectOrganizationAttribute();
        _organizationRepository.GetByIdAsync(_organizationId)
            .Returns((Organization)null);

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.IsType<NotFoundObjectResult>(_context.Result);
        var result = (NotFoundObjectResult)_context.Result;
        Assert.IsType<ErrorResponseModel>(result.Value);
        Assert.Equal("Organization not found.", ((ErrorResponseModel)result.Value).Message);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithInvalidOrganizationId_ReturnsBadRequest()
    {
        var attribute = new InjectOrganizationAttribute();
        _context.RouteData.Values["organizationId"] = "not-a-guid";

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.IsType<BadRequestObjectResult>(_context.Result);
        var result = (BadRequestObjectResult)_context.Result;
        Assert.IsType<ErrorResponseModel>(result.Value);
        Assert.Equal("Route parameter 'organizationId' is missing or invalid.", ((ErrorResponseModel)result.Value).Message);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithMissingOrganizationId_ReturnsBadRequest()
    {
        var attribute = new InjectOrganizationAttribute();
        _context.RouteData.Values.Clear();

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.IsType<BadRequestObjectResult>(_context.Result);
        var result = (BadRequestObjectResult)_context.Result;
        Assert.IsType<ErrorResponseModel>(result.Value);
        Assert.Equal("Route parameter 'organizationId' is missing or invalid.", ((ErrorResponseModel)result.Value).Message);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithoutOrganizationParameter_ContinuesExecution()
    {
        var attribute = new InjectOrganizationAttribute();
        _organizationRepository.GetByIdAsync(_organizationId)
            .Returns(_organization);

        _context.ActionDescriptor.Parameters = Array.Empty<ParameterDescriptor>();

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.Empty(_context.ActionArguments);
    }
}
