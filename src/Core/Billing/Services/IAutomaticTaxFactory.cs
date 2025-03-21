using Bit.Core.Billing.Services.Contracts;

namespace Bit.Core.Billing.Services;

public interface IAutomaticTaxFactory
{
    Task<IAutomaticTaxStrategy> CreateAsync(AutomaticTaxFactoryParameters parameters);
}
