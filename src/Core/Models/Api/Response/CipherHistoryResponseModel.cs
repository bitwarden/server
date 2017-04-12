using System;
using System.Collections.Generic;

namespace Bit.Core.Models.Api
{
    [Obsolete]
    public class CipherHistoryResponseModel : ResponseModel
    {
        public CipherHistoryResponseModel()
            : base("cipherHistory")
        { }

        public IEnumerable<CipherResponseModel> Revised { get; set; } = new List<CipherResponseModel>();
        public IEnumerable<string> Deleted { get; set; } = new List<string>();
    }
}
