using Bit.Core.Entities;

namespace Bit.Core.SecretManagerFeatures.Porting.Interfaces;

public interface IImportCommand
{
    Task<SMImport> ImportAsync(Guid organizationId, SMImport import);
    void AssignNewIds(SMImport import);
}
