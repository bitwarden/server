using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.Porting.Interfaces;

public interface IImportCommand
{
    Task<SMImport> ImportAsync(Guid organizationId, SMImport import);
    void AssignNewIds(SMImport import);
}
