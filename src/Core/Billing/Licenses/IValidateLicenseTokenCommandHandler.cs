using Bit.Core.Models.Common;

namespace Bit.Core.Billing.Licenses;

public interface IValidateLicenseTokenCommandHandler
{
    Result Handle(ValidateLicenseTokenCommand command);
}
