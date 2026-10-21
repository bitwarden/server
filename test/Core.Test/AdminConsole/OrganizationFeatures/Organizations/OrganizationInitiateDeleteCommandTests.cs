using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business.Tokenables;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Organizations;

[SutProviderCustomize]
public class OrganizationInitiateDeleteCommandTests
{
    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task InitiateDeleteAsync_ValidAdminUser_Success(OrganizationUserType organizationUserType,
        Organization organization, User orgAdmin, OrganizationUserOrganizationDetails orgAdminUser,
        string token, SutProvider<OrganizationInitiateDeleteCommand> sutProvider)
    {
        orgAdminUser.Type = organizationUserType;
        orgAdminUser.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(orgAdmin.Email)
            .Returns(orgAdmin);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetDetailsByUserAsync(orgAdmin.Id, organization.Id)
            .Returns(orgAdminUser);

        sutProvider.GetDependency<IDataProtectorTokenFactory<OrgDeleteTokenable>>()
            .Protect(Arg.Any<OrgDeleteTokenable>())
            .Returns(token);

        await sutProvider.Sut.InitiateDeleteAsync(organization, orgAdmin.Email);

        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendInitiateDeleteOrganzationEmailAsync(orgAdmin.Email, organization, token);
    }

    [Theory, BitAutoData]
    public async Task InitiateDeleteAsync_ValidAdminUser_IssuesTokenValidFor48Hours(
        Organization organization, User orgAdmin, OrganizationUserOrganizationDetails orgAdminUser,
        SutProvider<OrganizationInitiateDeleteCommand> sutProvider)
    {
        orgAdminUser.Type = OrganizationUserType.Owner;
        orgAdminUser.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(orgAdmin.Email)
            .Returns(orgAdmin);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetDetailsByUserAsync(orgAdmin.Id, organization.Id)
            .Returns(orgAdminUser);

        var expectedExpiry = DateTime.UtcNow.AddHours(48);

        await sutProvider.Sut.InitiateDeleteAsync(organization, orgAdmin.Email);

        sutProvider.GetDependency<IDataProtectorTokenFactory<OrgDeleteTokenable>>().Received(1)
            .Protect(Arg.Is<OrgDeleteTokenable>(t =>
                t.Id == organization.Id &&
                t.ExpirationDate >= expectedExpiry.AddMinutes(-1) &&
                t.ExpirationDate <= expectedExpiry.AddMinutes(1)));
    }

    [Theory, BitAutoData]
    public async Task InitiateDeleteAsync_UserNotFound_ThrowsBadRequest(
        Organization organization, string email, SutProvider<OrganizationInitiateDeleteCommand> sutProvider)
    {
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(email)
            .Returns((User)null);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InitiateDeleteAsync(organization, email));

        Assert.Equal(OrganizationInitiateDeleteCommand.OrganizationAdminNotFoundErrorMessage, exception.Message);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task InitiateDeleteAsync_UserNotOrgAdmin_ThrowsBadRequest(OrganizationUserType organizationUserType,
        Organization organization, User user, OrganizationUserOrganizationDetails orgUser,
        SutProvider<OrganizationInitiateDeleteCommand> sutProvider)
    {
        orgUser.Type = organizationUserType;
        orgUser.Status = OrganizationUserStatusType.Confirmed;

        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(user.Email)
            .Returns(user);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetDetailsByUserAsync(user.Id, organization.Id)
            .Returns(orgUser);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InitiateDeleteAsync(organization, user.Email));

        Assert.Equal(OrganizationInitiateDeleteCommand.OrganizationAdminNotFoundErrorMessage, exception.Message);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    [BitAutoData(OrganizationUserStatusType.Revoked)]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    public async Task InitiateDeleteAsync_UserNotConfirmed_ThrowsBadRequest(
        OrganizationUserStatusType organizationUserStatusType,
        Organization organization, User user, OrganizationUserOrganizationDetails orgUser,
        SutProvider<OrganizationInitiateDeleteCommand> sutProvider)
    {
        orgUser.Type = OrganizationUserType.Admin;
        orgUser.Status = organizationUserStatusType;

        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(user.Email)
            .Returns(user);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetDetailsByUserAsync(user.Id, organization.Id)
            .Returns(orgUser);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.InitiateDeleteAsync(organization, user.Email));

        Assert.Equal(OrganizationInitiateDeleteCommand.OrganizationAdminNotFoundErrorMessage, exception.Message);
    }
}
