using Bit.Core.Models.Common;

namespace Bit.Core.Billing.Licenses;

public interface IValidateEntityAgainstLicenseCommandHandler
{
    Result Handle(ValidateEntityAgainstLicenseCommand command);
}
