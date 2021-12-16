using System.Collections.Generic;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Business
{
    public class ImportedGroup
    {
        public Group Group { get; set; }
        public HashSet<string> ExternalUserIds { get; set; }
    }
}
