using Bit.Core.Enums;

namespace Bit.Core.Models.Data;

public class UserKdfInformation
{
    public KdfType Kdf { get; set; }
    public int KdfIterations { get; set; }
    public int? KdfMemory { get; set; }
    public int? KdfParallelism { get; set; }
}
