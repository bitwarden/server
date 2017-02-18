using System;
using Bit.Core.Domains;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

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

            if(!string.IsNullOrWhiteSpace(cipher.Shares))
            {
                var shares = JsonConvert.DeserializeObject<IEnumerable<Cipher.Share>>(cipher.Shares);
                var userShare = shares.FirstOrDefault(s => s.UserId == userId);
                if(userShare != null)
                {
                    Key = userShare.Key;
                }
            }
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string Key { get; set; }
        public DateTime RevisionDate { get; set; }
    }
}
