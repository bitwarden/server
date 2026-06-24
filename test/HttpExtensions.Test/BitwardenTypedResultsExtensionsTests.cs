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
}
