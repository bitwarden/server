using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Request;

public class FolderRequestModel
{
    [Required]
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string Name { get; set; }

    public Folder ToFolder(Guid userId)
    {
        return ToFolder(new Folder
        {
            UserId = userId
        });
    }

    public Folder ToFolder(Folder existingFolder)
    {
        existingFolder.Name = Name;
        return existingFolder;
    }
}

public class FolderWithIdRequestModel : FolderRequestModel
{
    public Guid? Id { get; set; }

    public new Folder ToFolder(Guid userId)
    {
        return ToFolder(new Folder
        {
            UserId = userId
        });
    }

    public new Folder ToFolder(Folder existingFolder)
    {
        existingFolder.Name = Name;
        if (Id != null && Id != Guid.Empty)
        {
            existingFolder.Id = Id ?? Guid.Empty;
        }
        return existingFolder;
    }
}
