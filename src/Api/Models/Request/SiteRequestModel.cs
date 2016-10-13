using System;
using System.ComponentModel.DataAnnotations;
using Bit.Api.Utilities;
using Bit.Core.Domains;
using Newtonsoft.Json;

namespace Bit.Api.Models
{
    public class SiteRequestModel
    {
        [StringLength(36)]
        public string FolderId { get; set; }
        public bool Favorite { get; set; }
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

        public Cipher ToCipher(string userId = null)
        {
            return ToCipher(new Cipher
            {
                UserId = new Guid(userId)
            });
        }

        public Cipher ToCipher(Cipher existingSite)
        {
            existingSite.FolderId = string.IsNullOrWhiteSpace(FolderId) ? null : (Guid?)new Guid(FolderId);
            existingSite.Favorite = Favorite;
            existingSite.Data = JsonConvert.SerializeObject(new SiteDataModel(this), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            existingSite.Type = Core.Enums.CipherType.Site;

            return existingSite;
        }
    }
}
