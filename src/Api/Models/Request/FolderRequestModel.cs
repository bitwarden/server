using System;
using System.ComponentModel.DataAnnotations;
using Bit.Api.Utilities;
using Bit.Core.Domains;
using Newtonsoft.Json;

namespace Bit.Api.Models
{
    public class FolderRequestModel
    {
        [Required]
        [EncryptedString]
        [StringLength(300)]
        public string Name { get; set; }

        public Cipher ToCipher(Guid userId)
        {
            return ToCipher(new Cipher
            {
                UserId = userId
            });
        }

        public Cipher ToCipher(Cipher existingFolder)
        {
            existingFolder.Data = JsonConvert.SerializeObject(new FolderDataModel(this), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            existingFolder.Type = Core.Enums.CipherType.Folder;

            return existingFolder;
        }
    }
}
