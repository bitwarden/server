using Bit.Core.Enums;

namespace Bit.Core.Models.Data;

public class CipherFido2KeyData : CipherData
{
    public CipherFido2KeyData() { }

    public string KeyType { get; set; }
    public string KeyCurve { get; set; }
    public string KeyValue { get; set; }
    public string RpId { get; set; }
    public string RpName { get; set; }
    public string UserHandle { get; set; }
    public string UserName { get; set; }
    public string Origin { get; set; }
}
