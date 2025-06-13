namespace Bit.Core.SecretsManager.Commands.Porting.Interfaces;

#nullable enable

public interface IImportCommand
{
    Task ImportAsync(Guid organizationId, SMImport import);
    SMImport AssignNewIds(SMImport import);
}
