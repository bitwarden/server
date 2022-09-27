using Bit.Core.Enums;

namespace Bit.Core.Models.Data;

public class UserKdfInformation
{
    public KdfType Kdf { get; set; }
    public int KdfIterations { get; set; }
}
