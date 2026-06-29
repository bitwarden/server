using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;
using static Microsoft.AspNetCore.Http.HttpResults.BitwardenTypedResultsExtensions;

namespace Bit.Api.Test.AdminConsole.Controllers;

public class ValidationErrorTypedResultsExtensionsTests
{
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
        Assert.Throws<ArgumentNullException>(() =>
            TypedResults.BitwardenValidationProblem(null!));
    }

    private sealed record TestValidationError(string PropertyName, string Message, string Type) : IValidationError;
}
