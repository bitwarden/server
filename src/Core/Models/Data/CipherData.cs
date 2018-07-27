using System.Collections.Generic;
using System.Linq;
using Bit.Core.Models.Api;

namespace Bit.Core.Models.Data
{
    public abstract class CipherData
    {
        public CipherData() { }

        public CipherData(CipherRequestModel cipher)
        {
            Name = cipher.Name;
            Notes = cipher.Notes;
            Fields = cipher.Fields?.Select(f => new CipherFieldData(f));
            PasswordHistory = cipher.PasswordHistory?.Select(ph => new CipherPasswordHistoryData(ph));
        }

        public string Name { get; set; }
        public string Notes { get; set; }
        public IEnumerable<CipherFieldData> Fields { get; set; }
        public IEnumerable<CipherPasswordHistoryData> PasswordHistory { get; set; }
    }
}
