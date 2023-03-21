using Bit.Core.Auth.Entities;
using Bit.Core.Vault.Models.Data;

namespace Bit.Core.Auth.Models.Data;

public class EmergencyAccessViewData
{
    public EmergencyAccess EmergencyAccess { get; set; }
    public IEnumerable<CipherDetails> Ciphers { get; set; }
}
