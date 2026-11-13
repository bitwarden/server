using Bit.Core.Exceptions;
using Bit.Scim.Models;
using Bit.Scim.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Bit.Scim.Test.Utilities;

public class ExceptionHandlerFilterAttributeTests
{
    [Fact]
    public void OnException_ScimInvalidFilterException_Returns400WithInvalidFilterScimType()
    {
        var sut = new ExceptionHandlerFilterAttribute();
        var context = CreateContext(new ScimInvalidFilterException("Filter attribute 'active' is not supported."));

        sut.OnException(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        var model = Assert.IsType<ScimErrorResponseModel>(result.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, model.Status);
        Assert.Equal(StatusCodes.Status400BadRequest, context.HttpContext.Response.StatusCode);
        Assert.Equal(ScimErrorTypes.InvalidFilter, model.ScimType);
        Assert.Equal("Filter attribute 'active' is not supported.", model.Detail);
    }

    [Fact]
    public void OnException_BadRequestException_Returns400WithoutScimType()
    {
        var sut = new ExceptionHandlerFilterAttribute();
        var context = CreateContext(new BadRequestException("bad"));

        sut.OnException(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        var model = Assert.IsType<ScimErrorResponseModel>(result.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, model.Status);
        Assert.Null(model.ScimType);
    }

    [Fact]
    public void OnException_NotFoundException_Returns404()
    {
        var sut = new ExceptionHandlerFilterAttribute();
        var context = CreateContext(new NotFoundException("missing"));

        sut.OnException(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        var model = Assert.IsType<ScimErrorResponseModel>(result.Value);
        Assert.Equal(StatusCodes.Status404NotFound, model.Status);
        Assert.Null(model.ScimType);
    }

    private static ExceptionContext CreateContext(System.Exception exception)
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor());

        return new ExceptionContext(actionContext, new List<IFilterMetadata>())
        {
            Exception = exception
        };
    }
}
