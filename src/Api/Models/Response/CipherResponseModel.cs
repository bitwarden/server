using System;
using Bit.Core.Domains;

namespace Bit.Api.Models
{
    public class CipherResponseModel : ResponseModel
    {
        public CipherResponseModel(Cipher cipher)
            : base("cipher")
        {
            if(cipher == null)
            {
                throw new ArgumentNullException(nameof(cipher));
            }

            Id = cipher.Id.ToString();
            FolderId = cipher.FolderId?.ToString();
            Type = cipher.Type;
            Data = cipher.Data;
            RevisionDate = cipher.RevisionDate;
        }

        public string Id { get; set; }
        public string FolderId { get; set; }
        public Core.Enums.CipherType Type { get; set; }
        public string Data { get; set; }
        public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;
    }
}
