using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Bit.Api.Utilities;
using Bit.Core.Domains;
using Bit.Core.Enums;
using Newtonsoft.Json;

namespace Bit.Api.Models
{
    public class CipherRequestModel : IValidatableObject
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
        [StringLength(5000)]
        public string Uri { get; set; }
        [EncryptedString]
        [StringLength(200)]
        public string Username { get; set; }
        [EncryptedString]
        [StringLength(300)]
        public string Password { get; set; }
        [EncryptedString]
        [StringLength(5000)]
        public string Notes { get; set; }

        public virtual Cipher ToCipher(string userId = null)
        {
            return new Cipher
            {
                Id = new Guid(Id),
                UserId = new Guid(userId),
                FolderId = string.IsNullOrWhiteSpace(FolderId) ? null : (Guid?)new Guid(FolderId),
                Type = Type,
                Data = JsonConvert.SerializeObject(new CipherDataModel(this), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })
            };
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if(Type == CipherType.Site)
            {
                if(string.IsNullOrWhiteSpace(Uri))
                {
                    yield return new ValidationResult("Uri is required for a site cypher.", new[] { "Uri" });
                }
                if(string.IsNullOrWhiteSpace(Password))
                {
                    yield return new ValidationResult("Password is required for a site cypher.", new[] { "Password" });
                }
            }
        }
    }
}
