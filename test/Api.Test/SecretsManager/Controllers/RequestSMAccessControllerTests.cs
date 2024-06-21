#nullable enable
using System.Security.Claims;
using Bit.Api.SecretsManager.Controllers;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Commercial.Core.SecretsManager.Commands.Requests;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Commands.Requests.Interfaces;
using Bit.Core.Services;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
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
    public async Task RequestSMAccessFromAdminst_WhenSendingNoModel_ShouldThrowNotFoundException(
    User user, SutProvider<RequestSMAccessController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(Task.FromResult(user));
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdentifierAsync(Arg.Any<string>()).ReturnsForAnyArgs((Core.AdminConsole.Entities.Organization)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.RequestSMAccessFromAdmins(new RequestSMAccessRequestModel()));
    }

    [Theory]
    [BitAutoData]
    public async Task RequestSMAccessFromAdmins_WhenSendingValidData_ShouldSucceed(
    User user,
    RequestSMAccessRequestModel model,
    Core.AdminConsole.Entities.Organization org,
    ICollection<OrganizationUserUserDetails> orgUsers,
    SutProvider<RequestSMAccessController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(model.OrganizationId).Returns(Task.FromResult(org));
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(Task.FromResult(user));

        // Act & Assert
        await sutProvider.Sut.RequestSMAccessFromAdmins(model);

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(org.Id).Returns(Task.FromResult(orgUsers));

        //Also check that the command was called
        await sutProvider.GetDependency<IRequestSMAccessCommand>()
            .Received(1)
            .SendRequestAccessToSM(org, orgUsers, user, model.EmailContent);
    }

    [Theory]
    [BitAutoData]
    public async Task RequestSMAccessFromAdmins_WhenUserInvalid_ShouldThrowBadRequestException(RequestSMAccessRequestModel model, SutProvider<RequestSMAccessController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).ReturnsForAnyArgs(Task.FromResult((User)null));

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sutProvider.Sut.RequestSMAccessFromAdmins(model));
    }

    [Theory]
    [BitAutoData]
    public async Task RequestSMAccessFromAdmins_WhenOrgInvalid_ShouldThrowNotFoundException(RequestSMAccessRequestModel model, User user, SutProvider<RequestSMAccessController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdentifierAsync(Arg.Any<string>()).ReturnsForAnyArgs(Task.FromResult((Core.AdminConsole.Entities.Organization)null));
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).ReturnsForAnyArgs(Task.FromResult(user));

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.RequestSMAccessFromAdmins(model));
    }
}
