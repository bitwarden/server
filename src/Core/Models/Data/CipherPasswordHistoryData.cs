using System;
using Bit.Core.Models.Api;

namespace Bit.Core.Models.Data
{
    public class CipherPasswordHistoryData
    {
        public CipherPasswordHistoryData() { }

        public CipherPasswordHistoryData(CipherPasswordHistoryModel phModel)
        {
            Password = phModel.Password;
            LastUsedDate = phModel.LastUsedDate.Value;
        }
        
        public string Password { get; set; }
        public DateTime LastUsedDate { get; set; }
    }
}
