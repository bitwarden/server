namespace Bit.Core.Dirt.Reports.Models.Data;

public class PasskeyDirectoryEntry
{
    public string DomainName { get; set; } = string.Empty;
    public bool Passwordless { get; set; }
    public bool Mfa { get; set; }
    public string Instructions { get; set; } = string.Empty;
}
