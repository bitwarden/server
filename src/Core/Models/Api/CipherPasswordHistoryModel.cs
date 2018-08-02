using System;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Api
{
    public class CipherPasswordHistoryModel
    {
        public CipherPasswordHistoryModel() { }

        public CipherPasswordHistoryModel(CipherPasswordHistoryData data)
        {
            Password = data.Password;
            LastUsedDate = data.LastUsedDate;
        }

        [EncryptedString]
        [EncryptedStringLength(2000)]
        [Required]
        public string Password { get; set; }
        [Required]
        public DateTime? LastUsedDate { get; set; }
    }
}
