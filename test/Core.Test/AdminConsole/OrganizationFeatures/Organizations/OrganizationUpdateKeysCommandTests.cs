using Bit.Core.AdminConsole.Entities;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Organizations;

[SutProviderCustomize]
public class OrganizationUpdateKeysCommandTests
{
    [Theory, BitAutoData]
    public async Task UpdateOrganizationKeysAsync_WithoutManageResetPasswordPermission_ThrowsUnauthorizedException(
        Guid orgId, string publicKey, string privateKey, SutProvider<OrganizationUpdateKeysCommand> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>()
            .ManageResetPassword(orgId)
            .Returns(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => sutProvider.Sut.UpdateOrganizationKeysAsync(orgId, publicKey, privateKey));
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationKeysAsync_WhenKeysAlreadyExist_ThrowsBadRequestException(
        Organization organization, string publicKey, string privateKey,
        SutProvider<OrganizationUpdateKeysCommand> sutProvider)
    {
        organization.PublicKey = "existingPublicKey";
        organization.PrivateKey = "existingPrivateKey";

        sutProvider.GetDependency<ICurrentContext>()
            .ManageResetPassword(organization.Id)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateOrganizationKeysAsync(organization.Id, publicKey, privateKey));

        Assert.Equal(OrganizationUpdateKeysCommand.OrganizationKeysAlreadyExistErrorMessage, exception.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateOrganizationKeysAsync_WhenKeysDoNotExist_UpdatesOrganization(
        Organization organization, string publicKey, string privateKey,
        SutProvider<OrganizationUpdateKeysCommand> sutProvider)
    {
        organization.PublicKey = null;
        organization.PrivateKey = null;

        sutProvider.GetDependency<ICurrentContext>()
            .ManageResetPassword(organization.Id)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var result = await sutProvider.Sut.UpdateOrganizationKeysAsync(organization.Id, publicKey, privateKey);

        Assert.Equal(publicKey, result.PublicKey);
        Assert.Equal(privateKey, result.PrivateKey);

        await sutProvider.GetDependency<IOrganizationService>()
            .Received(1)
            .UpdateAsync(organization);
    }
}
