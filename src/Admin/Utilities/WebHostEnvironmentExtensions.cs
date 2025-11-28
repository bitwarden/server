namespace Bit.Admin.Utilities;

public static class WebHostEnvironmentExtensions
{
    public static string GetStripeUrl(this IWebHostEnvironment hostingEnvironment)
    {
        if (hostingEnvironment.IsDevelopment() || hostingEnvironment.IsEnvironment("QA"))
        {
            return "https://dashboard.stripe.com/test";
        }

        return "https://dashboard.stripe.com";
    }

    public static string GetBraintreeMerchantUrl(this IWebHostEnvironment hostingEnvironment)
    {
        if (hostingEnvironment.IsDevelopment() || hostingEnvironment.IsEnvironment("QA"))
        {
            return "https://www.sandbox.braintreegateway.com/merchants";
        }

        return "https://www.braintreegateway.com/merchants";
    }
}
