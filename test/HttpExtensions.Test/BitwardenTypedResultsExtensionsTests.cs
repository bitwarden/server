using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;

namespace Bit.HttpExtensions.Test;

public class BitwardenTypedResultsExtensionsTests
{
    [Fact]
    public void BitwardenValidationProblem_WithErrorsDictionary_Returns400CarryingThem()
    {
        var errors = new Dictionary<string, ErrorCode[]>
        {
            { "email", [new ErrorCode("invalid", "Email is invalid.")] }
        };

        var result = TypedResults.BitwardenValidationProblem(errors);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("One or more validation errors occurred.", result.ProblemDetails.Title);
        Assert.Equal("validation_error", result.ProblemDetails.Type);
        Assert.Equal("invalid", Assert.Single(result.ProblemDetails.Errors["email"]).Type);
    }

    [Fact]
    public void BitwardenValidationProblem_WithExtensions_PreservesCallerExtensionsAndAddsErrors()
    {
        var errors = new Dictionary<string, ErrorCode[]>
        {
            { "email", [new ErrorCode("invalid", "Email is invalid.")] }
        };
        var callerExtensions = new Dictionary<string, object?>
        {
            { "traceId", "abc-123" }
        };

        var result = TypedResults.BitwardenValidationProblem(errors, extensions: callerExtensions);

        Assert.Equal("abc-123", result.ProblemDetails.Extensions["traceId"]);
        Assert.Equal("invalid", Assert.Single(result.ProblemDetails.Errors["email"]).Type);
    }

    [Fact]
    public void BitwardenValidationProblem_WithExtensionsContainingErrorsKey_DropsItForTheErrorsMap()
    {
        var errors = new Dictionary<string, ErrorCode[]>
        {
            { "email", [new ErrorCode("invalid", "Email is invalid.")] }
        };
        var callerExtensions = new Dictionary<string, object?>
        {
            { "errors", "should be dropped" }
        };

        var result = TypedResults.BitwardenValidationProblem(errors, extensions: callerExtensions);

        Assert.DoesNotContain("errors", result.ProblemDetails.Extensions.Keys);
        Assert.Equal("invalid", Assert.Single(result.ProblemDetails.Errors["email"]).Type);
    }

    [Fact]
    public void BitwardenValidationProblem_WithAStatusCode_UsesItInsteadOf400()
    {
        var errors = new Dictionary<string, ErrorCode[]>
        {
            { "code", [new ErrorCode("already_active", "You already have this.")] }
        };

        var result = TypedResults.BitwardenValidationProblem(
            errors, title: "The request conflicts with the current state.", type: "conflict_error",
            statusCode: StatusCodes.Status409Conflict);

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal(StatusCodes.Status409Conflict, result.ProblemDetails.Status);
        Assert.Equal("conflict_error", result.ProblemDetails.Type);
        Assert.Equal("already_active", Assert.Single(result.ProblemDetails.Errors["code"]).Type);
    }

    [Fact]
    public void BitwardenValidationProblem_WithNullErrors_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TypedResults.BitwardenValidationProblem(((IDictionary<string, ErrorCode[]>)null!)!));
    }

    [Fact]
    public void BitwardenValidationProblem_WithPairs_GroupsThemByProperty()
    {
        (string, ErrorCode)[] errors =
        [
            ("password", new ErrorCode("too_short", "Password is too short.")),
            ("email", new ErrorCode("invalid", "Email is invalid.")),
            ("password", new ErrorCode("missing_digit", "Password needs a digit.")),
        ];

        var result = TypedResults.BitwardenValidationProblem(errors);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        var grouped = result.ProblemDetails.Errors;
        Assert.Equal(2, grouped.Count);
        Assert.Equal("invalid", Assert.Single(grouped["email"]).Type);
        Assert.Equal(["too_short", "missing_digit"], grouped["password"].Select(error => error.Type));
    }

    [Fact]
    public void BitwardenValidationProblem_WithPairsAndAStatusCode_UsesItInsteadOf400()
    {
        (string, ErrorCode)[] errors = [("code", new ErrorCode("already_active", "You already have this."))];

        var result = TypedResults.BitwardenValidationProblem(
            errors, title: "The request conflicts with the current state.", type: "conflict_error",
            statusCode: StatusCodes.Status409Conflict);

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("conflict_error", result.ProblemDetails.Type);
    }

    [Fact]
    public void BitwardenValidationProblem_WithNullPairs_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TypedResults.BitwardenValidationProblem((IEnumerable<(string, ErrorCode)>)null!));
    }
}
