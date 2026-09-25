using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Bit.Core.Utilities;

/// <summary>
/// Validates each element of a collection using <typeparamref name="TValidator"/>.
/// The property must be <see cref="IEnumerable{T}"/> of a reference type (e.g. <c>IEnumerable&lt;string&gt;</c>).
/// An empty collection passes validation; use <c>[MinLength(1)]</c> if an empty collection should be invalid.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class ValidateSequenceAttribute<TValidator> : ValidationAttribute
    where TValidator : ValidationAttribute, new()
{
    private const string _invalidItemsMessage = "The following items are not valid: {0}";

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        if (value is not IEnumerable<object> items)
        {
            throw new ArgumentException("ValidateSequenceAttribute can only be used with IEnumerable<T> properties.");
        }

        var validator = new TValidator();
        var invalid = items.Where(item => !validator.IsValid(item)).ToList();

        if (invalid.Count == 0)
        {
            return ValidationResult.Success;
        }

        var memberNames = new[] { validationContext.MemberName ?? validationContext.DisplayName };
        var message = string.Format(CultureInfo.InvariantCulture, _invalidItemsMessage, string.Join(", ", invalid.Select(value => $"'{value}'")));

        return new ValidationResult(message, memberNames!);
    }
}
