namespace Bit.Api.Auth.Models.Request;

public class BulkDenyAdminAuthRequestRequestModel
{
    public IEnumerable<Guid> Ids { get; set; }
}
