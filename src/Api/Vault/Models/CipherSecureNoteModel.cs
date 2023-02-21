using Bit.Core.Enums;
using Bit.Core.Models.Data;

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
