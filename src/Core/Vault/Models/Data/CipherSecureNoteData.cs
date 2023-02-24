using Bit.Core.Enums;

namespace Bit.Core.Models.Data;

public class CipherSecureNoteData : CipherData
{
    public CipherSecureNoteData() { }

    public SecureNoteType Type { get; set; }
}
