using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.OrganizationFeatures.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.DataProtection;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationUsers;


// Note: test names follow MethodName_StateUnderTest_ExpectedBehavior pattern.
[SutProviderCustomize]
public class AcceptOrgUserCommandTests
{
    private readonly IUserService _userService;

    public AcceptOrgUserCommandTests()
    {
        _userService = Substitute.For<IUserService>();
    }

    [Theory]
    [EphemeralDataProtectionAutoData]
    public async Task AcceptOrgUserByToken_OldToken_Success(SutProvider<AcceptOrgUserCommand> sutProvider,
        User user, Guid organizationUserId, OrganizationUser orgUser)
    {
        // Arrange
        sutProvider.GetDependency<IGlobalSettings>().OrganizationInviteExpirationHours.Returns(24);

        user.EmailVerified = false;

        orgUser.Status = OrganizationUserStatusType.Invited;
        orgUser.Email = user.Email;


        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns(orgUser);

        var oldToken = CreateOldToken(sutProvider, orgUser);

        // Act
        var result = await sutProvider.Sut.AcceptOrgUserByTokenAsync(organizationUserId, user, oldToken, _userService);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(OrganizationUserStatusType.Accepted, result.Status);
        Assert.Equal(orgUser.Id, result.Id);
        Assert.Null(result.Email);
        Assert.Equal(user.Id, result.UserId);
        Assert.True(user.EmailVerified);  // Verifying the EmailVerified flag.
    }



    private string CreateOldToken(SutProvider<AcceptOrgUserCommand> sutProvider,
        OrganizationUser organizationUser)
    {

        var dataProtector = sutProvider.GetDependency<IDataProtectionProvider>()
            .CreateProtector("OrganizationServiceDataProtector");

        // Token matching the format used in OrganizationService.InviteUserAsync
        var oldToken = dataProtector.Protect(
            $"OrganizationUserInvite {organizationUser.Id} {organizationUser.Email} {CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow)}");

        return oldToken;
    }
}
