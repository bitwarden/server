using Bit.Core.Enums;
using Bit.Core.Models.Api;

namespace Bit.Core.Models.Data
{
    public class CipherSecureNoteData : CipherData
    {
        public CipherSecureNoteData() { }

        public CipherSecureNoteData(CipherRequestModel cipher)
            : base(cipher)
        {
            Type = cipher.SecureNote.Type;
        }

        public SecureNoteType Type { get; set; }
    }
}
