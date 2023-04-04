using System.ComponentModel.DataAnnotations;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.Vault.Models;

public class CipherFido2KeyModel
{
    public CipherFido2KeyModel() { }

    public CipherFido2KeyModel(CipherFido2KeyData data)
    {
        NonDiscoverableId = data.NonDiscoverableId;
        KeyType = data.KeyType;
        KeyAlgorithm = data.KeyAlgorithm;
        KeyCurve = data.KeyCurve;
        KeyValue = data.KeyValue;
        RpId = data.RpId;
        RpName = data.RpName;
        UserHandle = data.UserHandle;
        UserName = data.UserName;
        Counter = data.Counter;
    }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string NonDiscoverableId { get; set; }
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string KeyType { get; set; }
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string KeyAlgorithm { get; set; }
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
    public string Counter { get; set; }
}
