using Bit.Core.Billing.Services.Contracts;

namespace Bit.Core.Billing.Services;

/// <summary>
/// Responsible for defining the correct automatic tax strategy for either personal use of business use.
/// </summary>
public interface IAutomaticTaxFactory
{
    Task<IAutomaticTaxStrategy> CreateAsync(AutomaticTaxFactoryParameters parameters);
}
