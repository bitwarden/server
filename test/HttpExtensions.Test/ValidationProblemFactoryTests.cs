using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xunit;

namespace Bit.HttpExtensions.Test;

public class ValidationProblemFactoryTests
{
    private static ModelStateDictionary ModelState(params (string Key, string Message)[] errors)
    {
        var modelState = new ModelStateDictionary();
        foreach (var (key, message) in errors)
        {
            modelState.AddModelError(key, message);
        }

        return modelState;
    }

    [Fact]
    public void ARecognisedMessage_IsReportedWithItsCode()
    {
        var problem = ValidationProblemFactory.FromModelState(
            ModelState(("Name", "The Name field is required.")));

        var error = Assert.Single(problem.Errors["name"]);
        Assert.Equal(ValidationCodes.Required, error.Type);
        Assert.Equal("The Name field is required.", error.Detail);
    }

    [Fact]
    public void AnUnrecognisedMessage_IsStillReported()
    {
        // Dropping it would answer 400 with an empty errors map, which tells the client less than nothing.
        var problem = ValidationProblemFactory.FromModelState(ModelState(("Mystery", "Something was wrong.")));

        var error = Assert.Single(problem.Errors["mystery"]);
        Assert.Equal(ValidationCodes.Invalid, error.Type);
        Assert.Equal("Something was wrong.", error.Detail);
    }

    [Fact]
    public void ANestedPath_IsCamelCasedSegmentBySegment()
    {
        var problem = ValidationProblemFactory.FromModelState(ModelState(("Owner.PostCode", "Bad.")));

        Assert.True(problem.Errors.ContainsKey("owner.postCode"));
    }

    [Fact]
    public void ACollectionElement_KeepsItsIndex()
    {
        var problem = ValidationProblemFactory.FromModelState(
            ModelState(("Members[1].Email", "The Email field is required.")));

        Assert.Equal(ValidationCodes.Required, Assert.Single(problem.Errors["members[1].email"]).Type);
    }

    [Fact]
    public void AModelLevelFailure_KeepsTheEmptyKey()
    {
        var problem = ValidationProblemFactory.FromModelState(ModelState((string.Empty, "Body is empty.")));

        Assert.Equal(ValidationCodes.Invalid, Assert.Single(problem.Errors[string.Empty]).Type);
    }

    [Fact]
    public void SeveralFailuresOnOnePath_AreCollectedUnderIt()
    {
        var problem = ValidationProblemFactory.FromModelState(
            ModelState(("Name", "The Name field is required."), ("Name", "Name is also wrong.")));

        Assert.Equal(2, problem.Errors["name"].Length);
    }

    [Fact]
    public void ValidModelState_ProducesAnEmptyErrorsMap() =>
        Assert.Empty(ValidationProblemFactory.FromModelState(new ModelStateDictionary()).Errors);

    [Fact]
    public void TheDocumentCarriesTheProblemMembers()
    {
        var problem = ValidationProblemFactory.FromModelState(ModelState(("Name", "The Name field is required.")));

        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("validation_error", problem.Type);
        Assert.Equal("One or more validation errors occurred.", problem.Title);
    }

    [Fact]
    public void NullModelState_Throws() =>
        Assert.Throws<ArgumentNullException>(() => ValidationProblemFactory.FromModelState(null!));
}
