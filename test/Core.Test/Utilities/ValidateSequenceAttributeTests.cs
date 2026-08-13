using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class ValidateSequenceAttributeTests
{
    [Fact]
    public void IsValid_WithEmptyCollection_ReturnsSuccess()
    {
        var result = Validate([]);

        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithNullCollection_ReturnsSuccess()
    {
        var result = Validate(null);

        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithSomeInvalidItems_ReturnsErrorMessage()
    {
        var result = Validate(["bad", "also-bad", "ok"]);

        Assert.NotNull(result);
        Assert.Contains("'bad'", result!.ErrorMessage);
        Assert.Contains("'also-bad'", result.ErrorMessage);
        Assert.DoesNotContain("ok", result.ErrorMessage);
    }

    /// <summary>
    /// Invokes <see cref="ValidateSequenceAttribute{TValidator}"/> directly so edge cases
    /// (null/empty collection) can be tested in isolation, independent of any model's
    /// <c>[Required]</c> attribute which would intercept those cases first.
    /// </summary>
    private static ValidationResult? Validate(IEnumerable<string>? value)
    {
        var attr = new ValidateSequenceAttribute<OnlyAcceptsOkValidator>();
        return attr.GetValidationResult(value, new ValidationContext(new object()));
    }

    /// <summary>
    /// Accepts only the literal string "ok" so tests are not coupled to
    /// <see cref="DomainNameValidatorAttribute"/> regex rules.
    /// </summary>
    private class OnlyAcceptsOkValidator : ValidationAttribute
    {
        public override bool IsValid(object? value) => value?.ToString() == "ok";
    }
}
