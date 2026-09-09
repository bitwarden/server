using System.ComponentModel.DataAnnotations;

namespace Bit.Admin.Utilities;

[AttributeUsage(AttributeTargets.Property)]
public class MaxFileSizeAttribute : ValidationAttribute
{
    private readonly long _maxBytes;

    public MaxFileSizeAttribute(long maxBytes)
        : base("The file exceeds the maximum allowed size.")
    {
        _maxBytes = maxBytes;
    }

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value is IFormFile file && file.Length <= _maxBytes;
    }

    public override string FormatErrorMessage(string name) =>
        $"{name} exceeds the {_maxBytes / (1024 * 1024)} MB limit.";
}
