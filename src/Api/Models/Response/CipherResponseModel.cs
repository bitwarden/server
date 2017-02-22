using System;
using Bit.Core.Domains;
using Bit.Core.Models.Data;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bit.Api.Models
{
    public class CipherResponseModel : ResponseModel
    {
        public CipherResponseModel(Cipher cipher, Guid userId, string obj = "cipher")
            : base(obj)
        {
            if(cipher == null)
            {
                throw new ArgumentNullException(nameof(cipher));
            }

            Id = cipher.Id.ToString();
            FolderId = cipher.FolderId?.ToString();
            Type = cipher.Type;
            Favorite = cipher.Favorite;
            RevisionDate = cipher.RevisionDate;

            switch(cipher.Type)
            {
                case Core.Enums.CipherType.Folder:
                    Data = new FolderDataModel(cipher);
                    break;
                case Core.Enums.CipherType.Login:
                    Data = new LoginDataModel(cipher);
                    break;
                default:
                    throw new ArgumentException("Unsupported " + nameof(Type) + ".");
            }
        }

        public string Id { get; set; }
        public string FolderId { get; set; }
        public Core.Enums.CipherType Type { get; set; }
        public bool Favorite { get; set; }
        public dynamic Data { get; set; }
        public DateTime RevisionDate { get; set; }
    }

    public class CipherShareResponseModel : CipherResponseModel
    {
        public CipherShareResponseModel(CipherShare cipherShare, Guid userId)
            : base(cipherShare, userId, "cipherShare")
        {
            Key = cipherShare.Key;
            Permissions = cipherShare.Permissions == null ? null :
                JsonConvert.DeserializeObject<IEnumerable<Core.Enums.SharePermissionType>>(cipherShare.Permissions);
            Status = cipherShare.Status;
        }

        public string Key { get; set; }
        public IEnumerable<Core.Enums.SharePermissionType> Permissions { get; set; }
        public Core.Enums.ShareStatusType? Status { get; set; }
    }
}
