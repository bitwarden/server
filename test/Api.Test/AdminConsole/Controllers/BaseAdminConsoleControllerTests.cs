using Bit.Api.AdminConsole.Controllers;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.HttpExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;
using CommandError = Bit.Core.AdminConsole.Utilities.v2.Error;

namespace Bit.Api.Test.AdminConsole.Controllers;

public class BaseAdminConsoleControllerTests
{
    private readonly TestController _controller = new();

    [Fact]
    public void Handle_WithSuccess_ReturnsSuccessResponse()
    {
        Result<string, BadRequestValidationError> result = "value";

        var response = _controller.HandleResult(result, value => TypedResults.Ok(value));

        var ok = Assert.IsType<Ok<string>>(response);
        Assert.Equal("value", ok.Value);
    }

    [Fact]
    public void Handle_WithBadRequestValidationError_Returns400ValidationProblem()
    {
        Result<string, BadRequestValidationError> result = new BadRequestValidationError();

        var response = _controller.HandleResult(result, value => TypedResults.Ok(value));

        var problem = Assert.IsType<BitwardenValidationProblemResult>(response);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        var errors = Assert.IsType<Dictionary<string, BitwardenTypedResultsExtensions.ErrorCode[]>>(
            problem.ProblemDetails.Extensions["errors"]);
        var error = Assert.Single(errors["field"]);
        Assert.Equal("badRequestCode", error.Type);
    }

    [Fact]
    public void Handle_WithNotFoundValidationError_Returns404ValidationProblem()
    {
        Result<string, NotFoundValidationError> result = new NotFoundValidationError();

        var response = _controller.HandleResult(result, value => TypedResults.Ok(value));

        var problem = Assert.IsType<BitwardenValidationProblemResult>(response);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
        Assert.Equal("not_found", problem.ProblemDetails.Type);
    }

    private sealed class TestController : BaseAdminConsoleController
    {
        public IResult HandleResult<T, TError>(Result<T, TError> result, Func<T, IResult> success)
            where TError : CommandError, IValidationError =>
            Handle(result, success);
    }

    private sealed record BadRequestValidationError() : CommandError("Bad request"), IValidationError
    {
        public string PropertyName => "field";
        public string Type => "badRequestCode";
    }

    private sealed record NotFoundValidationError() : CommandError("Not found"), IValidationError
    {
        public string PropertyName => "resource";
        public string Type => "notFoundCode";
        public int StatusCode => 404;
    }
}
