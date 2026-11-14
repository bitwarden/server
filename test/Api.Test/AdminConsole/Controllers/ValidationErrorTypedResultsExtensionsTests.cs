using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;
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

    [Fact]
    public void BitwardenValidationProblem_WithErrorUsingDefaultStatus_Returns400ValidationProblem()
    {
        var result = TypedResults.BitwardenValidationProblem(new TestValidationError("email", "Invalid", "invalid"));

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("validation_error", result.ProblemDetails.Type);
    }

    [Fact]
    public void BitwardenValidationProblem_WithNotFoundError_Returns404()
    {
        var result = TypedResults.BitwardenValidationProblem(new TestNotFoundValidationError());

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("not_found", result.ProblemDetails.Type);
    }

    [Fact]
    public void BitwardenValidationProblem_WithEmailValidationError_Returns400()
    {
        var result = TypedResults.BitwardenValidationProblem(new MemberNotClaimedError());

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("validation_error", result.ProblemDetails.Type);
        Assert.Equal("One or more validation errors occurred.", result.ProblemDetails.Title);
    }

    [Fact]
    public void BitwardenValidationProblem_WithNameValidationError_Returns400()
    {
        var result = TypedResults.BitwardenValidationProblem(new NameChangeMemberNotClaimedError());

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("validation_error", result.ProblemDetails.Type);
        Assert.Equal("One or more validation errors occurred.", result.ProblemDetails.Title);
    }

    private sealed record TestValidationError(string PropertyName, string Message, string Type) : IValidationError;

    private sealed record TestNotFoundValidationError : IValidationError
    {
        public string PropertyName => "resource";
        public string Message => "Not found";
        public string Type => "notFound";
        public int StatusCode => 404;
    }
}
