using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;
using static Fido2NetLib.Objects.COSE;

namespace Bit.Api.Models;

public class CipherFido2KeyModel
{
    public CipherFido2KeyModel() { }

    public CipherFido2KeyModel(CipherFido2KeyData data)
    {
        KeyType = data.KeyType;
        KeyCurve = data.KeyCurve;
        KeyValue = data.KeyValue;
        RpId = data.RpId;
        RpName = data.RpName;
        UserHandle = data.UserHandle;
        UserName = data.UserName;
        Origin = data.Origin;
    }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string KeyType { get; set; }
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string KeyCurve { get; set; }
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string KeyValue { get; set; }
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string RpId { get; set; }
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string RpName { get; set; }
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string UserHandle { get; set; }
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string UserName { get; set; }
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string Origin { get; set; }
}
