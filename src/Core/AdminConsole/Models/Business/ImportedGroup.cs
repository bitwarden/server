// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.Models.Business;

public class ImportedGroup
{
    public Group Group { get; set; }
    public HashSet<string> ExternalUserIds { get; set; }
}
