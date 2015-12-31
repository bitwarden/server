using System;
using System.ComponentModel.DataAnnotations;
using Bit.Api.Utilities;
using Bit.Core.Domains;

namespace Bit.Api.Models
{
    public class FolderRequestModel
    {
        [Required]
        [EncryptedString]
        [StringLength(300)]
        public string Name { get; set; }

        public Folder ToFolder(string userId = null)
        {
            return new Folder
            {
                UserId = userId,
                Name = Name
            };
        }

        public Folder ToFolder(Folder existingFolder)
        {
            existingFolder.Name = Name;

            return existingFolder;
        }
    }
}
