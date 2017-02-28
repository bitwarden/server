using System;
using Bit.Core.Domains;

namespace Bit.Api.Models
{
    public class FolderResponseModel : ResponseModel
    {
        public FolderResponseModel(Cipher cipher, Guid userId)
            : base("folder")
        {
            if(cipher == null)
            {
                throw new ArgumentNullException(nameof(cipher));
            }

            if(cipher.Type != Core.Enums.CipherType.Folder)
            {
                throw new ArgumentException(nameof(cipher.Type));
            }

            var data = new FolderDataModel(cipher);

            Id = cipher.Id.ToString();
            Name = data.Name;
            RevisionDate = cipher.RevisionDate;
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime RevisionDate { get; set; }
    }
}
