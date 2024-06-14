#nullable enable
using System.Security.Claims;
using Bit.Api.SecretsManager.Controllers;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using Bit.Core.Repositories;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Exceptions;

namespace Bit.Api.Test.SecretsManager.Controllers;

[ControllerCustomize(typeof(RequestSMAccessController))]
[SutProviderCustomize]
public class RequestSMAccessControllerTests
{
    [Theory]
    [BitAutoData]
    public async Task RequestSMAccessFromAdminst_WhenSendingNoModel_ShouldThrowNotFoundException(
    User user, Organization org, SutProvider<RequestSMAccessController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(Task.FromResult(user));
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdentifierAsync(Arg.Any<string>()).ReturnsForAnyArgs((Organization)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.RequestSMAccessFromAdmins(new RequestSMAccessRequestModel()));
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
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdentifierAsync(Arg.Any<string>()).ReturnsForAnyArgs(Task.FromResult(org));
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(Task.FromResult(user));
        model.OrganizationId = org.Id;

        // Act & Assert
        await sutProvider.Sut.RequestSMAccessFromAdmins(model);
    }

    [Theory]
    [BitAutoData]
    public async Task RequestSMAccessFromAdminst_WhenUserInvalid_ShouldThrowBadRequestException(RequestSMAccessRequestModel model, Organization organization, SutProvider<RequestSMAccessController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).ReturnsForAnyArgs(Task.FromResult((User)null));

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sutProvider.Sut.RequestSMAccessFromAdmins(model));
    }

    [Theory]
    [BitAutoData]
    public async Task RequestSMAccessFromAdminst_WhenOrgInvalid_ShouldThrowNotFoundException(RequestSMAccessRequestModel model, User user, SutProvider<RequestSMAccessController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdentifierAsync(Arg.Any<string>()).ReturnsForAnyArgs(Task.FromResult((Organization)null));
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).ReturnsForAnyArgs(Task.FromResult(user));

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.RequestSMAccessFromAdmins(model));
    }
}
