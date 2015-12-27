using System;
using System.ComponentModel.DataAnnotations;
using Bit.Api.Utilities;
using Bit.Core.Domains;

namespace Bit.Api.Models
{
    public class SiteRequestModel
    {
        public string FolderId { get; set; }
        [Required]
        [EncryptedString]
        public string Name { get; set; }
        [Required]
        [EncryptedString]
        public string Uri { get; set; }
        [EncryptedString]
        public string Username { get; set; }
        [Required]
        [EncryptedString]
        public string Password { get; set; }
        [EncryptedString]
        public string Notes { get; set; }

        public Site ToSite(string userId = null)
        {
            return new Site
            {
                UserId = userId,
                FolderId = string.IsNullOrWhiteSpace(FolderId) ? null : FolderId,
                Name = Name,
                Uri = Uri,
                Username = string.IsNullOrWhiteSpace(Username) ? null : Username,
                Password = Password,
                Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes
            };
        }

        public Site ToSite(Site existingSite)
        {
            existingSite.FolderId = string.IsNullOrWhiteSpace(FolderId) ? null : FolderId;
            existingSite.Name = Name;
            existingSite.Uri = Uri;
            existingSite.Username = string.IsNullOrWhiteSpace(Username) ? null : Username;
            existingSite.Password = Password;
            existingSite.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes;

            return existingSite;
        }
    }
}
