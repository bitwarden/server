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

[ControllerCustomize(typeof(AppleController))]
[SutProviderCustomize]
public class AppleControllerTests
{
    [Theory]
    [BitAutoData]
    public async Task PostIap_NullHttpContext_BadRequest(SutProvider<AppleController> sutProvider)
    {
        sutProvider.Sut.ControllerContext = new ControllerContext
        {
            HttpContext = null
        };

        var response = await sutProvider.Sut.PostIap();

        Assert.IsType<BadRequestResult>(response);
    }

    [Theory]
    [BitAutoData]
    public async Task PostIap_EmptyBody_BadRequest(SutProvider<AppleController> sutProvider)
    {
        var billingSettings = new BillingSettings { AppleWebhookKey = "test-key" };
        sutProvider.GetDependency<IOptions<BillingSettings>>().Value.Returns(billingSettings);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?key=test-key");
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(""));

        sutProvider.Sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        var response = await sutProvider.Sut.PostIap();

        Assert.IsType<BadRequestResult>(response);
    }

    [Theory]
    [BitAutoData]
    public async Task PostIap_InvalidJson_BadRequest(SutProvider<AppleController> sutProvider)
    {
        var billingSettings = new BillingSettings { AppleWebhookKey = "test-key" };
        sutProvider.GetDependency<IOptions<BillingSettings>>().Value.Returns(billingSettings);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?key=test-key");
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("invalid json"));

        sutProvider.Sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        var response = await sutProvider.Sut.PostIap();

        Assert.IsType<BadRequestResult>(response);
    }
}
