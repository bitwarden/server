using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Utilities;

/// <summary>
/// Validates each string element of a collection using <typeparamref name="TValidator"/>, reporting
/// all invalid items in a single <see cref="ValidationResult"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class ValidateSequenceAttribute<TValidator> : ValidationAttribute
    where TValidator : ValidationAttribute, new()
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var items = (value as IEnumerable<string> ?? []).ToList();
        var validator = new TValidator();
        var invalid = items.Where(item => !validator.IsValid(item)).ToList();

        if (invalid.Count == 0) return ValidationResult.Success;

        var memberNames = new[] { validationContext.MemberName ?? validationContext.DisplayName };
        var message = $"The following items are not valid: {string.Join(", ", invalid.Select(v => $"'{v}'"))}";

        return new ValidationResult(message, memberNames!);
    }
}
