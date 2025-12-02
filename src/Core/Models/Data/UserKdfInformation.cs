using Bit.Core.Enums;

namespace Bit.Core.Models.Data;

public class UserKdfInformation
{
    public required KdfType Kdf { get; set; }
    public required int KdfIterations { get; set; }
    public int? KdfMemory { get; set; }
    public int? KdfParallelism { get; set; }
}
