using Bit.Core.Models.Common;

namespace Bit.Core.Billing.Licenses.ValidateLicense;

public interface IValidateLicenseCommandHandler
{
    Result Handle(ValidateLicenseCommand command);
}
