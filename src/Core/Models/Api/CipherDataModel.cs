using System.Collections.Generic;

namespace Bit.Core.Models.Api
{
    public abstract class CipherDataModel
    {
        public string Name { get; set; }
        public string Notes { get; set; }
        public IEnumerable<FieldDataModel> Fields { get; set; }
    }
}
