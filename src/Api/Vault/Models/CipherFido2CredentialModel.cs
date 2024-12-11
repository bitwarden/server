using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Vault.Models;

public class CipherFido2CredentialModel
{
    public CipherFido2CredentialModel() { }

    public CipherFido2CredentialModel(CipherLoginFido2CredentialData data)
    {
        CredentialId = data.CredentialId;
        KeyType = data.KeyType;
        KeyAlgorithm = data.KeyAlgorithm;
        KeyCurve = data.KeyCurve;
        KeyValue = data.KeyValue;
        RpId = data.RpId;
        RpName = data.RpName;
        UserHandle = data.UserHandle;
        UserName = data.UserName;
        UserDisplayName = data.UserDisplayName;
        Counter = data.Counter;
        Discoverable = data.Discoverable;
        CreationDate = data.CreationDate;
    }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string CredentialId { get; set; }

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
    public string UserDisplayName { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string Counter { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string Discoverable { get; set; }

    [Required]
    public DateTime CreationDate { get; set; }

    public CipherLoginFido2CredentialData ToCipherLoginFido2CredentialData()
    {
        return new CipherLoginFido2CredentialData
        {
            CredentialId = CredentialId,
            KeyType = KeyType,
            KeyAlgorithm = KeyAlgorithm,
            KeyCurve = KeyCurve,
            KeyValue = KeyValue,
            RpId = RpId,
            RpName = RpName,
            UserHandle = UserHandle,
            UserName = UserName,
            UserDisplayName = UserDisplayName,
            Counter = Counter,
            Discoverable = Discoverable,
            CreationDate = CreationDate,
        };
    }
}

static class CipherFido2CredentialModelExtensions
{
    public static CipherLoginFido2CredentialData[] ToCipherLoginFido2CredentialData(
        this CipherFido2CredentialModel[] models
    )
    {
        return models.Select(m => m.ToCipherLoginFido2CredentialData()).ToArray();
    }
}
