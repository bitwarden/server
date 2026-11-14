using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;
using static Microsoft.AspNetCore.Http.HttpResults.BitwardenTypedResultsExtensions;

namespace Bit.HttpExtensions.Test;

public class BitwardenTypedResultsExtensionsTests
{
    [Fact]
    public void BitwardenValidationProblem_WithErrorsDictionary_Returns400WithErrorsExtension()
    {
        var errors = new Dictionary<string, ErrorCode[]>
        {
            { "email", [new ErrorCode("invalid", "Email is invalid.")] }
        };

        var result = TypedResults.BitwardenValidationProblem(errors);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("One or more validation errors occurred.", result.ProblemDetails.Title);
        Assert.Equal("validation_error", result.ProblemDetails.Type);
        Assert.True(result.ProblemDetails.Extensions.ContainsKey("errors"));
        Assert.Same(errors, result.ProblemDetails.Extensions["errors"]);
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
        Assert.Same(errors, result.ProblemDetails.Extensions["errors"]);
    }

    [Fact]
    public void BitwardenValidationProblem_WithExtensionsContainingErrorsKey_OverwritesWithProvidedErrors()
    {
        var errors = new Dictionary<string, ErrorCode[]>
        {
            { "email", [new ErrorCode("invalid", "Email is invalid.")] }
        };
        var callerExtensions = new Dictionary<string, object?>
        {
            { "errors", "should be overwritten" }
        };

        var result = TypedResults.BitwardenValidationProblem(errors, extensions: callerExtensions);

        Assert.Same(errors, result.ProblemDetails.Extensions["errors"]);
    }

    [Fact]
    public void BitwardenValidationProblem_WithNullErrors_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TypedResults.BitwardenValidationProblem(((IDictionary<string, ErrorCode[]>)null!)!));
    }

    [Fact]
    public void BitwardenValidationProblem_WithNotFoundStatus_ReturnsNotFoundTypeAndTitle()
    {
        var errors = new Dictionary<string, ErrorCode[]>
        {
            { "inviteLink", [new ErrorCode("not_found", "Invite link not found.")] }
        };

        var result = TypedResults.BitwardenValidationProblem(errors, statusCode: StatusCodes.Status404NotFound);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("not_found", result.ProblemDetails.Type);
        Assert.Equal("The requested resource was not found.", result.ProblemDetails.Title);
    }

    [Fact]
    public void BitwardenValidationProblem_WithExplicitTypeAndTitle_UsesThem()
    {
        var errors = new Dictionary<string, ErrorCode[]>
        {
            { "inviteLink", [new ErrorCode("conflict", "Conflict.")] }
        };

        var result = TypedResults.BitwardenValidationProblem(errors, title: "Custom", type: "custom",
            statusCode: StatusCodes.Status409Conflict);

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("custom", result.ProblemDetails.Type);
        Assert.Equal("Custom", result.ProblemDetails.Title);
    }

    [Fact]
    public void BitwardenValidationProblem_WithUnsupportedStatusAndNoTypeOrTitle_Throws()
    {
        var errors = new Dictionary<string, ErrorCode[]>
        {
            { "inviteLink", [new ErrorCode("conflict", "Conflict.")] }
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TypedResults.BitwardenValidationProblem(errors, statusCode: StatusCodes.Status409Conflict));
    }
}
