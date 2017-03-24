using System;
using Core.Models.Data;
using System.Collections.Generic;
using Bit.Core.Models.Table;
using System.Linq;

namespace Bit.Core.Models.Api
{
    public class CipherResponseModel : ResponseModel
    {
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
            OrganizationId = cipher.OrganizationId?.ToString();
            FolderId = cipher.FolderId?.ToString();
            Favorite = cipher.Favorite;

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
        public string OrganizationId { get; set; }
        public string FolderId { get; set; }
        public Enums.CipherType Type { get; set; }
        public bool Favorite { get; set; }
        public dynamic Data { get; set; }
        public DateTime RevisionDate { get; set; }
    }

    public class CipherDetailsResponseModel : CipherResponseModel
    {
        public CipherDetailsResponseModel(CipherDetails cipher,
            IDictionary<Guid, IGrouping<Guid, SubvaultCipher>> subvaultCiphers)
            : base(cipher, "cipherDetails")
        {
            if(subvaultCiphers.ContainsKey(cipher.Id))
            {
                SubvaultIds = subvaultCiphers[cipher.Id].Select(s => s.SubvaultId);
            }
            else
            {
                SubvaultIds = new Guid[] { };
            }
        }

        public IEnumerable<Guid> SubvaultIds { get; set; }
    }
}
