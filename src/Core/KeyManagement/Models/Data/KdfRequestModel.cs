using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;

namespace Bit.Core.KeyManagement.Models.Data;

public class KdfRequestModel
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
}
