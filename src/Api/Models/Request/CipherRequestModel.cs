using System;
using System.ComponentModel.DataAnnotations;
using Bit.Api.Utilities;
using Bit.Core.Domains;
using Bit.Core.Enums;
using Newtonsoft.Json;

namespace Bit.Api.Models
{
    public class CipherRequestModel
    {
        public CipherType Type { get; set; }

        [Required]
        [StringLength(36)]
        public string Id { get; set; }
        [StringLength(36)]
        public string FolderId { get; set; }
        [Required]
        [EncryptedString]
        [StringLength(300)]
        public string Name { get; set; }
        [EncryptedString]
        [StringLength(10000)]
        public string Uri { get; set; }
        [EncryptedString]
        [StringLength(300)]
        public string Username { get; set; }
        [EncryptedString]
        [StringLength(300)]
        public string Password { get; set; }
        [EncryptedString]
        [StringLength(10000)]
        public string Notes { get; set; }

        public virtual Cipher ToCipher(string userId = null)
        {
            var cipher = new Cipher
            {
                Id = new Guid(Id),
                UserId = new Guid(userId),
                FolderId = string.IsNullOrWhiteSpace(FolderId) ? null : (Guid?)new Guid(FolderId),
                Type = Type
            };

            switch(Type)
            {
                case CipherType.Folder:
                    cipher.Data = JsonConvert.SerializeObject(new FolderDataModel(this), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                    break;
                case CipherType.Login:
                    cipher.Data = JsonConvert.SerializeObject(new LoginDataModel(this), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                    break;
                default:
                    throw new ArgumentException("Unsupported " + nameof(Type) + ".");
            }

            return cipher;
        }
    }
}
