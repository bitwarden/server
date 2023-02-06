using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Request.Accounts;

public class KdfRequestModel : PasswordRequestModel, IValidatableObject
{
    [Required]
    public KdfType? Kdf { get; set; }
    [Required]
    public int? KdfIterations { get; set; }
    public int? KdfMemory { get; set; }
    public int? KdfParallelism { get; set; }

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Kdf.HasValue && KdfIterations.HasValue)
        {
            return KdfSettingsValidator.Validate(Kdf.Value, KdfIterations.Value, KdfMemory, KdfParallelism);
        }

        return Enumerable.Empty<ValidationResult>();
    }
}
