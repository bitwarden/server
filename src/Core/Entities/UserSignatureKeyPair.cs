using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.Entities;

public class UserSignatureKeyPair : ITableObject<Guid>, IRevisable
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public SignatureAlgorithm SignatureAlgorithm { get; set; }

    [MaxLength(500)]
    required public string VerifyingKey { get; set; }
    [MaxLength(500)]
    required public string SigningKey { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
