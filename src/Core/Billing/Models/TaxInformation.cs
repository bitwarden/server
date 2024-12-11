using Bit.Core.Models.Business;
using Stripe;

namespace Bit.Core.Billing.Models;

public record TaxInformation(
    string Country,
    string PostalCode,
    string TaxId,
    string Line1,
    string Line2,
    string City,
    string State
)
{
    public static TaxInformation From(TaxInfo taxInfo) =>
        new(
            taxInfo.BillingAddressCountry,
            taxInfo.BillingAddressPostalCode,
            taxInfo.TaxIdNumber,
            taxInfo.BillingAddressLine1,
            taxInfo.BillingAddressLine2,
            taxInfo.BillingAddressCity,
            taxInfo.BillingAddressState
        );

    public (AddressOptions, List<CustomerTaxIdDataOptions>) GetStripeOptions()
    {
        var address = new AddressOptions
        {
            Country = Country,
            PostalCode = PostalCode,
            Line1 = Line1,
            Line2 = Line2,
            City = City,
            State = State,
        };

        var customerTaxIdDataOptionsList = !string.IsNullOrEmpty(TaxId)
            ? new List<CustomerTaxIdDataOptions>
            {
                new() { Type = GetTaxIdType(), Value = TaxId },
            }
            : null;

        return (address, customerTaxIdDataOptionsList);
    }

    public string GetTaxIdType()
    {
        if (string.IsNullOrEmpty(Country) || string.IsNullOrEmpty(TaxId))
        {
            return null;
        }

        switch (Country.ToUpper())
        {
            case "AD":
                return "ad_nrt";
            case "AE":
                return "ae_trn";
            case "AR":
                return "ar_cuit";
            case "AU":
                return "au_abn";
            case "BO":
                return "bo_tin";
            case "BR":
                return "br_cnpj";
            case "CA":
                // May break for those in Québec given the assumption of QST
                if (State?.Contains("bec") ?? false)
                {
                    return "ca_qst";
                }
                return "ca_bn";
            case "CH":
                return "ch_vat";
            case "CL":
                return "cl_tin";
            case "CN":
                return "cn_tin";
            case "CO":
                return "co_nit";
            case "CR":
                return "cr_tin";
            case "DO":
                return "do_rcn";
            case "EC":
                return "ec_ruc";
            case "EG":
                return "eg_tin";
            case "GE":
                return "ge_vat";
            case "ID":
                return "id_npwp";
            case "IL":
                return "il_vat";
            case "IS":
                return "is_vat";
            case "KE":
                return "ke_pin";
            case "AT":
            case "BE":
            case "BG":
            case "CY":
            case "CZ":
            case "DE":
            case "DK":
            case "EE":
            case "ES":
            case "FI":
            case "FR":
            case "GB":
            case "GR":
            case "HR":
            case "HU":
            case "IE":
            case "IT":
            case "LT":
            case "LU":
            case "LV":
            case "MT":
            case "NL":
            case "PL":
            case "PT":
            case "RO":
            case "SE":
            case "SI":
            case "SK":
                return "eu_vat";
            case "HK":
                return "hk_br";
            case "IN":
                return "in_gst";
            case "JP":
                return "jp_cn";
            case "KR":
                return "kr_brn";
            case "LI":
                return "li_uid";
            case "MX":
                return "mx_rfc";
            case "MY":
                return "my_sst";
            case "NO":
                return "no_vat";
            case "NZ":
                return "nz_gst";
            case "PE":
                return "pe_ruc";
            case "PH":
                return "ph_tin";
            case "RS":
                return "rs_pib";
            case "RU":
                return "ru_inn";
            case "SA":
                return "sa_vat";
            case "SG":
                return "sg_gst";
            case "SV":
                return "sv_nit";
            case "TH":
                return "th_vat";
            case "TR":
                return "tr_tin";
            case "TW":
                return "tw_vat";
            case "UA":
                return "ua_vat";
            case "US":
                return "us_ein";
            case "UY":
                return "uy_ruc";
            case "VE":
                return "ve_rif";
            case "VN":
                return "vn_tin";
            case "ZA":
                return "za_vat";
            default:
                return null;
        }
    }
}
