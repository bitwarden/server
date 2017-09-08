using System.Collections.Generic;

namespace Bit.Function.Models
{
    public class ListResult
    {
        public bool Success { get; set; }
        public List<AccessRuleResultResponse> Result { get; set; }
    }
}
