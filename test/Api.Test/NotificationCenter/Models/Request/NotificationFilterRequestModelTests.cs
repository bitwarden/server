#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Api.NotificationCenter.Models.Request;
using Xunit;

namespace Bit.Api.Test.NotificationCenter.Models.Request;

public class NotificationFilterRequestModelTests
{
    [Theory]
    [InlineData("invalid")]
    [InlineData("-1")]
    [InlineData("0")]
    public void Validate_ContinuationTokenInvalidNumber_Invalid(string continuationToken)
    {
        var model = new NotificationFilterRequestModel
        {
            ContinuationToken = continuationToken,
        };
        var result = Validate(model);
        Assert.Single(result);
        Assert.Contains("Continuation token must be a positive, non zero integer.", result[0].ErrorMessage);
        Assert.Contains("ContinuationToken", result[0].MemberNames);
    }

    [Fact]
    public void Validate_ContinuationTokenMaxLengthExceeded_Invalid()
    {
        var model = new NotificationFilterRequestModel
        {
            ContinuationToken = "1234567890"
        };
        var result = Validate(model);
        Assert.Single(result);
        Assert.Contains("The field ContinuationToken must be a string with a maximum length of 9.",
            result[0].ErrorMessage);
        Assert.Contains("ContinuationToken", result[0].MemberNames);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("1")]
    [InlineData("123456789")]
    public void Validate_ContinuationTokenCorrect_Valid(string? continuationToken)
    {
        var model = new NotificationFilterRequestModel
        {
            ContinuationToken = continuationToken
        };
        var result = Validate(model);
        Assert.Empty(result);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(1001)]
    public void Validate_PageSizeInvalidRange_Invalid(int pageSize)
    {
        var model = new NotificationFilterRequestModel
        {
            PageSize = pageSize
        };
        var result = Validate(model);
        Assert.Single(result);
        Assert.Contains("The field PageSize must be between 10 and 1000.", result[0].ErrorMessage);
        Assert.Contains("PageSize", result[0].MemberNames);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(10)]
    [InlineData(1000)]
    public void Validate_PageSizeCorrect_Valid(int? pageSize)
    {
        var model = pageSize == null
            ? new NotificationFilterRequestModel()
            : new NotificationFilterRequestModel
            {
                PageSize = pageSize.Value
            };
        var result = Validate(model);
        Assert.Empty(result);
    }

    private static List<ValidationResult> Validate(NotificationFilterRequestModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }
}
