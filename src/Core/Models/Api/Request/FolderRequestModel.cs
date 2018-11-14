using System;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;
using Bit.Core.Models.Table;
using Newtonsoft.Json;

namespace Bit.Core.Models.Api
{
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
        public Guid Id { get; set; }
    }
}
