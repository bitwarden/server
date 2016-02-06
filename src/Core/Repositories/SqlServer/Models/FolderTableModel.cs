using System;
using Bit.Core.Domains;

namespace Bit.Core.Repositories.SqlServer.Models
{
    public class FolderTableModel : ITableModel<Folder>
    {
        public FolderTableModel() { }

        public FolderTableModel(Folder folder)
        {
            Id = new Guid(folder.Id);
            UserId = new Guid(folder.UserId);
            Name = folder.Name;
            CreationDate = folder.CreationDate;
            RevisionDate = folder.RevisionDate;
        }

        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; }
        public DateTime CreationDate { get; set; }
        public DateTime RevisionDate { get; set; }

        public Folder ToDomain()
        {
            return new Folder
            {
                Id = Id.ToString(),
                UserId = UserId.ToString(),
                Name = Name,
                CreationDate = CreationDate,
                RevisionDate = RevisionDate
            };
        }
    }
}
