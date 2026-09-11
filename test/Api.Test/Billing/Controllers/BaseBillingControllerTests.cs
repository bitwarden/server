using Bit.Api.Billing.Controllers;
using Bit.Core.Billing.Commands;
using Bit.Core.Models.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using Unhandled = Bit.Core.Billing.Commands.Unhandled;

namespace Bit.Api.Test.Billing.Controllers;

/// <summary>
/// Regression tests for VULN-845 (PM-42560): <see cref="BaseBillingController"/> used to copy the
/// exception message and stack trace into every 500 response, with no environment guard, exposing
/// internal paths and assembly versions to any authenticated user.
/// </summary>
public class BaseBillingControllerTests
{
    private const string GenericMessage =
        "Something went wrong with your request. Please contact support for assistance.";

    [Fact]
    public void Handle_Unhandled_InProduction_OmitsExceptionDetail()
    {
        var sut = CreateSut("Production");

        var result = sut.HandleResult(new BillingCommandResult<string>(new Unhandled(BuildException())));

        var json = Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, json.StatusCode);
        Assert.Equal(GenericMessage, json.Value!.Message);
        Assert.Null(json.Value.ExceptionMessage);
        Assert.Null(json.Value.ExceptionStackTrace);
        Assert.Null(json.Value.InnerExceptionMessage);
    }

    [Fact]
    public void Handle_Unhandled_InDevelopment_IncludesExceptionDetail()
    {
        var sut = CreateSut("Development");
        var exception = BuildException();

        var result = sut.HandleResult(new BillingCommandResult<string>(new Unhandled(exception)));

        var json = Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, json.StatusCode);
        Assert.Equal(GenericMessage, json.Value!.Message);
        Assert.Equal(exception.Message, json.Value.ExceptionMessage);
        Assert.Equal(exception.StackTrace, json.Value.ExceptionStackTrace);
        Assert.Equal(exception.InnerException!.Message, json.Value.InnerExceptionMessage);
    }

    /// <summary>
    /// The environment is resolved off the request, so an unset <see cref="HttpContext"/> has to fail
    /// closed rather than throw or leak.
    /// </summary>
    [Fact]
    public void Handle_Unhandled_WithoutHttpContext_OmitsExceptionDetail()
    {
        var sut = new TestBillingController();

        var result = sut.HandleResult(new BillingCommandResult<string>(new Unhandled(BuildException())));

        var json = Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, json.StatusCode);
        Assert.Null(json.Value!.ExceptionMessage);
        Assert.Null(json.Value.ExceptionStackTrace);
    }

    [Fact]
    public void Handle_Success_ReturnsOk()
    {
        var sut = CreateSut("Production");

        var result = sut.HandleResult(new BillingCommandResult<string>("success"));

        var ok = Assert.IsType<Ok<string>>(result);
        Assert.Equal("success", ok.Value);
    }

    private static TestBillingController CreateSut(string environmentName)
    {
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        var services = new ServiceCollection();
        services.AddSingleton(environment);

        return new TestBillingController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() }
            }
        };
    }

    /// <summary>
    /// Throws and catches so the exception carries a real stack trace, which is the value under test.
    /// </summary>
    private static Exception BuildException()
    {
        try
        {
            throw new InvalidOperationException(
                "Connection to internal-billing-service failed.",
                new InvalidOperationException("Inner detail."));
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }
    }

    private class TestBillingController : BaseBillingController
    {
        public IResult HandleResult<T>(BillingCommandResult<T> result) => Handle(result);
    }
}
