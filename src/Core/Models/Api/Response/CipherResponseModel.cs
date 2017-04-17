using System;
using Core.Models.Data;
using System.Collections.Generic;
using Bit.Core.Models.Table;
using System.Linq;

namespace Bit.Core.Models.Api
{
    public class CipherMiniResponseModel : ResponseModel
    {
        public CipherMiniResponseModel(Cipher cipher, string obj = "cipherMini")
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
        public Enums.CipherType Type { get; set; }
        public dynamic Data { get; set; }
        public DateTime RevisionDate { get; set; }
    }

    public class CipherResponseModel : CipherMiniResponseModel
    {
        public CipherResponseModel(CipherDetails cipher, string obj = "cipher")
            : base(cipher, obj)
        {
            FolderId = cipher.FolderId?.ToString();
            Favorite = cipher.Favorite;
        }

        public string FolderId { get; set; }
        public bool Favorite { get; set; }
    }

    public class CipherDetailsResponseModel : CipherResponseModel
    {
        public CipherDetailsResponseModel(CipherDetails cipher,
            IDictionary<Guid, IGrouping<Guid, SubvaultCipher>> subvaultCiphers, string obj = "cipherDetails")
            : base(cipher, obj)
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

        public CipherDetailsResponseModel(CipherDetails cipher, IEnumerable<SubvaultCipher> subvaultCiphers,
            string obj = "cipherDetails")
            : base(cipher, obj)
        {
            SubvaultIds = subvaultCiphers.Select(s => s.SubvaultId);
        }

        public IEnumerable<Guid> SubvaultIds { get; set; }
    }

    public class CipherMiniDetailsResponseModel : CipherMiniResponseModel
    {
        public CipherMiniDetailsResponseModel(Cipher cipher,
            IDictionary<Guid, IGrouping<Guid, SubvaultCipher>> subvaultCiphers, string obj = "cipherMiniDetails")
            : base(cipher, obj)
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

    public class CipherFullDetailsResponseModel : CipherDetailsResponseModel
    {
        public CipherFullDetailsResponseModel(CipherFullDetails cipher, IEnumerable<SubvaultCipher> subvaultCiphers)
            : base(cipher, subvaultCiphers, "cipherFullDetails")
        {
            Edit = cipher.Edit;
        }

        public bool Edit { get; set; }
    }
}
