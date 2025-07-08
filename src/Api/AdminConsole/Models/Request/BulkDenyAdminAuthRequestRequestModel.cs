// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Api.AdminConsole.Models.Request;

public class BulkDenyAdminAuthRequestRequestModel
{
    public IEnumerable<Guid> Ids { get; set; }
}
