using System.Security.Claims;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.Auth.Controllers;
using Bit.Api.Auth.Models.Response;
using Bit.Api.Models.Response;
using Bit.Api.Vault.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.UserFeatures.EmergencyAccess;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Auth.Controllers;

[ControllerCustomize(typeof(EmergencyAccessController))]
[SutProviderCustomize]
public class EmergencyAccessControllerTests
{
    [Theory, BitAutoData]
    public async Task GetContacts_ReturnsExpectedResult(
        SutProvider<EmergencyAccessController> sutProvider,
        User user,
        List<EmergencyAccessDetails> details)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(user.Id);

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByGrantorIdAsync(user.Id)
            .Returns(details);

        // Act
        var result = await sutProvider.Sut.GetContacts();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<ListResponseModel<EmergencyAccessGranteeDetailsResponseModel>>(result);
        Assert.Equal(details.Count, result.Data.Count());
    }

    [Theory, BitAutoData]
    public async Task GetGrantees_ReturnsExpectedResult(
        SutProvider<EmergencyAccessController> sutProvider,
        User user,
        List<EmergencyAccessDetails> details)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(user.Id);

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByGranteeIdAsync(user.Id)
            .Returns(details);

        // Act
        var result = await sutProvider.Sut.GetGrantees();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<ListResponseModel<EmergencyAccessGrantorDetailsResponseModel>>(result);
        Assert.Equal(details.Count, result.Data.Count());
    }

    [Theory, BitAutoData]
    public async Task Get_ReturnsGranteeDetailsResponseModel(
        SutProvider<EmergencyAccessController> sutProvider,
        User user,
        EmergencyAccessDetails details)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(user.Id);

        sutProvider.GetDependency<IEmergencyAccessService>()
            .GetAsync(details.Id, user.Id)
            .Returns(details);

        // Act
        var result = await sutProvider.Sut.Get(details.Id);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<EmergencyAccessGranteeDetailsResponseModel>(result);
    }

    [Theory, BitAutoData]
    public async Task Policies_ReturnsListResponseModel(
        SutProvider<EmergencyAccessController> sutProvider,
        User user,
        List<Policy> policies,
        Guid id)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(user);

        sutProvider.GetDependency<IEmergencyAccessService>()
            .GetPoliciesAsync(id, user)
            .Returns(policies);

        // Act
        var result = await sutProvider.Sut.Policies(id);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<ListResponseModel<PolicyResponseModel>>(result);
    }

    [Theory, BitAutoData]
    public async Task Policies_WhenGrantorIsNotOrgOwner_ReturnsNullDataAsync(
        SutProvider<EmergencyAccessController> sutProvider,
        User user,
        Guid id)
    {
        // Arrange
        // GetPoliciesAsync returns null when the grantor is not an org owner
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(user);

        sutProvider.GetDependency<IEmergencyAccessService>()
            .GetPoliciesAsync(id, user)
            .Returns((ICollection<Policy>)null);

        // Act
        var result = await sutProvider.Sut.Policies(id);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<ListResponseModel<PolicyResponseModel>>(result);
        Assert.Null(result.Data);
    }

    [Theory, BitAutoData]
    public async Task Put_WithNullEmergencyAccess_ThrowsNotFoundException(
        SutProvider<EmergencyAccessController> sutProvider,
        Guid id,
        Bit.Api.Auth.Models.Request.EmergencyAccessUpdateRequestModel model)
    {
        // Arrange
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(id)
            .Returns((EmergencyAccess)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Put(id, model));
    }

    [Theory, BitAutoData]
    public async Task Put_WithValidEmergencyAccess_CallsSaveAsync(
        SutProvider<EmergencyAccessController> sutProvider,
        User user,
        EmergencyAccess emergencyAccess,
        Bit.Api.Auth.Models.Request.EmergencyAccessUpdateRequestModel model)
    {
        // Arrange
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetByIdAsync(emergencyAccess.Id)
            .Returns(emergencyAccess);

        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(user);

        // Act
        await sutProvider.Sut.Put(emergencyAccess.Id, model);

        // Assert
        await sutProvider.GetDependency<IEmergencyAccessService>()
            .Received(1)
            .SaveAsync(Arg.Any<EmergencyAccess>(), user);
    }

    [Theory, BitAutoData]
    public async Task Invite_CallsInviteAsync(
        SutProvider<EmergencyAccessController> sutProvider,
        User user,
        Bit.Api.Auth.Models.Request.EmergencyAccessInviteRequestModel model)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(user);

        // Act
        await sutProvider.Sut.Invite(model);

        // Assert
        await sutProvider.GetDependency<IEmergencyAccessService>()
            .Received(1)
            .InviteAsync(user, model.Email, model.Type!.Value, model.WaitTimeDays);
    }

    [Theory, BitAutoData]
    public async Task Takeover_ReturnsTakeoverResponseModel(
        SutProvider<EmergencyAccessController> sutProvider,
        User granteeUser,
        User grantorUser,
        EmergencyAccess emergencyAccess,
        Guid id)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(granteeUser);

        sutProvider.GetDependency<IEmergencyAccessService>()
            .TakeoverAsync(id, granteeUser)
            .Returns((emergencyAccess, grantorUser));

        // Act
        var result = await sutProvider.Sut.Takeover(id);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<EmergencyAccessTakeoverResponseModel>(result);
    }

    [Theory, BitAutoData]
    public async Task ViewCiphers_ReturnsViewResponseModel(
        SutProvider<EmergencyAccessController> sutProvider,
        User user,
        EmergencyAccessViewData viewData,
        Guid id)
    {
        // Arrange
        viewData.Ciphers = [];

        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(user);

        sutProvider.GetDependency<IEmergencyAccessService>()
            .ViewAsync(id, user)
            .Returns(viewData);

        // Act
        var result = await sutProvider.Sut.ViewCiphers(id);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<EmergencyAccessViewResponseModel>(result);
    }

    [Theory, BitAutoData]
    public async Task GetAttachmentData_ReturnsAttachmentResponseModel(
        SutProvider<EmergencyAccessController> sutProvider,
        User user,
        Guid id,
        Guid cipherId,
        string attachmentId)
    {
        // Arrange
        // CipherAttachment.MetaData has a circular self-reference, so construct manually
        var attachmentData = new AttachmentResponseData
        {
            Id = attachmentId,
            Url = "https://example.com/attachment",
            Cipher = new Cipher(),
            Data = new CipherAttachment.MetaData { FileName = "file.txt", Key = "key", Size = 1024 },
        };

        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(user);

        sutProvider.GetDependency<IEmergencyAccessService>()
            .GetAttachmentDownloadAsync(id, cipherId, attachmentId, user)
            .Returns(attachmentData);

        // Act
        var result = await sutProvider.Sut.GetAttachmentData(id, cipherId, attachmentId);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AttachmentResponseModel>(result);
    }
}
