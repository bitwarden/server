using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;

namespace Bit.Api.Models.Request.Accounts;

public class KdfRequestModel : PasswordRequestModel, IValidatableObject
{
    [Required]
    public KdfType? Kdf { get; set; }
    [Required]
    public int? KdfIterations { get; set; }

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Kdf.HasValue && KdfIterations.HasValue)
        {
            switch (Kdf.Value)
            {
                case KdfType.PBKDF2_SHA256:
                    if (KdfIterations.Value < 5000 || KdfIterations.Value > 2_000_000)
                    {
                        yield return new ValidationResult("KDF iterations must be between 5000 and 2000000.");
                    }
                    break;
                default:
                    break;
            }
        }
    }
}
