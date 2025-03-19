using Bit.Api.AdminConsole.Controllers;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Controllers;

[ControllerCustomize(typeof(SlackOAuthController))]
[SutProviderCustomize]
public class SlackOAuthControllerTests
{
    [Theory, BitAutoData]
    public async Task OAuthCallback_ThrowsBadResultWhenCodeIsEmpty(SutProvider<SlackOAuthController> sutProvider)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();

        var requestAction = await sutProvider.Sut.OAuthCallback(string.Empty);

        Assert.IsType<BadRequestObjectResult>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task OAuthCallback_ThrowsBadResultWhenSlackServiceReturnsEmpty(SutProvider<SlackOAuthController> sutProvider)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ISlackService>()
            .ObtainTokenViaOAuth(Arg.Any<string>(), Arg.Any<string>())
            .Returns(string.Empty);

        var requestAction = await sutProvider.Sut.OAuthCallback("A_test_code");

        Assert.IsType<BadRequestObjectResult>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task OAuthCallback_CompletesSuccessfully(SutProvider<SlackOAuthController> sutProvider)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ISlackService>()
            .ObtainTokenViaOAuth(Arg.Any<string>(), Arg.Any<string>())
            .Returns("xoxb-test-token");

        var requestAction = await sutProvider.Sut.OAuthCallback("A_test_code");

        Assert.IsType<OkObjectResult>(requestAction);
    }

    [Theory, BitAutoData]
    public void Redirect_ShouldRedirectToSlack(SutProvider<SlackOAuthController> sutProvider)
    {
        var expectedUrl = "https://localhost/";

        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ISlackService>().GetRedirectUrl(Arg.Any<string>()).Returns(expectedUrl);

        var requestAction = sutProvider.Sut.RedirectToSlack();

        var redirectResult = Assert.IsType<RedirectResult>(requestAction);
        Assert.Equal(expectedUrl, redirectResult.Url);
    }

    [Theory, BitAutoData]
    public void Redirect_ThrowsBadResultWhenSlackServiceReturnsEmpty(SutProvider<SlackOAuthController> sutProvider)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ISlackService>().GetRedirectUrl(Arg.Any<string>()).Returns(string.Empty);

        var requestAction = sutProvider.Sut.RedirectToSlack();

        Assert.IsType<BadRequestObjectResult>(requestAction);
    }
}
