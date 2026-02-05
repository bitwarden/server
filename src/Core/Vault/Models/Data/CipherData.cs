// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Vault.Models.Data;

public abstract class CipherData
{
    public CipherData() { }

    public string Name { get; set; }
    public string Notes { get; set; }
    public IEnumerable<CipherFieldData> Fields { get; set; }
    public IEnumerable<CipherPasswordHistoryData> PasswordHistory { get; set; }
}
