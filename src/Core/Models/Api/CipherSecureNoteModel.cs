using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.Models.Api
{
    public class CipherSecureNoteModel
    {
        public CipherSecureNoteModel() { }

        public CipherSecureNoteModel(CipherSecureNoteData data)
        {
            Type = data.Type;
        }

        public SecureNoteType Type { get; set; }
    }
}
