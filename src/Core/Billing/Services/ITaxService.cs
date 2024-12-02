namespace Bit.Core.Billing.Services;

public interface ITaxService
{
    /// <summary>
    /// Retrieves the Stripe tax code for a given country and tax ID.
    /// </summary>
    /// <param name="country"></param>
    /// <param name="taxId"></param>
    /// <returns>
    /// Returns the Stripe tax code if the tax ID is valid for the country.
    /// Returns null if the tax ID is invalid or the country is not supported.
    /// </returns>
    string GetStripeTaxCode(string country, string taxId);

    /// <summary>
    /// Returns true or false whether charging or storing tax is supported for the given country.
    /// </summary>
    /// <param name="country"></param>
    /// <returns></returns>
    bool IsSupported(string country);
}
