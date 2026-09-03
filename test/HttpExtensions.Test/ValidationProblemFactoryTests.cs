using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xunit;

namespace Bit.HttpExtensions.Test;

public class ValidationProblemFactoryTests
{
    private sealed class Known
    {
        [StringLength(200)]
        public string? Name { get; set; }
    }

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
    public void AMappedPath_IsReportedWithItsCode()
    {
        var problem = ValidationProblemFactory.FromModelState(
            ModelState(("Name", "Name is too long.")), [typeof(Known)]);

        var error = Assert.Single(problem.Errors["name"]);
        Assert.Equal(ValidationCodes.TooLong, error.Type);
        Assert.Equal("Name is too long.", error.Detail);
    }

    [Fact]
    public void AnUnmappedPath_IsStillReported()
    {
        // Dropping it would answer 400 with an empty errors map, which tells the client less than nothing.
        var problem = ValidationProblemFactory.FromModelState(
            ModelState(("Mystery", "Something was wrong.")), [typeof(Known)]);

        var error = Assert.Single(problem.Errors["mystery"]);
        Assert.Equal(ValidationCodes.Invalid, error.Type);
        Assert.Equal("Something was wrong.", error.Detail);
    }

    [Fact]
    public void AnUnmappedNestedPath_IsCamelCasedSegmentBySegment()
    {
        var problem = ValidationProblemFactory.FromModelState(
            ModelState(("Owner.PostCode", "Bad.")), [typeof(Known)]);

        Assert.True(problem.Errors.ContainsKey("owner.postCode"));
    }

    [Fact]
    public void AModelLevelFailure_KeepsTheEmptyKey()
    {
        var problem = ValidationProblemFactory.FromModelState(
            ModelState((string.Empty, "Body is empty.")), [typeof(Known)]);

        var error = Assert.Single(problem.Errors[string.Empty]);
        Assert.Equal(ValidationCodes.Invalid, error.Type);
    }

    [Fact]
    public void SeveralFailuresOnOnePath_AreCollectedUnderIt()
    {
        var problem = ValidationProblemFactory.FromModelState(
            ModelState(("Name", "Name is too long."), ("Name", "Name is also wrong.")), [typeof(Known)]);

        Assert.Equal(2, problem.Errors["name"].Length);
    }

    [Fact]
    public void NoRootTypes_ReportsEverythingUncodedRatherThanThrowing()
    {
        var problem = ValidationProblemFactory.FromModelState(ModelState(("Name", "Name is too long.")), []);

        Assert.Equal(ValidationCodes.Invalid, Assert.Single(problem.Errors["name"]).Type);
    }

    [Fact]
    public void ValidModelState_ProducesAnEmptyErrorsMap() =>
        Assert.Empty(ValidationProblemFactory.FromModelState(new ModelStateDictionary(), [typeof(Known)]).Errors);

    [Fact]
    public void TheDocumentCarriesTheProblemMembers()
    {
        var problem = ValidationProblemFactory.FromModelState(
            ModelState(("Name", "Name is too long.")), [typeof(Known)]);

        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("validation_error", problem.Type);
        Assert.Equal("One or more validation errors occurred.", problem.Title);
    }

    [Fact]
    public void NullModelState_Throws() =>
        Assert.Throws<ArgumentNullException>(() => ValidationProblemFactory.FromModelState(null!, [typeof(Known)]));

    [Fact]
    public void NullRootTypes_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            ValidationProblemFactory.FromModelState(new ModelStateDictionary(), null!));
}
