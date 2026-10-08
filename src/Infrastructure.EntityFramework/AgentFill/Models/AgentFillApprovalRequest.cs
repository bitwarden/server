using Bit.Infrastructure.EntityFramework.Models;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.AgentFill.Models;

/// <summary>
/// EF model for <c>dbo.AgentFillApprovalRequest</c>. It lives here, rather than in the <c>Bit.AgentFill</c> library,
/// because EF migrations are generated from the shared <see cref="Repositories.DatabaseContext"/>.
/// The sealed fields are opaque to the server and must never be logged or inspected.
/// </summary>
public class AgentFillApprovalRequest
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid RequestDeviceId { get; set; }
    public string SealedRequest { get; set; } = null!;
    public string? SealedResponse { get; set; }
    public Guid? ResponseDeviceId { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public DateTime? ResponseDate { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual Device RequestDevice { get; set; } = null!;
}
