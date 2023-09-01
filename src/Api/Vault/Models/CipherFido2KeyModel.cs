using Bit.Core.Utilities;
using Bit.Core.Vault.Models.Data;

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
        UserDisplayName = data.UserDisplayName;
        Counter = data.Counter;
    }

    public CipherFido2KeyModel(CipherLoginFido2KeyData data)
    {
        NonDiscoverableId = data.NonDiscoverableId;
        KeyType = data.KeyType;
        KeyAlgorithm = data.KeyAlgorithm;
        KeyCurve = data.KeyCurve;
        KeyValue = data.KeyValue;
        RpId = data.RpId;
        RpName = data.RpName;
        UserHandle = data.UserHandle;
        UserDisplayName = data.UserDisplayName;
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
    public string UserDisplayName { get; set; }
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string Counter { get; set; }

    public CipherLoginFido2KeyData ToCipherLoginFido2KeyData()
    {
        return new CipherLoginFido2KeyData
        {
            NonDiscoverableId = NonDiscoverableId,
            KeyType = KeyType,
            KeyAlgorithm = KeyAlgorithm,
            KeyCurve = KeyCurve,
            KeyValue = KeyValue,
            RpId = RpId,
            RpName = RpName,
            UserHandle = UserHandle,
            UserDisplayName = UserDisplayName,
            Counter = Counter,
        };
    }
}
