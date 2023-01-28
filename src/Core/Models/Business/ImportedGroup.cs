using Bit.Core.Entities;

namespace Bit.Core.Models.Business;

public class ImportedGroup
{
    public Group Group { get; set; }
    public HashSet<string> ExternalUserIds { get; set; }
}
