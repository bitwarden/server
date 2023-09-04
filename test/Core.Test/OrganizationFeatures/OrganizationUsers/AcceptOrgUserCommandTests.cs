using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.OrganizationFeatures.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.DataProtection;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class AcceptOrgUserCommandTests
{
    private readonly IUserService _userService;

    public AcceptOrgUserCommandTests()
    {
        _userService = Substitute.For<IUserService>();
    }

    [Theory]
    [BitAutoData]
    public async Task AcceptOrgUser_ByOrgUserId_Success(SutProvider<AcceptOrgUserCommand> sutProvider,
        User user, Guid organizationUserId, string token, OrganizationUser orgUser)
    {
        // Arrange
        orgUser.Status = OrganizationUserStatusType.Invited;
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns(orgUser);

        // TODO: can't mock static methods
        // See CoreHelpersTests to figure out how to properly mock valid token
        CoreHelpers.UserInviteTokenIsValid(Arg.Any<IDataProtector>(), token, user.Email, orgUser.Id,
                Arg.Any<IGlobalSettings>())
            .Returns(true);

        // Act
        var result = await sutProvider.Sut.AcceptOrgUserAsync(organizationUserId, user, token, _userService);


        // Assert
        // Assert.Equal(IdentityResult.Success, result);
    }
}
