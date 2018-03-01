using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.Models.Api
{
    public class CipherSecureNoteModel
    {
        public SecureNoteType Type { get; set; }

        public CipherSecureNoteModel(CipherSecureNoteData data)
        {
            Type = data.Type;
        }
    }
}
