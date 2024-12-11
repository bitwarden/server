using Bit.Core.Billing.Models;

namespace Bit.Api.Billing.Models.Responses;

public record TaxInformationResponse(
    string Country,
    string PostalCode,
    string TaxId,
    string Line1,
    string Line2,
    string City,
    string State
)
{
    public static TaxInformationResponse From(TaxInformation taxInformation) =>
        new(
            taxInformation.Country,
            taxInformation.PostalCode,
            taxInformation.TaxId,
            taxInformation.Line1,
            taxInformation.Line2,
            taxInformation.City,
            taxInformation.State
        );
}
