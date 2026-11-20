using System.ComponentModel.DataAnnotations;
using Bit.Api.SecretsManager.Models.Request;
using Xunit;

namespace Bit.Api.Test.SecretsManager.Models.Request;

public class RequestSMAccessRequestModelTests
{
    [Theory]
    [InlineData("I need access to the deployment secrets.")]
    [InlineData("x")]
    public void Validate_WithReasonableNote_ReturnsNoErrors(string emailContent)
    {
        var results = Validate(BuildModel(emailContent));

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_WithNoteAtMaxLength_ReturnsNoErrors()
    {
        var results = Validate(BuildModel(new string('a', RequestSMAccessRequestModel.MaxEmailContentLength)));

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_WithNoteOverMaxLength_ReturnsError()
    {
        var results = Validate(BuildModel(new string('a', RequestSMAccessRequestModel.MaxEmailContentLength + 1)));

        var error = Assert.Single(results);
        Assert.Contains(nameof(RequestSMAccessRequestModel.EmailContent), error.MemberNames);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_WithMissingNote_ReturnsError(string? emailContent)
    {
        var results = Validate(BuildModel(emailContent));

        var error = Assert.Single(results);
        Assert.Contains(nameof(RequestSMAccessRequestModel.EmailContent), error.MemberNames);
    }

    private static RequestSMAccessRequestModel BuildModel(string? emailContent) => new()
    {
        OrganizationId = Guid.NewGuid(),
        EmailContent = emailContent!,
    };

    private static List<ValidationResult> Validate(RequestSMAccessRequestModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }
}
