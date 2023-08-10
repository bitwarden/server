namespace Bit.Core.Vault.Models.Data;

public class CipherFido2KeyData : CipherData
{
    public CipherFido2KeyData() { }

    public string NonDiscoverableId { get; set; }
    public string KeyType { get; set; }
    public string KeyAlgorithm { get; set; }
    public string KeyCurve { get; set; }
    public string KeyValue { get; set; }
    public string RpId { get; set; }
    public string RpName { get; set; }
    public string UserHandle { get; set; }
    public string UserDisplayName { get; set; }
    public string Counter { get; set; }
}
