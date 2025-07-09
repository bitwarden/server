using Bit.Core.Entities;
using Bit.Core.Models.Api;
using Bit.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using System.Security.Claims;
using Bit.Api.Billing.Attributes;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bit.Api.Test.Billing.Attributes;

public class InjectUserAttributesTests
{
    private readonly IUserService _userService;
    private readonly ActionExecutionDelegate _next;
    private readonly ActionExecutingContext _context;
    private readonly User _user;

    public InjectUserAttributesTests()
    {
        _userService = Substitute.For<IUserService>();
        _user = new User { Id = Guid.NewGuid() };

        var httpContext = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddScoped(_ => _userService);
        httpContext.RequestServices = services.BuildServiceProvider();

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
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
    public async Task OnActionExecutionAsync_WithAuthorizedUser_InjectsUser()
    {
        var attribute = new InjectUserAttribute();
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(_user);

        var parameter = new ParameterDescriptor
        {
            Name = "user",
            ParameterType = typeof(User)
        };
        _context.ActionDescriptor.Parameters = [parameter];

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.Equal(_user, _context.ActionArguments["user"]);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithUnauthorizedUser_ReturnsUnauthorized()
    {
        var attribute = new InjectUserAttribute();
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns((User)null);

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.IsType<UnauthorizedObjectResult>(_context.Result);
        var result = (UnauthorizedObjectResult)_context.Result;
        Assert.IsType<ErrorResponseModel>(result.Value);
        Assert.Equal("Unauthorized.", ((ErrorResponseModel)result.Value).Message);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithoutUserParameter_ContinuesExecution()
    {
        var attribute = new InjectUserAttribute();
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(_user);

        _context.ActionDescriptor.Parameters = Array.Empty<ParameterDescriptor>();

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.Empty(_context.ActionArguments);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithMultipleParameters_InjectsUserCorrectly()
    {
        var attribute = new InjectUserAttribute();
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(_user);

        var parameters = new[]
        {
            new ParameterDescriptor
            {
                Name = "otherParam",
                ParameterType = typeof(string)
            },
            new ParameterDescriptor
            {
                Name = "user",
                ParameterType = typeof(User)
            }
        };
        _context.ActionDescriptor.Parameters = parameters;

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.Single(_context.ActionArguments);
        Assert.Equal(_user, _context.ActionArguments["user"]);
    }
}
