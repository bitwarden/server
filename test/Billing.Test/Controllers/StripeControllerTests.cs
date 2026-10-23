using System.Text;
using Bit.Billing.Controllers;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Bit.Billing.Test.Controllers;

[ControllerCustomize(typeof(StripeController))]
[SutProviderCustomize]
public class StripeControllerTests
{
    [Theory]
    [BitAutoData]
    public async Task PostWebhook_InvalidWebhookKey_BadRequest(SutProvider<StripeController> sutProvider)
    {
        var billingSettings = new BillingSettings { StripeWebhookKey = "correct-key" };
        sutProvider.GetDependency<IOptions<BillingSettings>>().Value.Returns(billingSettings);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

        sutProvider.Sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        var response = await sutProvider.Sut.PostWebhook("definitely-wrong-key");

        Assert.IsType<BadRequestResult>(response);
    }
}
