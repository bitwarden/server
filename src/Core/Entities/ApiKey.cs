#nullable enable
using Bit.Core.Utilities;

namespace Bit.Core.Entities;

public class ApiKey: ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? ServiceAccountId { get; set; }
    public string ClientSecret { get; set; }
    public string Scope { get; set; }
    public string EncryptedPaylog { get; set; }
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
