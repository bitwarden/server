using Bit.Core.Enums;

namespace Bit.Core.Vault.Models.Data;

public class CipherLoginFido2KeyData
{
    public CipherLoginFido2KeyData() { }

    public string NonDiscoverableId { get; set; }
    public string KeyType { get; set; }
    public string KeyAlgorithm { get; set; }
    public string KeyCurve { get; set; }
    public string KeyValue { get; set; }
    public string RpId { get; set; }
    public string RpName { get; set; }
    public string UserHandle { get; set; }
    public string UserName { get; set; }
    public string Counter { get; set; }
}
