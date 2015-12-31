using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Bit.Api.Utilities;
using Bit.Core.Domains;
using System.Linq;
using Bit.Core.Enums;

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

        public virtual Site ToSite(string userId = null)
        {
            return new Site
            {
                Id = Id,
                UserId = userId,
                FolderId = string.IsNullOrWhiteSpace(FolderId) ? null : FolderId,
                Name = Name,
                Uri = Uri,
                Username = Username,
                Password = Password,
                Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes
            };
        }

        public Folder ToFolder(string userId = null)
        {
            return new Folder
            {
                Id = Id,
                UserId = userId,
                Name = Name
            };
        }

        public static IEnumerable<dynamic> ToDynamicCiphers(CipherRequestModel[] models, string userId)
        {
            var sites = models.Where(m => m.Type == CipherType.Site).Select(m => m.ToSite(userId)).ToList();
            var folders = models.Where(m => m.Type == CipherType.Folder).Select(m => m.ToFolder(userId)).ToList();

            var ciphers = new List<dynamic>();
            ciphers.AddRange(sites);
            ciphers.AddRange(folders);
            return ciphers;
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
