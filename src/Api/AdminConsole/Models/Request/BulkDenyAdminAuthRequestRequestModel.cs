namespace Bit.Api.AdminConsole.Models.Request;

public class BulkDenyAdminAuthRequestRequestModel
{
    public IEnumerable<Guid> Ids { get; set; }
}
