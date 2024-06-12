#nullable enable
using System.Security.Claims;
using Bit.Api.SecretsManager.Controllers;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Commercial.Core.SecretsManager.Commands.PasswordManager;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.SecretsManager.Controllers;

[ControllerCustomize(typeof(RequestSMAccessController))]
[SutProviderCustomize]
public class RequestSMAccessControllerTests
{
    [Theory]
    [BitAutoData]
    public async Task RequestSMAccessFromAdminst_WhenSendingNoData_ShouldThrowArgumentNullException(
    User user, SutProvider<RequestSMAccessController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(Task.FromResult(user));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.RequestSMAccessFromAdmins(new RequestSMAccessRequestModel()));
    }

    [Theory]
    [BitAutoData]
    public async Task RequestSMAccessFromAdminst_WhenSendingValidData_ShouldSucceed(
    User user,
    RequestSMAccessRequestModel model,
    Core.AdminConsole.Entities.Organization org,
    SutProvider<RequestSMAccessController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(Task.FromResult(user));
        model.OrganizationId = org.Id.ToString();

        sutProvider.GetDependency<IRequestSMAccessCommand>().SendRequestAccessToSM(org.Id, user, model.EmailContent)
            .ReturnsForAnyArgs(true);

        // Act & Assert
        await sutProvider.Sut.RequestSMAccessFromAdmins(model);
    }
   
    [Theory]
    [BitAutoData]
    public async Task RequestSMAccessFromAdminst_WhenUserInvalid_ShouldThrowBadRequestException(
        RequestSMAccessRequestModel model, SutProvider<RequestSMAccessController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).ReturnsForAnyArgs(Task.FromResult((User)null));
       
        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sutProvider.Sut.RequestSMAccessFromAdmins(model));
    }
}
