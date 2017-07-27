using System;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;
using Newtonsoft.Json;
using Core.Models.Data;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api
{
    public class LoginRequestModel
    {
        [StringLength(36)]
        public string OrganizationId { get; set; }
        [StringLength(36)]
        public string FolderId { get; set; }
        public bool Favorite { get; set; }
        [Required]
        [EncryptedString]
        [StringLength(1000)]
        public string Name { get; set; }
        [EncryptedString]
        [StringLength(10000)]
        public string Uri { get; set; }
        [EncryptedString]
        [StringLength(1000)]
        public string Username { get; set; }
        [EncryptedString]
        [StringLength(1000)]
        public string Password { get; set; }
        [EncryptedString]
        [StringLength(10000)]
        public string Notes { get; set; }
        [EncryptedString]
        [StringLength(1000)]
        public string Totp { get; set; }

        public CipherDetails ToCipherDetails(Guid userId)
        {
            return ToCipherDetails(new CipherDetails
            {
                UserId = string.IsNullOrWhiteSpace(OrganizationId) ? (Guid?)userId : null,
                OrganizationId = string.IsNullOrWhiteSpace(OrganizationId) ? (Guid?)null : new Guid(OrganizationId),
                Edit = true
            });
        }

        public Cipher ToOrganizationCipher()
        {
            if(string.IsNullOrWhiteSpace(OrganizationId))
            {
                throw new ArgumentNullException(nameof(OrganizationId));
            }

            return ToCipher(new Cipher
            {
                OrganizationId = new Guid(OrganizationId)
            });
        }

        public CipherDetails ToCipherDetails(CipherDetails existingLogin)
        {
            existingLogin.FolderId = string.IsNullOrWhiteSpace(FolderId) ? null : (Guid?)new Guid(FolderId);
            existingLogin.Favorite = Favorite;

            existingLogin.Data = JsonConvert.SerializeObject(new LoginDataModel(this),
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            existingLogin.Type = Enums.CipherType.Login;

            return existingLogin;
        }

        public Cipher ToCipher(Cipher existingLogin)
        {
            existingLogin.Data = JsonConvert.SerializeObject(new LoginDataModel(this),
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            existingLogin.Type = Enums.CipherType.Login;

            return existingLogin;
        }
    }

    public class LoginWithIdRequestModel : LoginRequestModel
    {
        public Guid Id { get; set; }

        public Cipher ToCipher(Guid userId)
        {
            return ToCipherDetails(new CipherDetails
            {
                UserId = userId,
                Id = Id
            });
        }
    }
}
