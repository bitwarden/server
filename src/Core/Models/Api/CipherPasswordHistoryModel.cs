using System;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Data;

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

        [StringLength(2000)]
        [Required]
        public string Password { get; set; }
        [Required]
        public DateTime? LastUsedDate { get; set; }
    }
}
