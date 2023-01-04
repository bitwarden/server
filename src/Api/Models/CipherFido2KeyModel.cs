using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.Models;

public class CipherFido2KeyModel
{
    public CipherFido2KeyModel() { }

    public CipherFido2KeyModel(CipherFido2KeyData data)
    {
        Key = data.Key;
        RpId = data.RpId;
        Origin = data.Origin;
        UserHandle = data.UserHandle;
    }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string Key { get; set; }
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string RpId { get; set; }
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string Origin { get; set; }
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string UserHandle { get; set; }
}
