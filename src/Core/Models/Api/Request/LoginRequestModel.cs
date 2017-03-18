using System;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;
using Bit.Core.Models.Table;
using Newtonsoft.Json;
using Core.Models.Data;

namespace Bit.Core.Models.Api
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

        public CipherDetails ToCipher(Guid userId)
        {
            return ToCipher(new CipherDetails
            {
                UserId = userId
            });
        }

        public CipherDetails ToCipher(CipherDetails existingLogin)
        {
            existingLogin.FolderId = string.IsNullOrWhiteSpace(FolderId) ? null : (Guid?)new Guid(FolderId);
            existingLogin.Favorite = Favorite;

            existingLogin.Data = JsonConvert.SerializeObject(new LoginDataModel(this),
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            existingLogin.Type = Enums.CipherType.Login;

            return existingLogin;
        }
    }
}
