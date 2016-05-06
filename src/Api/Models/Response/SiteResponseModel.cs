using System;
using Bit.Core.Domains;

namespace Bit.Api.Models
{
    public class SiteResponseModel : ResponseModel
    {
        public SiteResponseModel(Site site)
            : base("site")
        {
            if(site == null)
            {
                throw new ArgumentNullException(nameof(site));
            }

            Id = site.Id;
            FolderId = string.IsNullOrWhiteSpace(site.FolderId) ? null : site.FolderId;
            Name = site.Name;
            Uri = site.Uri;
            Username = site.Username;
            Password = site.Password;
            Notes = site.Notes;
            RevisionDate = site.RevisionDate;
        }

        public string Id { get; set; }
        public string FolderId { get; set; }
        public string Name { get; set; }
        public string Uri { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Notes { get; set; }
        public DateTime RevisionDate { get; set; }

        // Expandables
        public FolderResponseModel Folder { get; set; }
    }
}
