using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Models;

public class CipherSecureNoteModel
{
    public CipherSecureNoteModel() { }

    public CipherSecureNoteModel(CipherSecureNoteData data)
    {
        Type = data.Type;
    }

    public SecureNoteType Type { get; set; }
}
