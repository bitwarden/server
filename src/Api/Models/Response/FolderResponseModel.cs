using System;
using Bit.Core.Domains;

namespace Bit.Api.Models
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

            Id = folder.Id;
            Name = folder.Name;
        }

        public string Id { get; set; }
        public string Name { get; set; }
    }
}
