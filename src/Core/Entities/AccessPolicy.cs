using Bit.Core.Utilities;

namespace Bit.Core.Entities;

public class AccessPolicy : ITableObject<Guid>
{
    public Guid Id { get; set; }

    // Object to grant access from
    public Guid? OrganizationUserId { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? ServiceAccountId { get; set; }

    // Object to grant access to
    public Guid? ProjectId { get; set; }
    public Guid? SecretId { get; set; }

    // Access
    public bool Read { get; set; }
    public bool Write { get; set; }
    public bool Delete { get; set; }

    public DateTime CreationDate { get; set; }
    public DateTime RevisionDate { get; set; }

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
