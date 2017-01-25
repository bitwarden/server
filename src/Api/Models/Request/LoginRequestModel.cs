using System;
using System.ComponentModel.DataAnnotations;
using Bit.Api.Utilities;
using Bit.Core.Domains;
using Newtonsoft.Json;

namespace Bit.Api.Models
{
    public class LoginRequestModel
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

        public Cipher ToCipher(Guid userId)
        {
            return ToCipher(new Cipher
            {
                UserId = userId
            });
        }

        public Cipher ToCipher(Cipher existingLogin)
        {
            existingLogin.FolderId = string.IsNullOrWhiteSpace(FolderId) ? null : (Guid?)new Guid(FolderId);
            existingLogin.Favorite = Favorite;
            existingLogin.Data = JsonConvert.SerializeObject(new LoginDataModel(this),
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            existingLogin.Type = Core.Enums.CipherType.Login;

            return existingLogin;
        }
    }
}
