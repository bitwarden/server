using System.ComponentModel.DataAnnotations;
using Bit.Scim.Models;
using Xunit;

namespace Bit.Scim.Test.Models;

public class GetUsersQueryParamModelTests
{
    [Fact]
    public void GetUsersQueryParamModel_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var model = new GetUsersQueryParamModel();

        // Assert
        Assert.Equal(1, model.StartIndex);
        Assert.Equal(50, model.Count);
        Assert.Equal(1, model.GroupStartIndex);
        Assert.Equal(int.MaxValue, model.GroupCount);
        Assert.Empty(model.Filter);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void GetUsersQueryParamModel_ValidGroupStartIndex_Passes(int groupStartIndex)
    {
        // Arrange
        var model = new GetUsersQueryParamModel { GroupStartIndex = groupStartIndex };
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(model, context, results, validateAllProperties: true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void GetUsersQueryParamModel_InvalidGroupStartIndex_Fails(int groupStartIndex)
    {
        // Arrange
        var model = new GetUsersQueryParamModel { GroupStartIndex = groupStartIndex };
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(model, context, results, validateAllProperties: true);

        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(model.GroupStartIndex)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void GetUsersQueryParamModel_ValidGroupCount_Passes(int groupCount)
    {
        // Arrange
        var model = new GetUsersQueryParamModel { GroupCount = groupCount };
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(model, context, results, validateAllProperties: true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void GetUsersQueryParamModel_InvalidGroupCount_Fails(int groupCount)
    {
        // Arrange
        var model = new GetUsersQueryParamModel { GroupCount = groupCount };
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(model, context, results, validateAllProperties: true);

        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(model.GroupCount)));
    }
}
