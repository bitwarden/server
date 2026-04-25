using Bit.Api.AdminConsole.Attributes;
using Bit.Core.Entities;
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

namespace Bit.Api.Test.AdminConsole.Attributes;

public class InjectOrganizationUserAttributeTests
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ActionExecutionDelegate _next;
    private readonly ActionExecutingContext _context;
    private readonly OrganizationUser _organizationUser;
    private readonly Guid _orgId;
    private readonly Guid _orgUserId;

    public InjectOrganizationUserAttributeTests()
    {
        _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        _orgId = Guid.NewGuid();
        _orgUserId = Guid.NewGuid();
        _organizationUser = new OrganizationUser { Id = _orgUserId, OrganizationId = _orgId };

        var httpContext = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddScoped(_ => _organizationUserRepository);
        httpContext.RequestServices = services.BuildServiceProvider();

        var routeData = new RouteData
        {
            Values =
            {
                ["orgId"] = _orgId.ToString(),
                ["id"] = _orgUserId.ToString()
            }
        };

        // Set route data on HttpContext so HttpContext.GetRouteData() returns it
        httpContext.Request.RouteValues = routeData.Values;

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
    public async Task OnActionExecutionAsync_OrgUserExistsAndBelongsToOrg_InjectsAndCallsNext()
    {
        var attribute = new InjectOrganizationUserAttribute();
        _organizationUserRepository.GetByIdAsync(_orgUserId)
            .Returns(_organizationUser);

        var parameter = new ParameterDescriptor
        {
            Name = "organizationUser",
            ParameterType = typeof(OrganizationUser)
        };
        _context.ActionDescriptor.Parameters = [parameter];

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.Null(_context.Result);
        Assert.Equal(_organizationUser, _context.ActionArguments["organizationUser"]);
    }

    [Fact]
    public async Task OnActionExecutionAsync_OrgUserNotFound_ReturnsNotFound()
    {
        var attribute = new InjectOrganizationUserAttribute();
        _organizationUserRepository.GetByIdAsync(_orgUserId)
            .Returns((OrganizationUser)null);

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.IsType<NotFoundObjectResult>(_context.Result);
        var result = (NotFoundObjectResult)_context.Result;
        Assert.IsType<ErrorResponseModel>(result.Value);
        Assert.Equal("Organization user not found.", ((ErrorResponseModel)result.Value).Message);
    }

    [Fact]
    public async Task OnActionExecutionAsync_OrgUserBelongsToDifferentOrg_ReturnsNotFound()
    {
        var attribute = new InjectOrganizationUserAttribute();
        var wrongOrgUser = new OrganizationUser { Id = _orgUserId, OrganizationId = Guid.NewGuid() };
        _organizationUserRepository.GetByIdAsync(_orgUserId)
            .Returns(wrongOrgUser);

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.IsType<NotFoundObjectResult>(_context.Result);
        var result = (NotFoundObjectResult)_context.Result;
        Assert.IsType<ErrorResponseModel>(result.Value);
        Assert.Equal("Organization user not found.", ((ErrorResponseModel)result.Value).Message);
    }

    [Fact]
    public async Task OnActionExecutionAsync_InvalidOrgId_ReturnsBadRequest()
    {
        var attribute = new InjectOrganizationUserAttribute();
        _context.RouteData.Values["orgId"] = "not-a-guid";

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.IsType<BadRequestObjectResult>(_context.Result);
        var result = (BadRequestObjectResult)_context.Result;
        Assert.IsType<ErrorResponseModel>(result.Value);
        Assert.Equal("Route parameter 'orgId' or 'organizationId' is missing or invalid.", ((ErrorResponseModel)result.Value).Message);
    }

    [Fact]
    public async Task OnActionExecutionAsync_MissingOrgId_ReturnsBadRequest()
    {
        var attribute = new InjectOrganizationUserAttribute();
        _context.RouteData.Values.Remove("orgId");

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.IsType<BadRequestObjectResult>(_context.Result);
        var result = (BadRequestObjectResult)_context.Result;
        Assert.IsType<ErrorResponseModel>(result.Value);
        Assert.Equal("Route parameter 'orgId' or 'organizationId' is missing or invalid.", ((ErrorResponseModel)result.Value).Message);
    }

    [Fact]
    public async Task OnActionExecutionAsync_InvalidOrgUserId_ReturnsBadRequest()
    {
        var attribute = new InjectOrganizationUserAttribute();
        _context.RouteData.Values["id"] = "not-a-guid";

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.IsType<BadRequestObjectResult>(_context.Result);
        var result = (BadRequestObjectResult)_context.Result;
        Assert.IsType<ErrorResponseModel>(result.Value);
        Assert.Equal("Route parameter 'id' is missing or invalid.", ((ErrorResponseModel)result.Value).Message);
    }

    [Fact]
    public async Task OnActionExecutionAsync_MissingOrgUserId_ReturnsBadRequest()
    {
        var attribute = new InjectOrganizationUserAttribute();
        _context.RouteData.Values.Remove("id");

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.IsType<BadRequestObjectResult>(_context.Result);
        var result = (BadRequestObjectResult)_context.Result;
        Assert.IsType<ErrorResponseModel>(result.Value);
        Assert.Equal("Route parameter 'id' is missing or invalid.", ((ErrorResponseModel)result.Value).Message);
    }

    [Fact]
    public async Task OnActionExecutionAsync_NoMatchingParameter_ContinuesWithoutInjection()
    {
        var attribute = new InjectOrganizationUserAttribute();
        _organizationUserRepository.GetByIdAsync(_orgUserId)
            .Returns(_organizationUser);

        _context.ActionDescriptor.Parameters = Array.Empty<ParameterDescriptor>();

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.Null(_context.Result);
        Assert.Empty(_context.ActionArguments);
    }

    [Fact]
    public async Task OnActionExecutionAsync_OrganizationIdRouteParam_ResolvesOrgId()
    {
        var attribute = new InjectOrganizationUserAttribute();
        _context.RouteData.Values.Remove("orgId");
        _context.RouteData.Values["organizationId"] = _orgId.ToString();

        _organizationUserRepository.GetByIdAsync(_orgUserId)
            .Returns(_organizationUser);

        var parameter = new ParameterDescriptor
        {
            Name = "organizationUser",
            ParameterType = typeof(OrganizationUser)
        };
        _context.ActionDescriptor.Parameters = [parameter];

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.Null(_context.Result);
        Assert.Equal(_organizationUser, _context.ActionArguments["organizationUser"]);
    }

    [Fact]
    public async Task OnActionExecutionAsync_CustomRouteParamName_ReadsCorrectRouteValue()
    {
        var attribute = new InjectOrganizationUserAttribute("organizationUserId");
        _context.RouteData.Values.Remove("id");
        _context.RouteData.Values["organizationUserId"] = _orgUserId.ToString();

        _organizationUserRepository.GetByIdAsync(_orgUserId)
            .Returns(_organizationUser);

        var parameter = new ParameterDescriptor
        {
            Name = "organizationUser",
            ParameterType = typeof(OrganizationUser)
        };
        _context.ActionDescriptor.Parameters = [parameter];

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.Null(_context.Result);
        Assert.Equal(_organizationUser, _context.ActionArguments["organizationUser"]);
    }
}
