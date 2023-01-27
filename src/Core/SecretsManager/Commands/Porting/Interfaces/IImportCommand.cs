namespace Bit.Core.SecretsManager.Commands.Porting.Interfaces;

public interface IImportCommand
{
    Task<SMImport> ImportAsync(Guid organizationId, SMImport import);
    SMImport AssignNewIds(SMImport import);
}
