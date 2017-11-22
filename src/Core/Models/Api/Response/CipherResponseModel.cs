using System;
using Core.Models.Data;
using System.Collections.Generic;
using Bit.Core.Models.Table;
using System.Linq;

namespace Bit.Core.Models.Api
{
    public class CipherMiniResponseModel : ResponseModel
    {
        public CipherMiniResponseModel(Cipher cipher, GlobalSettings globalSettings, bool orgUseTotp, string obj = "cipherMini")
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
            Attachments = AttachmentResponseModel.FromCipher(cipher, globalSettings);
            OrganizationUseTotp = orgUseTotp;

            switch(cipher.Type)
            {
                case Enums.CipherType.Login:
                    Data = new LoginDataModel(cipher);
                    break;
                case Enums.CipherType.SecureNote:
                    Data = new SecureNoteDataModel(cipher);
                    break;
                case Enums.CipherType.Card:
                    Data = new CardDataModel(cipher);
                    break;
                case Enums.CipherType.Identity:
                    Data = new IdentityDataModel(cipher);
                    break;
                default:
                    throw new ArgumentException("Unsupported " + nameof(Type) + ".");
            }
        }

        public string Id { get; set; }
        public string OrganizationId { get; set; }
        public Enums.CipherType Type { get; set; }
        public dynamic Data { get; set; }
        public IEnumerable<AttachmentResponseModel> Attachments { get; set; }
        public bool OrganizationUseTotp { get; set; }
        public DateTime RevisionDate { get; set; }
    }

    public class CipherResponseModel : CipherMiniResponseModel
    {
        public CipherResponseModel(CipherDetails cipher, GlobalSettings globalSettings, string obj = "cipher")
            : base(cipher, globalSettings, cipher.OrganizationUseTotp, obj)
        {
            FolderId = cipher.FolderId?.ToString();
            Favorite = cipher.Favorite;
            Edit = cipher.Edit;
        }

        public string FolderId { get; set; }
        public bool Favorite { get; set; }
        public bool Edit { get; set; }
    }

    public class CipherDetailsResponseModel : CipherResponseModel
    {
        public CipherDetailsResponseModel(CipherDetails cipher, GlobalSettings globalSettings,
            IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphers, string obj = "cipherDetails")
            : base(cipher, globalSettings, obj)
        {
            if(collectionCiphers?.ContainsKey(cipher.Id) ?? false)
            {
                CollectionIds = collectionCiphers[cipher.Id].Select(c => c.CollectionId);
            }
            else
            {
                CollectionIds = new Guid[] { };
            }
        }

        public CipherDetailsResponseModel(CipherDetails cipher, GlobalSettings globalSettings,
            IEnumerable<CollectionCipher> collectionCiphers, string obj = "cipherDetails")
            : base(cipher, globalSettings, obj)
        {
            CollectionIds = collectionCiphers?.Select(c => c.CollectionId) ?? new List<Guid>();
        }

        public IEnumerable<Guid> CollectionIds { get; set; }
    }

    public class CipherMiniDetailsResponseModel : CipherMiniResponseModel
    {
        public CipherMiniDetailsResponseModel(Cipher cipher, GlobalSettings globalSettings,
            IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphers, string obj = "cipherMiniDetails")
            : base(cipher, globalSettings, false, obj)
        {
            if(collectionCiphers?.ContainsKey(cipher.Id) ?? false)
            {
                CollectionIds = collectionCiphers[cipher.Id].Select(c => c.CollectionId);
            }
            else
            {
                CollectionIds = new Guid[] { };
            }
        }

        public IEnumerable<Guid> CollectionIds { get; set; }
    }
}
