using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api
{
    public class FolderDataModel
    {
        public FolderDataModel(Folder folder)
        {
            Name = folder.Name;
        }

        public string Name { get; set; }
    }
}
