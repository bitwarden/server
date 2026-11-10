using Bit.Scim.Models;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace Bit.Scim.Test.Models;

public class GetGroupsQueryParamModelTests
{
    [Fact]
    public void GetGroupsQueryParamModel_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var model = new GetGroupsQueryParamModel();

        // Assert
        Assert.Equal(1, model.StartIndex);
        Assert.Equal(50, model.Count);
        Assert.Equal(1, model.MemberStartIndex);
        Assert.Equal(50, model.MemberCount);
        Assert.Empty(model.Filter);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void GetGroupsQueryParamModel_ValidMemberStartIndex_Passes(int memberStartIndex)
    {
        // Arrange
        var model = new GetGroupsQueryParamModel { MemberStartIndex = memberStartIndex };
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
    public void GetGroupsQueryParamModel_InvalidMemberStartIndex_Fails(int memberStartIndex)
    {
        // Arrange
        var model = new GetGroupsQueryParamModel { MemberStartIndex = memberStartIndex };
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(model, context, results, validateAllProperties: true);

        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(model.MemberStartIndex)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void GetGroupsQueryParamModel_ValidMemberCount_Passes(int memberCount)
    {
        // Arrange
        var model = new GetGroupsQueryParamModel { MemberCount = memberCount };
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
    public void GetGroupsQueryParamModel_InvalidMemberCount_Fails(int memberCount)
    {
        // Arrange
        var model = new GetGroupsQueryParamModel { MemberCount = memberCount };
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(model, context, results, validateAllProperties: true);

        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(model.MemberCount)));
    }
}
