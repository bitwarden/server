using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Utilities;

public class EncryptedStringLengthAttribute : StringLengthAttribute
{
    public EncryptedStringLengthAttribute(int maximumLength)
        : base(maximumLength) { }

    public override string FormatErrorMessage(string name)
    {
        return string.Format(
            "The field {0} exceeds the maximum encrypted value length of {1} characters.",
            name,
            MaximumLength
        );
    }
}
