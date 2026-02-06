// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Vault.Models.Data;

public class CipherPasswordHistoryData
{
    public CipherPasswordHistoryData() { }

    public string Password { get; set; }
    public DateTime LastUsedDate { get; set; }
}
