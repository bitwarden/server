using Bit.Core.Enums;
using Bit.Core.Models.Business;

namespace Bit.Api.Billing.Models.Responses;

public record PaymentInformationResponse(PaymentMethodDTO PaymentInfo, TaxInformationDTO TaxInfo)
{
    public static PaymentInformationResponse From(BillingInfo.BillingSource billingSource, TaxInfo taxInfo)
    {
        var paymentMethodDto = new PaymentMethodDTO(
            billingSource.Type, billingSource.Description, billingSource.CardBrand
        );

        var taxInformationDto = new TaxInformationDTO(
            taxInfo.BillingAddressCountry, taxInfo.BillingAddressPostalCode, taxInfo.TaxIdNumber,
            taxInfo.BillingAddressLine1, taxInfo.BillingAddressLine2, taxInfo.BillingAddressCity,
            taxInfo.BillingAddressState
        );

        return new PaymentInformationResponse(paymentMethodDto, taxInformationDto);
    }

}

public record PaymentMethodDTO(
    PaymentMethodType Type,
    string Description,
    string CardBrand);

public record TaxInformationDTO(
    string Country,
    string PostalCode,
    string TaxId,
    string Line1,
    string Line2,
    string City,
    string State);

