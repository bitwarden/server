using System;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Newtonsoft.Json;

namespace Bit.Core.Models.Api
{
    public class SecureNoteDataModel : CipherDataModel
    {
        public SecureNoteDataModel() { }

        public SecureNoteDataModel(Cipher cipher)
        {
            if(cipher.Type != CipherType.SecureNote)
            {
                throw new ArgumentException("Cipher is not correct type.");
            }

            var data = JsonConvert.DeserializeObject<SecureNoteDataModel>(cipher.Data);

            Name = data.Name;
            Notes = data.Notes;
            Fields = data.Fields;

            Type = data.Type;
        }

        public SecureNoteType Type { get; set; }
    }
}
