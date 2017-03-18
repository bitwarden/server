using System;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api
{
    public class FolderResponseModel : ResponseModel
    {
        public FolderResponseModel(Folder folder)
            : base("folder")
        {
            if(folder == null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            Id = folder.Id.ToString();
            Name = folder.Name;
            RevisionDate = folder.RevisionDate;
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime RevisionDate { get; set; }
    }
}
