using Bit.Core.Models.Business;
using Xunit;

namespace Bit.Core.Test.Models.Business;

public class TaxInfoTests
{
    // PH = Placeholder
    [Theory]
    [InlineData(null, null, null, null)]
    [InlineData("", "", null, null)]
    [InlineData("PH", "", null, null)]
    [InlineData("", "PH", null, null)]
    [InlineData("AE", "PH", null, "ae_trn")]
    [InlineData("AU", "PH", null, "au_abn")]
    [InlineData("BR", "PH", null, "br_cnpj")]
    [InlineData("CA", "PH", "bec", "ca_qst")]
    [InlineData("CA", "PH", null, "ca_bn")]
    [InlineData("CL", "PH", null, "cl_tin")]
    [InlineData("AT", "PH", null, "eu_vat")]
    [InlineData("BE", "PH", null, "eu_vat")]
    [InlineData("BG", "PH", null, "eu_vat")]
    [InlineData("CY", "PH", null, "eu_vat")]
    [InlineData("CZ", "PH", null, "eu_vat")]
    [InlineData("DE", "PH", null, "eu_vat")]
    [InlineData("DK", "PH", null, "eu_vat")]
    [InlineData("EE", "PH", null, "eu_vat")]
    [InlineData("ES", "PH", null, "eu_vat")]
    [InlineData("FI", "PH", null, "eu_vat")]
    [InlineData("FR", "PH", null, "eu_vat")]
    [InlineData("GB", "PH", null, "eu_vat")]
    [InlineData("GR", "PH", null, "eu_vat")]
    [InlineData("HR", "PH", null, "eu_vat")]
    [InlineData("HU", "PH", null, "eu_vat")]
    [InlineData("IE", "PH", null, "eu_vat")]
    [InlineData("IT", "PH", null, "eu_vat")]
    [InlineData("LT", "PH", null, "eu_vat")]
    [InlineData("LU", "PH", null, "eu_vat")]
    [InlineData("LV", "PH", null, "eu_vat")]
    [InlineData("MT", "PH", null, "eu_vat")]
    [InlineData("NL", "PH", null, "eu_vat")]
    [InlineData("PL", "PH", null, "eu_vat")]
    [InlineData("PT", "PH", null, "eu_vat")]
    [InlineData("RO", "PH", null, "eu_vat")]
    [InlineData("SE", "PH", null, "eu_vat")]
    [InlineData("SI", "PH", null, "eu_vat")]
    [InlineData("SK", "PH", null, "eu_vat")]
    [InlineData("HK", "PH", null, "hk_br")]
    [InlineData("IN", "PH", null, "in_gst")]
    [InlineData("JP", "PH", null, "jp_cn")]
    [InlineData("KR", "PH", null, "kr_brn")]
    [InlineData("LI", "PH", null, "li_uid")]
    [InlineData("MX", "PH", null, "mx_rfc")]
    [InlineData("MY", "PH", null, "my_sst")]
    [InlineData("NO", "PH", null, "no_vat")]
    [InlineData("NZ", "PH", null, "nz_gst")]
    [InlineData("RU", "PH", null, "ru_inn")]
    [InlineData("SA", "PH", null, "sa_vat")]
    [InlineData("SG", "PH", null, "sg_gst")]
    [InlineData("TH", "PH", null, "th_vat")]
    [InlineData("TW", "PH", null, "tw_vat")]
    [InlineData("US", "PH", null, "us_ein")]
    [InlineData("ZA", "PH", null, "za_vat")]
    [InlineData("ABCDEF", "PH", null, null)]
    public void GetTaxIdType_Success(string billingAddressCountry,
        string taxIdNumber,
        string billingAddressState,
        string expectedTaxIdType)
    {
        var taxInfo = new TaxInfo
        {
            BillingAddressCountry = billingAddressCountry,
            TaxIdNumber = taxIdNumber,
            BillingAddressState = billingAddressState,
        };

        Assert.Equal(expectedTaxIdType, taxInfo.TaxIdType);
    }

    [Fact]
    public void GetTaxIdType_CreateOnce_ReturnCacheSecondTime()
    {
        var taxInfo = new TaxInfo
        {
            BillingAddressCountry = "US",
            TaxIdNumber = "PH",
            BillingAddressState = null,
        };

        Assert.Equal("us_ein", taxInfo.TaxIdType);

        // Per the current spec even if the values change to something other than null it
        // will return the cached version of TaxIdType.
        taxInfo.BillingAddressCountry = "ZA";

        Assert.Equal("us_ein", taxInfo.TaxIdType);
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("123", "US", true)]
    [InlineData("123", "ZQ12", false)]
    [InlineData("    ", "US", false)]
    public void HasTaxId_ReturnsExpected(string taxIdNumber, string billingAddressCountry, bool expected)
    {
        var taxInfo = new TaxInfo
        {
            TaxIdNumber = taxIdNumber,
            BillingAddressCountry = billingAddressCountry,
        };

        Assert.Equal(expected, taxInfo.HasTaxId);
    }
}
