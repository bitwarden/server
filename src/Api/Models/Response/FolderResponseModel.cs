using System;
using Bit.Core.Domains;
using Newtonsoft.Json;

namespace Bit.Api.Models
{
    public class FolderResponseModel : ResponseModel
    {
        public FolderResponseModel(Cipher cipher)
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

            var data = JsonConvert.DeserializeObject<CipherDataModel>(cipher.Data);

            Id = cipher.Id.ToString();
            Name = data.Name;
            RevisionDate = cipher.RevisionDate;
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime RevisionDate { get; set; }
    }
}
