using Bit.Core.Models.Api;
using Bit.Core.Vault.Entities;

namespace Bit.Api.Vault.Models.Response;

public class FolderResponseModel : ResponseModel
{
    public FolderResponseModel(Folder folder)
        : base("folder")
    {
        if (folder == null)
        {
            throw new ArgumentNullException(nameof(folder));
        }

        Id = folder.Id;
        Name = folder.Name;
        RevisionDate = folder.RevisionDate;
    }

    public Guid Id { get; set; }
    public string Name { get; set; }
    public DateTime RevisionDate { get; set; }
}
