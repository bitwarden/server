using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class KdfRequestModel : PasswordRequestModel, IValidatableObject
{
    [Required]
    public required KdfType Kdf { get; set; }
    [Required]
    public required int KdfIterations { get; set; }
    public int? KdfMemory { get; set; }
    public int? KdfParallelism { get; set; }

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        return KdfSettingsValidator.Validate(Kdf, KdfIterations, KdfMemory, KdfParallelism);
    }

    public KdfSettings ToKdfSettings()
    {
        return new KdfSettings
        {
            KdfType = Kdf,
            Iterations = KdfIterations,
            Memory = KdfMemory,
            Parallelism = KdfParallelism
        };
    }
}
