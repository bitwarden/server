using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.Entities;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class InviteLinkEmailVerificationValidationServiceTests
{
    [Theory]
    [BitAutoData(false, true)]
    [BitAutoData(false, false)]
    [BitAutoData(true, true)]
    [BitAutoData(true, false)]
    public void ValidateEmailVerified_WithVerifiedEmail_ReturnsNull(
        bool selfHosted,
        bool enableOpenInviteLinkEmailVerification,
        SutProvider<InviteLinkEmailVerificationValidationService> sutProvider,
        User user)
    {
        // Arrange
        user.EmailVerified = true;
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted = selfHosted;
        sutProvider.GetDependency<IGlobalSettings>().EnableOpenInviteLinkEmailVerification = enableOpenInviteLinkEmailVerification;

        // Act
        var error = sutProvider.Sut.ValidateEmailVerified(user);

        // Assert
        Assert.Null(error);
    }

    // Cloud always requires email verification, even if the setting is disabled.
    [Theory]
    [BitAutoData(false, true)]
    [BitAutoData(false, false)]
    [BitAutoData(true, true)]
    public void ValidateEmailVerified_WithUnverifiedEmailAndVerificationRequired_ReturnsEmailNotVerified(
        bool selfHosted,
        bool enableOpenInviteLinkEmailVerification,
        SutProvider<InviteLinkEmailVerificationValidationService> sutProvider,
        User user)
    {
        // Arrange
        user.EmailVerified = false;
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted = selfHosted;
        sutProvider.GetDependency<IGlobalSettings>().EnableOpenInviteLinkEmailVerification = enableOpenInviteLinkEmailVerification;

        // Act
        var error = sutProvider.Sut.ValidateEmailVerified(user);

        // Assert
        Assert.IsType<EmailNotVerified>(error);
    }

    [Theory, BitAutoData]
    public void ValidateEmailVerified_WithUnverifiedEmailSelfHostedAndSettingDisabled_ReturnsNull(
        SutProvider<InviteLinkEmailVerificationValidationService> sutProvider,
        User user)
    {
        // Arrange
        user.EmailVerified = false;
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted = true;
        sutProvider.GetDependency<IGlobalSettings>().EnableOpenInviteLinkEmailVerification = false;

        // Act
        var error = sutProvider.Sut.ValidateEmailVerified(user);

        // Assert
        Assert.Null(error);
    }
}
