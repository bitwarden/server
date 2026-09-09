using Bit.Core.AdminConsole.Utilities.v2.Validation;

namespace Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery;

public interface IAdminRecoverAccountValidator
{
    Task<ValidationResult<RecoverAccountRequest>> ValidateAsync(RecoverAccountRequest request);
}
