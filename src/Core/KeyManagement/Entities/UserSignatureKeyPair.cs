using Bit.Core.Entities;
using Bit.Core.KeyManagement.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Utilities;


namespace Bit.Core.KeyManagement.Entities;

public class UserSignatureKeyPair : ITableObject<Guid>, IRevisable
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public SignatureAlgorithm SignatureAlgorithm { get; set; }

    required public string VerifyingKey { get; set; }
    required public string SigningKey { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }

    public SignatureKeyPairData ToSignatureKeyPairData()
    {
        return new SignatureKeyPairData(SignatureAlgorithm, SigningKey, VerifyingKey);
    }
}
