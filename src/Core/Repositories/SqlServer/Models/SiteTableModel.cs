using System;
using Bit.Core.Domains;

namespace Bit.Core.Repositories.SqlServer.Models
{
    public class SiteTableModel : ITableModel<Site>
    {
        public SiteTableModel() { }

        public SiteTableModel(Site site)
        {
            Id = new Guid(site.Id);
            UserId = new Guid(site.UserId);
            FolderId = string.IsNullOrWhiteSpace(site.FolderId) ? (Guid?)null : new Guid(site.FolderId);
            Name = site.Name;
            Uri = site.Uri;
            Username = site.Username;
            Password = site.Password;
            Notes = site.Notes;
            CreationDate = site.CreationDate;
            RevisionDate = site.RevisionDate;
        }

        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid? FolderId { get; set; }
        public string Name { get; set; }
        public string Uri { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Notes { get; set; }
        public DateTime CreationDate { get; set; }
        public DateTime RevisionDate { get; set; }

        public Site ToDomain()
        {
            return new Site
            {
                Id = Id.ToString(),
                UserId = UserId.ToString(),
                FolderId = FolderId.ToString(),
                Name = Name,
                Uri = Uri,
                Username = Username,
                Password = Password,
                Notes = Notes,
                CreationDate = CreationDate,
                RevisionDate = RevisionDate
            };
        }
    }
}
