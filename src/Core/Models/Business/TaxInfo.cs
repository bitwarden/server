namespace Bit.Core.Models.Business;

public class TaxInfo
{
    private string _taxIdNumber = null;
    private string _taxIdType = null;

    public string TaxIdNumber
    {
        get => _taxIdNumber;
        set
        {
            _taxIdNumber = value;
            _taxIdType = null;
        }
    }
    public string StripeTaxRateId { get; set; }
    public string BillingAddressLine1 { get; set; }
    public string BillingAddressLine2 { get; set; }
    public string BillingAddressCity { get; set; }
    public string BillingAddressState { get; set; }
    public string BillingAddressPostalCode { get; set; }
    public string BillingAddressCountry { get; set; } = "US";
    public string TaxIdType
    {
        get
        {
            if (string.IsNullOrWhiteSpace(BillingAddressCountry) ||
                string.IsNullOrWhiteSpace(TaxIdNumber))
            {
                return null;
            }
            if (!string.IsNullOrWhiteSpace(_taxIdType))
            {
                return _taxIdType;
            }

            switch (BillingAddressCountry)
            {
                case "AE":
                    _taxIdType = "ae_trn";
                    break;
                case "AU":
                    _taxIdType = "au_abn";
                    break;
                case "BR":
                    _taxIdType = "br_cnpj";
                    break;
                case "CA":
                    // May break for those in Québec given the assumption of QST
                    if (BillingAddressState?.Contains("bec") ?? false)
                    {
                        _taxIdType = "ca_qst";
                        break;
                    }
                    _taxIdType = "ca_bn";
                    break;
                case "CL":
                    _taxIdType = "cl_tin";
                    break;
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
                    _taxIdType = "eu_vat";
                    break;
                case "HK":
                    _taxIdType = "hk_br";
                    break;
                case "IN":
                    _taxIdType = "in_gst";
                    break;
                case "JP":
                    _taxIdType = "jp_cn";
                    break;
                case "KR":
                    _taxIdType = "kr_brn";
                    break;
                case "LI":
                    _taxIdType = "li_uid";
                    break;
                case "MX":
                    _taxIdType = "mx_rfc";
                    break;
                case "MY":
                    _taxIdType = "my_sst";
                    break;
                case "NO":
                    _taxIdType = "no_vat";
                    break;
                case "NZ":
                    _taxIdType = "nz_gst";
                    break;
                case "RU":
                    _taxIdType = "ru_inn";
                    break;
                case "SA":
                    _taxIdType = "sa_vat";
                    break;
                case "SG":
                    _taxIdType = "sg_gst";
                    break;
                case "TH":
                    _taxIdType = "th_vat";
                    break;
                case "TW":
                    _taxIdType = "tw_vat";
                    break;
                case "US":
                    _taxIdType = "us_ein";
                    break;
                case "ZA":
                    _taxIdType = "za_vat";
                    break;
                default:
                    _taxIdType = null;
                    break;
            }

            return _taxIdType;
        }
    }

    public bool HasTaxId
    {
        get => !string.IsNullOrWhiteSpace(TaxIdNumber) &&
            !string.IsNullOrWhiteSpace(TaxIdType);
    }
}
