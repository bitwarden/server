using System.Text.RegularExpressions;
using Bit.Core.Billing.Models;

namespace Bit.Core.Billing.Services;

public class TaxService : ITaxService
{
    /// <summary>
    /// Retrieves a list of supported tax ID types for customers.
    /// </summary>
    /// <remarks>Compiled list from <see href="https://docs.stripe.com/billing/customer/tax-ids">Stripe</see></remarks>
    private static readonly IEnumerable<TaxIdType> _taxIdTypes =
    [
        new()
        {
            Country = "AD",
            Code = "ad_nrt",
            Description = "Andorran NRT number",
            Example = "A-123456-Z",
            ValidationExpression = new Regex("^([A-Z]{1})-?([0-9]{6})-?([A-Z]{1})$")
        },
        new()
        {
            Country = "AR",
            Code = "ar_cuit",
            Description = "Argentinian tax ID number",
            Example = "12-34567890-1",
            ValidationExpression = new Regex("^([0-9]{2})-?([0-9]{8})-?([0-9]{1})$")
        },
        new()
        {
            Country = "AU",
            Code = "au_abn",
            Description = "Australian Business Number (AU ABN)",
            Example = "123456789012",
            ValidationExpression = new Regex("^[0-9]{11}$")
        },
        new()
        {
            Country = "AU",
            Code = "au_arn",
            Description = "Australian Taxation Office Reference Number",
            Example = "123456789123",
            ValidationExpression = new Regex("^[0-9]{12}$")
        },
        new()
        {
            Country = "AT",
            Code = "eu_vat",
            Description = "European VAT number (Austria)",
            Example = "ATU12345678",
            ValidationExpression = new Regex("^ATU[0-9]{8}$")
        },
        new()
        {
            Country = "BH",
            Code = "bh_vat",
            Description = "Bahraini VAT Number",
            Example = "123456789012345",
            ValidationExpression = new Regex("^[0-9]{15}$")
        },
        new()
        {
            Country = "BY",
            Code = "by_tin",
            Description = "Belarus TIN Number",
            Example = "123456789",
            ValidationExpression = new Regex("^[0-9]{9}$")
        },
        new()
        {
            Country = "BE",
            Code = "eu_vat",
            Description = "European VAT number (Belgium)",
            Example = "BE0123456789",
            ValidationExpression = new Regex("^BE[0-9]{10}$")
        },
        new()
        {
            Country = "BO",
            Code = "bo_tin",
            Description = "Bolivian tax ID",
            Example = "123456789",
            ValidationExpression = new Regex("^[0-9]{9}$")
        },
        new()
        {
            Country = "BR",
            Code = "br_cnpj",
            Description = "Brazilian CNPJ number",
            Example = "01.234.456/5432-10",
            ValidationExpression = new Regex("^[0-9]{2}.?[0-9]{3}.?[0-9]{3}/?[0-9]{4}-?[0-9]{2}$")
        },
        new()
        {
            Country = "BR",
            Code = "br_cpf",
            Description = "Brazilian CPF number",
            Example = "123.456.789-87",
            ValidationExpression = new Regex("^[0-9]{3}.?[0-9]{3}.?[0-9]{3}-?[0-9]{2}$")
        },
        new()
        {
            Country = "BG",
            Code = "bg_uic",
            Description = "Bulgaria Unified Identification Code",
            Example = "123456789",
            ValidationExpression = new Regex("^[0-9]{9}$")
        },
        new()
        {
            Country = "BG",
            Code = "eu_vat",
            Description = "European VAT number (Bulgaria)",
            Example = "BG0123456789",
            ValidationExpression = new Regex("^BG[0-9]{9,10}$")
        },
        new()
        {
            Country = "CA",
            Code = "ca_bn",
            Description = "Canadian BN",
            Example = "123456789",
            ValidationExpression = new Regex("^[0-9]{9}$")
        },
        new()
        {
            Country = "CA",
            Code = "ca_gst_hst",
            Description = "Canadian GST/HST number",
            Example = "123456789RT0002",
            ValidationExpression = new Regex("^[0-9]{9}RT[0-9]{4}$")
        },
        new()
        {
            Country = "CA",
            Code = "ca_pst_bc",
            Description = "Canadian PST number (British Columbia)",
            Example = "PST-1234-5678",
            ValidationExpression = new Regex("^PST-[0-9]{4}-[0-9]{4}$")
        },
        new()
        {
            Country = "CA",
            Code = "ca_pst_mb",
            Description = "Canadian PST number (Manitoba)",
            Example = "123456-7",
            ValidationExpression = new Regex("^[0-9]{6}-[0-9]{1}$")
        },
        new()
        {
            Country = "CA",
            Code = "ca_pst_sk",
            Description = "Canadian PST number (Saskatchewan)",
            Example = "1234567",
            ValidationExpression = new Regex("^[0-9]{7}$")
        },
        new()
        {
            Country = "CA",
            Code = "ca_qst",
            Description = "Canadian QST number (Québec)",
            Example = "1234567890TQ1234",
            ValidationExpression = new Regex("^[0-9]{10}TQ[0-9]{4}$")
        },
        new()
        {
            Country = "CL",
            Code = "cl_tin",
            Description = "Chilean TIN",
            Example = "12.345.678-K",
            ValidationExpression = new Regex("^[0-9]{2}.?[0-9]{3}.?[0-9]{3}-?[0-9A-Z]{1}$")
        },
        new()
        {
            Country = "CN",
            Code = "cn_tin",
            Description = "Chinese tax ID",
            Example = "123456789012345678",
            ValidationExpression = new Regex("^[0-9]{15,18}$")
        },
        new()
        {
            Country = "CO",
            Code = "co_nit",
            Description = "Colombian NIT number",
            Example = "123.456.789-0",
            ValidationExpression = new Regex("^[0-9]{3}.?[0-9]{3}.?[0-9]{3}-?[0-9]{1}$")
        },
        new()
        {
            Country = "CR",
            Code = "cr_tin",
            Description = "Costa Rican tax ID",
            Example = "1-234-567890",
            ValidationExpression = new Regex("^[0-9]{1}-?[0-9]{3}-?[0-9]{6}$")
        },
        new()
        {
            Country = "HR",
            Code = "eu_vat",
            Description = "European VAT number (Croatia)",
            Example = "HR12345678912",
            ValidationExpression = new Regex("^HR[0-9]{11}$")
        },
        new()
        {
            Country = "HR",
            Code = "hr_oib",
            Description = "Croatian Personal Identification Number",
            Example = "12345678901",
            ValidationExpression = new Regex("^[0-9]{11}$")
        },
        new()
        {
            Country = "CY",
            Code = "eu_vat",
            Description = "European VAT number (Cyprus)",
            Example = "CY12345678X",
            ValidationExpression = new Regex("^CY[0-9]{8}[A-Z]{1}$")
        },
        new()
        {
            Country = "CZ",
            Code = "eu_vat",
            Description = "European VAT number (Czech Republic)",
            Example = "CZ12345678",
            ValidationExpression = new Regex("^CZ[0-9]{8,10}$")
        },
        new()
        {
            Country = "DK",
            Code = "eu_vat",
            Description = "European VAT number (Denmark)",
            Example = "DK12345678",
            ValidationExpression = new Regex("^DK[0-9]{8}$")
        },
        new()
        {
            Country = "DO",
            Code = "do_rcn",
            Description = "Dominican RCN number",
            Example = "123-4567890-1",
            ValidationExpression = new Regex("^[0-9]{3}-?[0-9]{7}-?[0-9]{1}$")
        },
        new()
        {
            Country = "EC",
            Code = "ec_ruc",
            Description = "Ecuadorian RUC number",
            Example = "1234567890001",
            ValidationExpression = new Regex("^[0-9]{13}$")
        },
        new()
        {
            Country = "EG",
            Code = "eg_tin",
            Description = "Egyptian Tax Identification Number",
            Example = "123456789",
            ValidationExpression = new Regex("^[0-9]{9}$")
        },

        new()
        {
            Country = "SV",
            Code = "sv_nit",
            Description = "El Salvadorian NIT number",
            Example = "1234-567890-123-4",
            ValidationExpression = new Regex("^[0-9]{4}-?[0-9]{6}-?[0-9]{3}-?[0-9]{1}$")
        },

        new()
        {
            Country = "EE",
            Code = "eu_vat",
            Description = "European VAT number (Estonia)",
            Example = "EE123456789",
            ValidationExpression = new Regex("^EE[0-9]{9}$")
        },

        new()
        {
            Country = "EU",
            Code = "eu_oss_vat",
            Description = "European One Stop Shop VAT number for non-Union scheme",
            Example = "EU123456789",
            ValidationExpression = new Regex("^EU[0-9]{9}$")
        },
        new()
        {
            Country = "FI",
            Code = "eu_vat",
            Description = "European VAT number (Finland)",
            Example = "FI12345678",
            ValidationExpression = new Regex("^FI[0-9]{8}$")
        },
        new()
        {
            Country = "FR",
            Code = "eu_vat",
            Description = "European VAT number (France)",
            Example = "FR12345678901",
            ValidationExpression = new Regex("^FR[0-9A-Z]{2}[0-9]{9}$")
        },
        new()
        {
            Country = "GE",
            Code = "ge_vat",
            Description = "Georgian VAT",
            Example = "123456789",
            ValidationExpression = new Regex("^[0-9]{9}$")
        },
        new()
        {
            Country = "DE",
            Code = "de_stn",
            Description = "German Tax Number (Steuernummer)",
            Example = "1234567890",
            ValidationExpression = new Regex("^[0-9]{10}$")
        },
        new()
        {
            Country = "DE",
            Code = "eu_vat",
            Description = "European VAT number (Germany)",
            Example = "DE123456789",
            ValidationExpression = new Regex("^DE[0-9]{9}$")
        },
        new()
        {
            Country = "GR",
            Code = "eu_vat",
            Description = "European VAT number (Greece)",
            Example = "EL123456789",
            ValidationExpression = new Regex("^EL[0-9]{9}$")
        },
        new()
        {
            Country = "HK",
            Code = "hk_br",
            Description = "Hong Kong BR number",
            Example = "12345678",
            ValidationExpression = new Regex("^[0-9]{8}$")
        },
        new()
        {
            Country = "HU",
            Code = "eu_vat",
            Description = "European VAT number (Hungaria)",
            Example = "HU12345678",
            ValidationExpression = new Regex("^HU[0-9]{8}$")
        },
        new()
        {
            Country = "HU",
            Code = "hu_tin",
            Description = "Hungary tax number (adószám)",
            Example = "12345678-1-23",
            ValidationExpression = new Regex("^[0-9]{8}-?[0-9]-?[0-9]{2}$")
        },
        new()
        {
            Country = "IS",
            Code = "is_vat",
            Description = "Icelandic VAT",
            Example = "123456",
            ValidationExpression = new Regex("^[0-9]{6}$")
        },
        new()
        {
            Country = "IN",
            Code = "in_gst",
            Description = "Indian GST number",
            Example = "12ABCDE3456FGZH",
            ValidationExpression = new Regex("^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$")
        },
        new()
        {
            Country = "ID",
            Code = "id_npwp",
            Description = "Indonesian NPWP number",
            Example = "012.345.678.9-012.345",
            ValidationExpression = new Regex("^[0-9]{3}.?[0-9]{3}.?[0-9]{3}.?[0-9]{1}-?[0-9]{3}.?[0-9]{3}$")
        },
        new()
        {
            Country = "IE",
            Code = "eu_vat",
            Description = "European VAT number (Ireland)",
            Example = "IE1234567AB",
            ValidationExpression = new Regex("^IE[0-9]{7}[A-Z]{1,2}$")
        },
        new()
        {
            Country = "IL",
            Code = "il_vat",
            Description = "Israel VAT",
            Example = "000012345",
            ValidationExpression = new Regex("^[0-9]{9}$")
        },
        new()
        {
            Country = "IT",
            Code = "eu_vat",
            Description = "European VAT number (Italy)",
            Example = "IT12345678912",
            ValidationExpression = new Regex("^IT[0-9]{11}$")
        },
        new()
        {
            Country = "JP",
            Code = "jp_cn",
            Description = "Japanese Corporate Number (*Hōjin Bangō*)",
            Example = "1234567891234",
            ValidationExpression = new Regex("^[0-9]{13}$")
        },
        new()
        {
            Country = "JP",
            Code = "jp_rn",
            Description =
                "Japanese Registered Foreign Businesses' Registration Number (*Tōroku Kokugai Jigyōsha no Tōroku Bangō*)",
            Example = "12345",
            ValidationExpression = new Regex("^[0-9]{5}$")
        },
        new()
        {
            Country = "JP",
            Code = "jp_trn",
            Description = "Japanese Tax Registration Number (*Tōroku Bangō*)",
            Example = "T1234567891234",
            ValidationExpression = new Regex("^T[0-9]{13}$")
        },
        new()
        {
            Country = "KZ",
            Code = "kz_bin",
            Description = "Kazakhstani Business Identification Number",
            Example = "123456789012",
            ValidationExpression = new Regex("^[0-9]{12}$")
        },
        new()
        {
            Country = "KE",
            Code = "ke_pin",
            Description = "Kenya Revenue Authority Personal Identification Number",
            Example = "P000111111A",
            ValidationExpression = new Regex("^[A-Z]{1}[0-9]{9}[A-Z]{1}$")
        },
        new()
        {
            Country = "LV",
            Code = "eu_vat",
            Description = "European VAT number",
            Example = "LV12345678912",
            ValidationExpression = new Regex("^LV[0-9]{11}$")
        },
        new()
        {
            Country = "LI",
            Code = "li_uid",
            Description = "Liechtensteinian UID number",
            Example = "CHE123456789",
            ValidationExpression = new Regex("^CHE[0-9]{9}$")
        },
        new()
        {
            Country = "LI",
            Code = "li_vat",
            Description = "Liechtensteinian VAT number",
            Example = "12345",
            ValidationExpression = new Regex("^[0-9]{5}$")
        },
        new()
        {
            Country = "LT",
            Code = "eu_vat",
            Description = "European VAT number (Lithuania)",
            Example = "LT123456789123",
            ValidationExpression = new Regex("^LT[0-9]{9,12}$")
        },
        new()
        {
            Country = "LU",
            Code = "eu_vat",
            Description = "European VAT number (Luxembourg)",
            Example = "LU12345678",
            ValidationExpression = new Regex("^LU[0-9]{8}$")
        },
        new()
        {
            Country = "MY",
            Code = "my_frp",
            Description = "Malaysian FRP number",
            Example = "12345678",
            ValidationExpression = new Regex("^[0-9]{8}$")
        },
        new()
        {
            Country = "MY",
            Code = "my_itn",
            Description = "Malaysian ITN",
            Example = "C 1234567890",
            ValidationExpression = new Regex("^[A-Z]{1} ?[0-9]{10}$")
        },
        new()
        {
            Country = "MY",
            Code = "my_sst",
            Description = "Malaysian SST number",
            Example = "A12-3456-78912345",
            ValidationExpression = new Regex("^[A-Z]{1}[0-9]{2}-?[0-9]{4}-?[0-9]{8}$")
        },
        new()
        {
            Country = "MT",
            Code = "eu_vat",
            Description = "European VAT number (Malta)",
            Example = "MT12345678",
            ValidationExpression = new Regex("^MT[0-9]{8}$")
        },
        new()
        {
            Country = "MX",
            Code = "mx_rfc",
            Description = "Mexican RFC number",
            Example = "ABC010203AB9",
            ValidationExpression = new Regex("^[A-Z]{3}[0-9]{6}[A-Z0-9]{3}$")
        },
        new()
        {
            Country = "MD",
            Code = "md_vat",
            Description = "Moldova VAT Number",
            Example = "1234567",
            ValidationExpression = new Regex("^[0-9]{7}$")
        },
        new()
        {
            Country = "MA",
            Code = "ma_vat",
            Description = "Morocco VAT Number",
            Example = "12345678",
            ValidationExpression = new Regex("^[0-9]{8}$")
        },
        new()
        {
            Country = "NL",
            Code = "eu_vat",
            Description = "European VAT number (Netherlands)",
            Example = "NL123456789B12",
            ValidationExpression = new Regex("^NL[0-9]{9}B[0-9]{2}$")
        },
        new()
        {
            Country = "NZ",
            Code = "nz_gst",
            Description = "New Zealand GST number",
            Example = "123456789",
            ValidationExpression = new Regex("^[0-9]{9}$")
        },
        new()
        {
            Country = "NG",
            Code = "ng_tin",
            Description = "Nigerian TIN Number",
            Example = "12345678-0001",
            ValidationExpression = new Regex("^[0-9]{8}-[0-9]{4}$")
        },
        new()
        {
            Country = "NO",
            Code = "no_vat",
            Description = "Norwegian VAT number",
            Example = "123456789MVA",
            ValidationExpression = new Regex("^[0-9]{9}MVA$")
        },
        new()
        {
            Country = "NO",
            Code = "no_voec",
            Description = "Norwegian VAT on e-commerce number",
            Example = "1234567",
            ValidationExpression = new Regex("^[0-9]{7}$")
        },
        new()
        {
            Country = "OM",
            Code = "om_vat",
            Description = "Omani VAT Number",
            Example = "OM1234567890",
            ValidationExpression = new Regex("^OM[0-9]{10}$")
        },
        new()
        {
            Country = "PE",
            Code = "pe_ruc",
            Description = "Peruvian RUC number",
            Example = "12345678901",
            ValidationExpression = new Regex("^[0-9]{11}$")
        },
        new()
        {
            Country = "PH",
            Code = "ph_tin",
            Description = "Philippines Tax Identification Number",
            Example = "123456789012",
            ValidationExpression = new Regex("^[0-9]{12}$")
        },
        new()
        {
            Country = "PL",
            Code = "eu_vat",
            Description = "European VAT number (Poland)",
            Example = "PL1234567890",
            ValidationExpression = new Regex("^PL[0-9]{10}$")
        },
        new()
        {
            Country = "PT",
            Code = "eu_vat",
            Description = "European VAT number (Portugal)",
            Example = "PT123456789",
            ValidationExpression = new Regex("^PT[0-9]{9}$")
        },
        new()
        {
            Country = "RO",
            Code = "eu_vat",
            Description = "European VAT number (Romania)",
            Example = "RO1234567891",
            ValidationExpression = new Regex("^RO[0-9]{2,10}$")
        },
        new()
        {
            Country = "RO",
            Code = "ro_tin",
            Description = "Romanian tax ID number",
            Example = "1234567890123",
            ValidationExpression = new Regex("^[0-9]{13}$")
        },
        new()
        {
            Country = "RU",
            Code = "ru_inn",
            Description = "Russian INN",
            Example = "1234567891",
            ValidationExpression = new Regex("^[0-9]{10,12}$")
        },
        new()
        {
            Country = "RU",
            Code = "ru_kpp",
            Description = "Russian KPP",
            Example = "123456789",
            ValidationExpression = new Regex("^[0-9]{9}$")
        },
        new()
        {
            Country = "SA",
            Code = "sa_vat",
            Description = "Saudi Arabia VAT",
            Example = "123456789012345",
            ValidationExpression = new Regex("^[0-9]{15}$")
        },
        new()
        {
            Country = "RS",
            Code = "rs_pib",
            Description = "Serbian PIB number",
            Example = "123456789",
            ValidationExpression = new Regex("^[0-9]{9}$")
        },
        new()
        {
            Country = "SG",
            Code = "sg_gst",
            Description = "Singaporean GST",
            Example = "M12345678X",
            ValidationExpression = new Regex("^[A-Z]{1}[0-9]{8}[A-Z]{1}$")
        },
        new()
        {
            Country = "SG",
            Code = "sg_uen",
            Description = "Singaporean UEN",
            Example = "123456789F",
            ValidationExpression = new Regex("^[0-9]{9}[A-Z]{1}$")
        },
        new()
        {
            Country = "SK",
            Code = "eu_vat",
            Description = "European VAT number (Slovakia)",
            Example = "SK1234567891",
            ValidationExpression = new Regex("^SK[0-9]{10}$")
        },
        new()
        {
            Country = "SI",
            Code = "eu_vat",
            Description = "European VAT number (Slovenia)",
            Example = "SI12345678",
            ValidationExpression = new Regex("^SI[0-9]{8}$")
        },
        new()
        {
            Country = "SI",
            Code = "si_tin",
            Description = "Slovenia tax number (davčna številka)",
            Example = "12345678",
            ValidationExpression = new Regex("^[0-9]{8}$")
        },
        new()
        {
            Country = "ZA",
            Code = "za_vat",
            Description = "South African VAT number",
            Example = "4123456789",
            ValidationExpression = new Regex("^[0-9]{10}$")
        },
        new()
        {
            Country = "KR",
            Code = "kr_brn",
            Description = "Korean BRN",
            Example = "123-45-67890",
            ValidationExpression = new Regex("^[0-9]{3}-?[0-9]{2}-?[0-9]{5}$")
        },
        new()
        {
            Country = "ES",
            Code = "es_cif",
            Description = "Spanish NIF/CIF number",
            Example = "A12345678",
            ValidationExpression = new Regex("^[A-Z]{1}[0-9]{8}$")
        },
        new()
        {
            Country = "ES",
            Code = "eu_vat",
            Description = "European VAT number (Spain)",
            Example = "ESA1234567Z",
            ValidationExpression = new Regex("^ES[A-Z]{1}[0-9]{7}[A-Z]{1}$")
        },
        new()
        {
            Country = "SE",
            Code = "eu_vat",
            Description = "European VAT number (Sweden)",
            Example = "SE123456789123",
            ValidationExpression = new Regex("^SE[0-9]{12}$")
        },
        new()
        {
            Country = "CH",
            Code = "ch_uid",
            Description = "Switzerland UID number",
            Example = "CHE-123.456.789 HR",
            ValidationExpression = new Regex("^CHE-?[0-9]{3}.?[0-9]{3}.?[0-9]{3} ?HR$")
        },
        new()
        {
            Country = "CH",
            Code = "ch_vat",
            Description = "Switzerland VAT number",
            Example = "CHE-123.456.789 MWST",
            ValidationExpression = new Regex("^CHE-?[0-9]{3}.?[0-9]{3}.?[0-9]{3} ?MWST$")
        },
        new()
        {
            Country = "TW",
            Code = "tw_vat",
            Description = "Taiwanese VAT",
            Example = "12345678",
            ValidationExpression = new Regex("^[0-9]{8}$")
        },
        new()
        {
            Country = "TZ",
            Code = "tz_vat",
            Description = "Tanzania VAT Number",
            Example = "12345678A",
            ValidationExpression = new Regex("^[0-9]{8}[A-Z]{1}$")
        },
        new()
        {
            Country = "TH",
            Code = "th_vat",
            Description = "Thai VAT",
            Example = "1234567891234",
            ValidationExpression = new Regex("^[0-9]{13}$")
        },
        new()
        {
            Country = "TR",
            Code = "tr_tin",
            Description = "Turkish TIN Number",
            Example = "0123456789",
            ValidationExpression = new Regex("^[0-9]{10}$")
        },
        new()
        {
            Country = "UA",
            Code = "ua_vat",
            Description = "Ukrainian VAT",
            Example = "123456789",
            ValidationExpression = new Regex("^[0-9]{9}$")
        },
        new()
        {
            Country = "AE",
            Code = "ae_trn",
            Description = "United Arab Emirates TRN",
            Example = "123456789012345",
            ValidationExpression = new Regex("^[0-9]{15}$")
        },
        new()
        {
            Country = "GB",
            Code = "eu_vat",
            Description = "Northern Ireland VAT number",
            Example = "XI123456789",
            ValidationExpression = new Regex("^XI[0-9]{9}$")
        },
        new()
        {
            Country = "GB",
            Code = "gb_vat",
            Description = "United Kingdom VAT number",
            Example = "GB123456789",
            ValidationExpression = new Regex("^GB[0-9]{9}$")
        },
        new()
        {
            Country = "US",
            Code = "us_ein",
            Description = "United States EIN",
            Example = "12-3456789",
            ValidationExpression = new Regex("^[0-9]{2}-?[0-9]{7}$")
        },
        new()
        {
            Country = "UY",
            Code = "uy_ruc",
            Description = "Uruguayan RUC number",
            Example = "123456789012",
            ValidationExpression = new Regex("^[0-9]{12}$")
        },
        new()
        {
            Country = "UZ",
            Code = "uz_tin",
            Description = "Uzbekistan TIN Number",
            Example = "123456789",
            ValidationExpression = new Regex("^[0-9]{9}$")
        },
        new()
        {
            Country = "UZ",
            Code = "uz_vat",
            Description = "Uzbekistan VAT Number",
            Example = "123456789012",
            ValidationExpression = new Regex("^[0-9]{12}$")
        },
        new()
        {
            Country = "VE",
            Code = "ve_rif",
            Description = "Venezuelan RIF number",
            Example = "A-12345678-9",
            ValidationExpression = new Regex("^[A-Z]{1}-?[0-9]{8}-?[0-9]{1}$")
        },
        new()
        {
            Country = "VN",
            Code = "vn_tin",
            Description = "Vietnamese tax ID number",
            Example = "1234567890",
            ValidationExpression = new Regex("^[0-9]{10}$")
        }
    ];

    /// <summary>
    /// Retrieves the Stripe tax code for a given country and tax ID.
    /// </summary>
    /// <param name="country"></param>
    /// <param name="taxId"></param>
    /// <returns>
    /// Returns the Stripe tax code if the tax ID is valid for the country.
    /// Returns null if the tax ID is invalid or the country is not supported.
    /// </returns>
    public string? GetStripeTaxCode(string country, string taxId)
    {
        foreach (var taxIdType in _taxIdTypes.Where(x => x.Country == country))
        {
            if (taxIdType.ValidationExpression.IsMatch(taxId))
            {
                return taxIdType.Code;
            }
        }

        return null;
    }

    public IEnumerable<TaxIdType> GetTaxIdTypes() => _taxIdTypes;
}
