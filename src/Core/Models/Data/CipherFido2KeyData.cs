using Bit.Core.Enums;

namespace Bit.Core.Models.Data;

public class CipherFido2KeyData : CipherData
{
    public CipherFido2KeyData() { }

    public string Key { get; set; }
    public string RpId { get; set; }
    public string Origin { get; set; }
    public string UserHandle { get; set; }
}
