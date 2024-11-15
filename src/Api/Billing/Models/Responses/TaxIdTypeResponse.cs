using Bit.Core.Billing.Models;

namespace Bit.Api.Billing.Models.Responses;

public record TaxIdTypesResponse(List<TaxIdTypeResponse> TaxIdTypes)
{
    public static TaxIdTypesResponse From(IEnumerable<TaxIdType> taxIdTypes) => new(
        taxIdTypes.Select(TaxIdTypeResponse.From).ToList());
}


/// <param name="Code">Stripe short code for the tax ID type</param>
/// <param name="Country">ISO-3166-2 country code</param>
/// <param name="Description">Description of the tax ID type</param>
/// <param name="Example">Example of the tax ID type</param>
public record TaxIdTypeResponse(
    string Code,
    string Country,
    string Description,
    string Example)
{
    public static TaxIdTypeResponse From(TaxIdType taxIdType)
        => new(
            taxIdType.Code,
            taxIdType.Country,
            taxIdType.Description,
            taxIdType.Example);
}
