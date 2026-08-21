using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Utilities.v2.Validation;

public class ValidationErrorTypedResultsExtensionsTests
{
    [Fact]
    public void BitwardenValidationProblem_WithValidationError_KeysByPropertyName()
    {
        var validationError = new TestValidationError("email", "Member not claimed", "memberNotClaimed");

        var result = TypedResults.BitwardenValidationProblem(validationError);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        var errors = result.ProblemDetails.Errors;
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
            TypedResults.BitwardenValidationProblem((IValidationError)null!));
    }

    [Fact]
    public void BitwardenValidationProblem_WithStatusCode_CarriesTheProblemOnThatStatus()
    {
        var validationError = new TestValidationError("code", "Access is already active.", "accessAlreadyActive");

        var result = TypedResults.BitwardenValidationProblem(
            validationError,
            title: "The request conflicts with the current state.",
            type: "conflict_error",
            statusCode: StatusCodes.Status409Conflict);

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("The request conflicts with the current state.", result.ProblemDetails.Title);
        Assert.Equal("conflict_error", result.ProblemDetails.Type);
    }

    [Fact]
    public void BitwardenValidationProblem_WithErrorsOnDifferentProperties_KeysEachSeparately()
    {
        IValidationError[] validationErrors =
        [
            new TestValidationError("email", "Email is required.", "required"),
            new TestValidationError("name", "Name is required.", "required"),
        ];

        var result = TypedResults.BitwardenValidationProblem(validationErrors);

        var errors = result.ProblemDetails.Errors;
        Assert.Equal(2, errors.Count);
        Assert.Equal("required", Assert.Single(errors["email"]).Type);
        Assert.Equal("required", Assert.Single(errors["name"]).Type);
    }

    [Fact]
    public void BitwardenValidationProblem_WithErrorsOnOneProperty_CollectsThemUnderIt()
    {
        // A model that fails several ways at once reports every failure, rather than the last one winning.
        IValidationError[] validationErrors =
        [
            new TestValidationError("password", "Password is too short.", "tooShort"),
            new TestValidationError("password", "Password needs a digit.", "missingDigit"),
        ];

        var result = TypedResults.BitwardenValidationProblem(validationErrors);

        var errors = result.ProblemDetails.Errors;
        var entry = Assert.Single(errors);
        Assert.Equal("password", entry.Key);
        Assert.Equal(["tooShort", "missingDigit"], entry.Value.Select(error => error.Type));
    }

    [Fact]
    public void BitwardenValidationProblem_WithNullValidationErrors_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TypedResults.BitwardenValidationProblem((IEnumerable<IValidationError>)null!));
    }

    private sealed record TestValidationError(string PropertyName, string Message, string Type) : IValidationError;
}
