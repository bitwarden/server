using Bit.Core.Enums;

namespace Bit.Core.KeyManagement.Models.Data;

public class MasterPasswordUnlockData
{
    public KdfType KdfType { get; set; }
    public int KdfIterations { get; set; }
    public int? KdfMemory { get; set; }
    public int? KdfParallelism { get; set; }

    public string Email { get; set; }
    public string MasterPasswordHash { get; set; }
    public string MasterKeyEncryptedUserKey { get; set; }
}
