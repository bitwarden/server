using Bit.Core.Models.Common;

namespace Bit.Core.Billing.Licenses;

public interface IValidateLicenseCommandHandler
{
    Result Handle(ValidateLicenseCommand command);
}
