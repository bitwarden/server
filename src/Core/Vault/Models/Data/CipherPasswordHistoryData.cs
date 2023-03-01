namespace Bit.Core.Vault.Models.Data;

public class CipherPasswordHistoryData
{
    public CipherPasswordHistoryData() { }

    public string Password { get; set; }
    public DateTime LastUsedDate { get; set; }
}
