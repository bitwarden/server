using Bit.Core.Billing.Models;

namespace Bit.Core.Billing.Services;

public interface ITaxService
{
    string? GetStripeTaxCode(string country, string taxId);

    IEnumerable<TaxIdType> GetTaxIdTypes();
}
