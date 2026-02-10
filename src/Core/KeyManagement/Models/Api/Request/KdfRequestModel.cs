using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Core.KeyManagement.Models.Api.Request;

public class KdfRequestModel : IValidatableObject
{
    [Required]
    public required KdfType KdfType { get; init; }
    [Required]
    public required int Iterations { get; init; }
    public int? Memory { get; init; }
    public int? Parallelism { get; init; }

    public KdfSettings ToData()
    {
        return new KdfSettings
        {
            KdfType = KdfType,
            Iterations = Iterations,
            Memory = Memory,
            Parallelism = Parallelism
        };
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Generic per-request KDF validation for any request model embedding KdfRequestModel
        return KdfSettingsValidator.Validate(ToData());
    }
}
