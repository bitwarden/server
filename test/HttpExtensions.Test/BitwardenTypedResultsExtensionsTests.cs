using Bit.Core.AdminConsole.Utilities.v2.Validation;
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
    public void BitwardenValidationProblem_WithValidationError_KeysByPropertyName()
    {
        var validationError = new TestValidationError("email", "Member not claimed", "memberNotClaimed");

        var result = TypedResults.BitwardenValidationProblem(validationError);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        var errors = Assert.IsType<Dictionary<string, ErrorCode[]>>(result.ProblemDetails.Extensions["errors"]);
        var entry = Assert.Single(errors);
        Assert.Equal("email", entry.Key);
        var error = Assert.Single(entry.Value);
        Assert.Equal("memberNotClaimed", error.Type);
        Assert.Equal("Member not claimed", error.Detail);
    }

    [Fact]
    public void BitwardenValidationProblem_WithNullValidationError_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TypedResults.BitwardenValidationProblem(null!));
    }

    private sealed record TestValidationError(string PropertyName, string Message, string Type) : IValidationError;
}
