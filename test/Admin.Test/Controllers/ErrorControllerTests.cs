using Bit.Admin.Controllers;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using NSubstitute;

namespace Admin.Test.Controllers;

public class ErrorControllerTests
{
    private static ErrorController BuildSut(string? exceptionPath)
    {
        var httpContext = new DefaultHttpContext();

        if (exceptionPath is not null)
        {
            var feature = new ExceptionHandlerFeature
            {
                Path = exceptionPath,
                Error = new InvalidOperationException("invalid operation")
            };
            httpContext.Features.Set<IExceptionHandlerPathFeature>(feature);
            httpContext.Features.Set<IExceptionHandlerFeature>(feature);
        }

        var controllerContext = new ControllerContext { HttpContext = httpContext };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        return new ErrorController
        {
            ControllerContext = controllerContext,
            TempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>()),
            Url = new UrlHelper(actionContext)
        };
    }

    [Theory]
    [InlineData("//example.com")]
    [InlineData("//example.com/login")]
    [InlineData("/\\example.com")]
    [InlineData("https://example.com")]
    public void Error_WithNonLocalExceptionPath_RedirectsHome(string badPath)
    {
        var sut = BuildSut(badPath);

        var result = sut.Error();

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/Home", redirect.Url);
    }

    [Theory]
    [InlineData("/organizations/f0b1a2c3-0000-0000-0000-000000000000")]
    [InlineData("/users")]
    [InlineData("/")]
    public void Error_WithLocalExceptionPath_RedirectsBackToThatPath(string localPath)
    {
        var sut = BuildSut(localPath);

        var result = sut.Error();

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal(localPath, redirect.Url);
    }

    [Fact]
    public void Error_WithoutExceptionFeature_RedirectsHome()
    {
        var sut = BuildSut(exceptionPath: null);

        var result = sut.Error();

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/Home", redirect.Url);
    }
}
