using Bit.Api.Billing.Attributes;
using Bit.Api.Models.Public.Response;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
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

public class InjectProviderAttributeTests
{
    private readonly IProviderRepository _providerRepository;
    private readonly ICurrentContext _currentContext;
    private readonly ActionExecutionDelegate _next;
    private readonly ActionExecutingContext _context;
    private readonly Provider _provider;
    private readonly Guid _providerId;

    public InjectProviderAttributeTests()
    {
        _providerRepository = Substitute.For<IProviderRepository>();
        _currentContext = Substitute.For<ICurrentContext>();
        _providerId = Guid.NewGuid();
        _provider = new Provider { Id = _providerId };

        var httpContext = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddScoped(_ => _providerRepository);
        services.AddScoped(_ => _currentContext);
        httpContext.RequestServices = services.BuildServiceProvider();

        var routeData = new RouteData { Values = { ["providerId"] = _providerId.ToString() } };

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
    public async Task OnActionExecutionAsync_WithExistingProvider_InjectsProvider()
    {
        var attribute = new InjectProviderAttribute(ProviderUserType.ProviderAdmin);
        _providerRepository.GetByIdAsync(_providerId).Returns(_provider);
        _currentContext.ProviderProviderAdmin(_providerId).Returns(true);

        var parameter = new ParameterDescriptor
        {
            Name = "provider",
            ParameterType = typeof(Provider)
        };
        _context.ActionDescriptor.Parameters = [parameter];

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.Equal(_provider, _context.ActionArguments["provider"]);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithNonExistentProvider_ReturnsNotFound()
    {
        var attribute = new InjectProviderAttribute(ProviderUserType.ProviderAdmin);
        _providerRepository.GetByIdAsync(_providerId).Returns((Provider)null);

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.IsType<NotFoundObjectResult>(_context.Result);
        var result = (NotFoundObjectResult)_context.Result;
        Assert.IsType<ErrorResponseModel>(result.Value);
        Assert.Equal("Provider not found.", ((ErrorResponseModel)result.Value).Message);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithInvalidProviderId_ReturnsBadRequest()
    {
        var attribute = new InjectProviderAttribute(ProviderUserType.ProviderAdmin);
        _context.RouteData.Values["providerId"] = "not-a-guid";

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.IsType<BadRequestObjectResult>(_context.Result);
        var result = (BadRequestObjectResult)_context.Result;
        Assert.IsType<ErrorResponseModel>(result.Value);
        Assert.Equal("Route parameter 'providerId' is missing or invalid.", ((ErrorResponseModel)result.Value).Message);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithMissingProviderId_ReturnsBadRequest()
    {
        var attribute = new InjectProviderAttribute(ProviderUserType.ProviderAdmin);
        _context.RouteData.Values.Clear();

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.IsType<BadRequestObjectResult>(_context.Result);
        var result = (BadRequestObjectResult)_context.Result;
        Assert.IsType<ErrorResponseModel>(result.Value);
        Assert.Equal("Route parameter 'providerId' is missing or invalid.", ((ErrorResponseModel)result.Value).Message);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithoutProviderParameter_ContinuesExecution()
    {
        var attribute = new InjectProviderAttribute(ProviderUserType.ProviderAdmin);
        _providerRepository.GetByIdAsync(_providerId).Returns(_provider);
        _currentContext.ProviderProviderAdmin(_providerId).Returns(true);

        _context.ActionDescriptor.Parameters = Array.Empty<ParameterDescriptor>();

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.Empty(_context.ActionArguments);
    }

    [Fact]
    public async Task OnActionExecutionAsync_UnauthorizedProviderAdmin_ReturnsUnauthorized()
    {
        var attribute = new InjectProviderAttribute(ProviderUserType.ProviderAdmin);
        _providerRepository.GetByIdAsync(_providerId).Returns(_provider);
        _currentContext.ProviderProviderAdmin(_providerId).Returns(false);

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.IsType<UnauthorizedObjectResult>(_context.Result);
        var result = (UnauthorizedObjectResult)_context.Result;
        Assert.IsType<ErrorResponseModel>(result.Value);
        Assert.Equal("Unauthorized.", ((ErrorResponseModel)result.Value).Message);
    }

    [Fact]
    public async Task OnActionExecutionAsync_UnauthorizedServiceUser_ReturnsUnauthorized()
    {
        var attribute = new InjectProviderAttribute(ProviderUserType.ServiceUser);
        _providerRepository.GetByIdAsync(_providerId).Returns(_provider);
        _currentContext.ProviderUser(_providerId).Returns(false);

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.IsType<UnauthorizedObjectResult>(_context.Result);
        var result = (UnauthorizedObjectResult)_context.Result;
        Assert.IsType<ErrorResponseModel>(result.Value);
        Assert.Equal("Unauthorized.", ((ErrorResponseModel)result.Value).Message);
    }

    [Fact]
    public async Task OnActionExecutionAsync_AuthorizedProviderAdmin_Succeeds()
    {
        var attribute = new InjectProviderAttribute(ProviderUserType.ProviderAdmin);
        _providerRepository.GetByIdAsync(_providerId).Returns(_provider);
        _currentContext.ProviderProviderAdmin(_providerId).Returns(true);

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.Null(_context.Result);
    }

    [Fact]
    public async Task OnActionExecutionAsync_AuthorizedServiceUser_Succeeds()
    {
        var attribute = new InjectProviderAttribute(ProviderUserType.ServiceUser);
        _providerRepository.GetByIdAsync(_providerId).Returns(_provider);
        _currentContext.ProviderUser(_providerId).Returns(true);

        await attribute.OnActionExecutionAsync(_context, _next);

        Assert.Null(_context.Result);
    }
}
