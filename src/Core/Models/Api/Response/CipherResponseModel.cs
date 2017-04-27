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

        [Obsolete]
        public CipherMiniResponseModel(Folder folder, string obj = "cipherMini")
            : base(obj)
        {
            if(folder == null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            Id = folder.Id.ToString();
            Type = Enums.CipherType.Folder;
            RevisionDate = folder.RevisionDate;
            Data = new FolderDataModel(folder);
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

        [Obsolete]
        public CipherResponseModel(Folder folder, string obj = "cipher")
            : base(folder, obj)
        { }

        public string FolderId { get; set; }
        public bool Favorite { get; set; }
    }

    public class CipherDetailsResponseModel : CipherResponseModel
    {
        public CipherDetailsResponseModel(CipherDetails cipher,
            IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphers, string obj = "cipherDetails")
            : base(cipher, obj)
        {
            if(collectionCiphers.ContainsKey(cipher.Id))
            {
                CollectionIds = collectionCiphers[cipher.Id].Select(c => c.CollectionId);
            }
            else
            {
                CollectionIds = new Guid[] { };
            }
        }

        public CipherDetailsResponseModel(CipherDetails cipher, IEnumerable<CollectionCipher> collectionCiphers,
            string obj = "cipherDetails")
            : base(cipher, obj)
        {
            CollectionIds = collectionCiphers.Select(c => c.CollectionId);
        }

        public IEnumerable<Guid> CollectionIds { get; set; }
    }

    public class CipherMiniDetailsResponseModel : CipherMiniResponseModel
    {
        public CipherMiniDetailsResponseModel(Cipher cipher,
            IDictionary<Guid, IGrouping<Guid, CollectionCipher>> collectionCiphers, string obj = "cipherMiniDetails")
            : base(cipher, obj)
        {
            if(collectionCiphers.ContainsKey(cipher.Id))
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

    public class CipherFullDetailsResponseModel : CipherDetailsResponseModel
    {
        public CipherFullDetailsResponseModel(CipherFullDetails cipher, IEnumerable<CollectionCipher> collectionCiphers)
            : base(cipher, collectionCiphers, "cipherFullDetails")
        {
            Edit = cipher.Edit;
        }

        public bool Edit { get; set; }
    }
}
