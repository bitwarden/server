using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class InviteLinkFreeOrganizationAdminValidatorTests
{
    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task ValidateAsync_FreeOrganizationAdminLimitReached_ReturnsOnlyOneFreeOrganizationAdminAllowed(
        OrganizationUserType role,
        Organization organization, User user, OrganizationUser existingOrganizationUser,
        SutProvider<InviteLinkFreeOrganizationAdminValidator> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.Free;
        existingOrganizationUser.Type = role;
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(user.Id)
            .Returns(1);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user, existingOrganizationUser));

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OnlyOneFreeOrganizationAdminAllowed>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_FreeOrganizationAdminNotAtLimit_ReturnsValidRequest(
        Organization organization, User user, OrganizationUser existingOrganizationUser,
        SutProvider<InviteLinkFreeOrganizationAdminValidator> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.Free;
        existingOrganizationUser.Type = OrganizationUserType.Admin;
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(user.Id)
            .Returns(0);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user, existingOrganizationUser));

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_NonAdminOnFreeOrganization_SkipsAdminLimitCheck(
        Organization organization, User user, OrganizationUser existingOrganizationUser,
        SutProvider<InviteLinkFreeOrganizationAdminValidator> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.Free;
        existingOrganizationUser.Type = OrganizationUserType.User;

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, user, existingOrganizationUser));

        // Assert
        Assert.True(result.IsValid);
        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .GetCountByFreeOrganizationAdminUserAsync(Arg.Any<Guid>());
    }

    private static InviteLinkFreeOrganizationAdminValidationRequest BuildRequest(
        Organization organization, User user, OrganizationUser? existingOrganizationUser) =>
        new()
        {
            Organization = organization,
            User = user,
            ExistingOrganizationUser = existingOrganizationUser,
        };
}
