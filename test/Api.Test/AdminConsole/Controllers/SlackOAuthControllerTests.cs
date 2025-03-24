using Bit.Api.AdminConsole.Controllers;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Integrations;
using Bit.Core.Repositories;
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
    public async Task OAuthCallback_CompletesSuccessfully(SutProvider<SlackOAuthController> sutProvider, Guid organizationId)
    {
        var token = "xoxb-test-token";
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<ISlackService>()
            .ObtainTokenViaOAuth(Arg.Any<string>(), Arg.Any<string>())
            .Returns(token);

        var requestAction = await sutProvider.Sut.OAuthCallback(organizationId.ToString(), "A_test_code");

        await sutProvider.GetDependency<IOrganizationIntegrationConfigurationRepository>().Received(1)
            .CreateOrganizationIntegrationAsync(organizationId, IntegrationType.Slack, new SlackIntegration(token));
        Assert.IsType<OkObjectResult>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task OAuthCallback_ThrowsBadResultWhenCodeIsEmpty(SutProvider<SlackOAuthController> sutProvider, Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.OAuthCallback(organizationId.ToString(), string.Empty));
    }

    [Theory, BitAutoData]
    public async Task OAuthCallback_ThrowsBadResultWhenSlackServiceReturnsEmpty(SutProvider<SlackOAuthController> sutProvider, Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<ISlackService>()
            .ObtainTokenViaOAuth(Arg.Any<string>(), Arg.Any<string>())
            .Returns(string.Empty);

        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.OAuthCallback(organizationId.ToString(), "A_test_code"));
    }

    [Theory, BitAutoData]
    public async Task OAuthCallback_ThrowsNotFoundIfUserIsNotOrganizationAdmin(SutProvider<SlackOAuthController> sutProvider, Guid organizationId)
    {
        var token = "xoxb-test-token";
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);
        sutProvider.GetDependency<ISlackService>()
            .ObtainTokenViaOAuth(Arg.Any<string>(), Arg.Any<string>())
            .Returns(token);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.OAuthCallback(organizationId.ToString(), "A_test_code"));
    }

    [Theory, BitAutoData]
    public async Task Redirect_ShouldRedirectToSlack(SutProvider<SlackOAuthController> sutProvider, Guid organizationId)
    {
        var expectedUrl = $"https://localhost/{organizationId.ToString()}";

        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ISlackService>().GetRedirectUrl(Arg.Any<string>()).Returns(expectedUrl);
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<ICurrentContext>()
            .HttpContext.Request.Scheme
            .Returns("https");

        var requestAction = await sutProvider.Sut.RedirectToSlack(organizationId.ToString());

        var redirectResult = Assert.IsType<RedirectResult>(requestAction);
        Assert.Equal(expectedUrl, redirectResult.Url);
    }

    [Theory, BitAutoData]
    public async Task Redirect_ThrowsNotFoundWhenSlackServiceReturnsEmpty(SutProvider<SlackOAuthController> sutProvider, Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ISlackService>().GetRedirectUrl(Arg.Any<string>()).Returns(string.Empty);
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(true);
        sutProvider.GetDependency<ICurrentContext>()
            .HttpContext.Request.Scheme
            .Returns("https");

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.RedirectToSlack(organizationId.ToString()));
    }

    [Theory, BitAutoData]
    public async Task Redirect_ThrowsNotFoundWhenUserIsNotOrganizationAdmin(SutProvider<SlackOAuthController> sutProvider,
        Guid organizationId)
    {
        sutProvider.Sut.Url = Substitute.For<IUrlHelper>();
        sutProvider.GetDependency<ISlackService>().GetRedirectUrl(Arg.Any<string>()).Returns(string.Empty);
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(false);
        sutProvider.GetDependency<ICurrentContext>()
            .HttpContext.Request.Scheme
            .Returns("https");

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.RedirectToSlack(organizationId.ToString()));
    }
}
