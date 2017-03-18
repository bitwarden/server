using System;
using Bit.Core.Models.Table;
using Core.Models.Data;

namespace Bit.Core.Models.Api
{
    public class CipherResponseModel : ResponseModel
    {
        public CipherResponseModel(Cipher cipher, string obj = "cipher")
            : base(obj)
        {
            if(cipher == null)
            {
                throw new ArgumentNullException(nameof(cipher));
            }

            Id = cipher.Id.ToString();
            Type = cipher.Type;
            RevisionDate = cipher.RevisionDate;

            switch(cipher.Type)
            {
                case Enums.CipherType.Login:
                    Data = new LoginDataModel(cipher);
                    break;
                default:
                    throw new ArgumentException("Unsupported " + nameof(Type) + ".");
            }
        }
        public CipherResponseModel(CipherDetails cipher, string obj = "cipher")
            : base(obj)
        {
            if(cipher == null)
            {
                throw new ArgumentNullException(nameof(cipher));
            }

            Id = cipher.Id.ToString();
            Type = cipher.Type;
            RevisionDate = cipher.RevisionDate;

            switch(cipher.Type)
            {
                case Enums.CipherType.Login:
                    Data = new LoginDataModel(cipher);
                    break;
                default:
                    throw new ArgumentException("Unsupported " + nameof(Type) + ".");
            }
        }

        public string Id { get; set; }
        public string FolderId { get; set; }
        public Enums.CipherType Type { get; set; }
        public bool Favorite { get; set; }
        public dynamic Data { get; set; }
        public DateTime RevisionDate { get; set; }
    }
}
