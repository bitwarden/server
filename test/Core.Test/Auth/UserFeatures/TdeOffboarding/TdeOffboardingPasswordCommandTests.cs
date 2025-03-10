using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.UserMasterPassword;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.UserMasterPassword;

[SutProviderCustomize]
public class TdeOffboardingPasswordTests
{
    [Theory]
    [BitAutoData]
    public async Task TdeOffboardingPasswordCommand_Success(SutProvider<TdeOffboardingPasswordCommand> sutProvider,
        User user, string masterPassword, string key, string hint, OrganizationUserOrganizationDetails orgUserDetails, SsoUser ssoUser)
    {
        // Arrange
        user.MasterPassword = null;

        sutProvider.GetDependency<IUserService>()
            .UpdatePasswordHash(Arg.Any<User>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);

        orgUserDetails.UseSso = true;
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByUserAsync(user.Id)
            .Returns(new List<OrganizationUserOrganizationDetails> { orgUserDetails });

        sutProvider.GetDependency<ISsoUserRepository>()
            .GetByUserIdOrganizationIdAsync(orgUserDetails.OrganizationId, user.Id)
            .Returns(ssoUser);

        var ssoConfig = new SsoConfig();
        var ssoConfigData = ssoConfig.GetData();
        ssoConfigData.MemberDecryptionType = MemberDecryptionType.MasterPassword;
        ssoConfig.SetData(ssoConfigData);
        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(orgUserDetails.OrganizationId)
            .Returns(ssoConfig);

        // Act
        var result = await sutProvider.Sut.UpdateTdeOffboardingPasswordAsync(user, masterPassword, key, hint);

        // Assert
        Assert.Equal(IdentityResult.Success, result);
    }

    [Theory]
    [BitAutoData]
    public async Task TdeOffboardingPasswordCommand_RejectWithTdeEnabled(SutProvider<TdeOffboardingPasswordCommand> sutProvider,
        User user, string masterPassword, string key, string hint, OrganizationUserOrganizationDetails orgUserDetails, SsoUser ssoUser)
    {
        // Arrange
        user.MasterPassword = null;

        sutProvider.GetDependency<IUserService>()
            .UpdatePasswordHash(Arg.Any<User>(), Arg.Any<string>(), true, false)
            .Returns(IdentityResult.Success);

        orgUserDetails.UseSso = true;
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByUserAsync(user.Id)
            .Returns(new List<OrganizationUserOrganizationDetails> { orgUserDetails });

        sutProvider.GetDependency<ISsoUserRepository>()
            .GetByUserIdOrganizationIdAsync(orgUserDetails.OrganizationId, user.Id)
            .Returns(ssoUser);

        var ssoConfig = new SsoConfig();
        var ssoConfigData = ssoConfig.GetData();
        ssoConfigData.MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption;
        ssoConfig.SetData(ssoConfigData);
        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(orgUserDetails.OrganizationId)
            .Returns(ssoConfig);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateTdeOffboardingPasswordAsync(user, masterPassword, key, hint));
    }


    [Theory]
    [BitAutoData]
    public async Task TdeOffboardingPasswordCommand_RejectWithMasterPassword(SutProvider<TdeOffboardingPasswordCommand> sutProvider,
        User user, string masterPassword, string key, string hint)
    {
        // the user already has a master password, so the off-boarding request should fail, since off-boarding only applies to passwordless TDE users
        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateTdeOffboardingPasswordAsync(user, masterPassword, key, hint));
    }

}
