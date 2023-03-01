using Bit.Core.Entities;
using Core.Vault.Models.Data;

namespace Bit.Core.Models.Data;

public class EmergencyAccessViewData
{
    public EmergencyAccess EmergencyAccess { get; set; }
    public IEnumerable<CipherDetails> Ciphers { get; set; }
}
