#nullable enable
using Bit.Core.Entities;
using Bit.Core.Models.Common;

// ReSharper disable once CheckNamespace
namespace Bit.Core.Billing.Licenses;

public class ValidateLicenseTokenCommand
{
    public required ILicense License { get; init; }

    public User? User { get; init; }
}

public class ValidateLicenseTokenCommandHandler : IValidateLicenseTokenCommandHandler
{
    public Result Handle(ValidateLicenseTokenCommand command)
    {
        throw new NotImplementedException();
    }
}
