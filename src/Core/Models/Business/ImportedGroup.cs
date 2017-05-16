using Bit.Core.Models.Table;
using System.Collections.Generic;

namespace Bit.Core.Models.Business
{
    public class ImportedGroup
    {
        public Group Group { get; set; }
        public HashSet<string> ExternalUserIds { get; set; }
    }
}
