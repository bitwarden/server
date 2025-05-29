using Bit.Core.Billing.Tax.Models;

namespace Bit.Core.Billing.Tax.Services;

/// <summary>
/// Responsible for defining the correct automatic tax strategy for either personal use of business use.
/// </summary>
public interface IAutomaticTaxFactory
{
    Task<IAutomaticTaxStrategy> CreateAsync(AutomaticTaxFactoryParameters parameters);
}
