// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Auth.Entities;
using Bit.Core.Vault.Models.Data;

namespace Bit.Core.Auth.Models.Data;

public class EmergencyAccessViewData
{
    public EmergencyAccess EmergencyAccess { get; set; }
    public IEnumerable<CipherDetails> Ciphers { get; set; }
}
