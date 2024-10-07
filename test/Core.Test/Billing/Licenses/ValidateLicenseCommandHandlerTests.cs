using AutoFixture;
using Bit.Core.Billing.Licenses;
using Bit.Core.Billing.Licenses.OrganizationLicenses;
using Bit.Core.Billing.Licenses.UserLicenses;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Test.Billing.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Billing.Licenses;

[SutProviderCustomize]
public class ValidateLicenseCommandHandlerTests
{
    private readonly SutProvider<ValidateLicenseCommandHandler> _sutProvider;

    public ValidateLicenseCommandHandlerTests()
    {
        var fixture = new Fixture().WithAutoNSubstitutions();
        _sutProvider = new SutProvider<ValidateLicenseCommandHandler>(fixture);

        var licensingService = Substitute.For<ILicensingService>();

        licensingService.VerifyLicenseSignature(Arg.Any<ILicense>()).Returns(true);

        _sutProvider.SetDependency(licensingService);
        _sutProvider.Create();
    }

    #region OrganizationLicense tests

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithValidData_ReturnsSuccess(
        OrganizationLicense license)
    {
        var result = _sutProvider.Sut.Handle(
            new ValidateLicenseCommand { License = license }
        );

        Assert.True(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithDisabledLicense_ReturnsFailure(
        OrganizationLicense license)
    {
        license.Enabled = false;

        var result = _sutProvider.Sut.Handle(
            new ValidateLicenseCommand { License = license }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithNotYetIssuedLicense_ReturnsFailure(
        OrganizationLicense license)
    {
        license.Issued = DateTime.UtcNow.AddDays(1);

        var result = _sutProvider.Sut.Handle(
            new ValidateLicenseCommand { License = license }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithExpiredLicense_ReturnsFailure(
        OrganizationLicense license)
    {
        license.Expires = DateTime.UtcNow.AddDays(-1);

        var result = _sutProvider.Sut.Handle(
            new ValidateLicenseCommand { License = license }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidVersion_ReturnsFailure(
        OrganizationLicense license)
    {
        license.Version = OrganizationLicense.CurrentLicenseFileVersion + 2;

        var result = _sutProvider.Sut.Handle(
            new ValidateLicenseCommand { License = license }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidInstallationId_ReturnsFailure(
        OrganizationLicense license)
    {
        _sutProvider.GetDependency<IGlobalSettings>().Installation.Id.Returns(Guid.NewGuid());

        var result = _sutProvider.Sut.Handle(
            new ValidateLicenseCommand { License = license }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithLicenseNotMarkedSelfHost_ReturnsFailure(
        OrganizationLicense license)
    {
        license.SelfHost = false;

        var result = _sutProvider.Sut.Handle(
            new ValidateLicenseCommand { License = license }
        );

        Assert.False(result.Succeeded);
    }

    // Skipping LicenseType as it's tested in the base case

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidLicenseSignature_ReturnsFailure(
        OrganizationLicense license)
    {
        _sutProvider.GetDependency<ILicensingService>()
            .VerifyLicenseSignature(Arg.Any<ILicense>())
            .Returns(false);

        var result = _sutProvider.Sut.Handle(
            new ValidateLicenseCommand { License = license }
        );

        Assert.False(result.Succeeded);
    }

    #endregion

    #region UserLicense tests

    [Theory]
    [BitAutoData]
    [UserLicenseCustomize]
    public void Handle_UserLicenseWithValidData_ReturnsSuccess(
        UserLicense license)
    {
        var user = ArrangeValidUserValues(license);

        var result = _sutProvider.Sut.Handle(
            new ValidateLicenseCommand { License = license, User = user }
        );

        Assert.True(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [UserLicenseCustomize]
    public void Handle_WithUserLicenseNotIssuedYet_ReturnsFailure(
        UserLicense license)
    {
        var user = ArrangeValidUserValues(license);
        license.Issued = DateTime.UtcNow.AddDays(1);

        var result = _sutProvider.Sut.Handle(
            new ValidateLicenseCommand { License = license, User = user }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [UserLicenseCustomize]
    public void Handle_WithUserLicenseExpired_ReturnsFailure(
        UserLicense license)
    {
        var user = ArrangeValidUserValues(license);
        license.Expires = DateTime.UtcNow.AddDays(-1);

        var result = _sutProvider.Sut.Handle(
            new ValidateLicenseCommand { License = license, User = user }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [UserLicenseCustomize]
    public void Handle_WithUserLicenseInvalidVersion_ReturnsFailure(
        UserLicense license)
    {
        var user = ArrangeValidUserValues(license);
        license.Version = UserLicense.CurrentLicenseFileVersion + 2;

        var result = _sutProvider.Sut.Handle(
            new ValidateLicenseCommand { License = license, User = user }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [UserLicenseCustomize]
    public void Handle_UserWithUnverifiedEmail_ReturnsFailure(
        UserLicense license)
    {
        var user = ArrangeValidUserValues(license);
        user.EmailVerified = false;

        var result = _sutProvider.Sut.Handle(
            new ValidateLicenseCommand { License = license, User = user }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [UserLicenseCustomize]
    public void Handle_UserWithInvalidEmail_ReturnsFailure(
        UserLicense license)
    {
        var user = ArrangeValidUserValues(license);
        user.Email = "invalid";

        var result = _sutProvider.Sut.Handle(
            new ValidateLicenseCommand { License = license, User = user }
        );

        Assert.False(result.Succeeded);
    }

    // Skipping LicenseType as it's tested in the base case

    [Theory]
    [BitAutoData]
    [UserLicenseCustomize]
    public void Handle_UserLicenseWithInvalidLicenseSignature_ReturnsFailure(
        UserLicense license)
    {
        var user = ArrangeValidUserValues(license);

        _sutProvider.GetDependency<ILicensingService>()
            .VerifyLicenseSignature(Arg.Any<ILicense>())
            .Returns(false);

        var result = _sutProvider.Sut.Handle(
            new ValidateLicenseCommand { License = license, User = user }
        );

        Assert.False(result.Succeeded);
    }

    private static User ArrangeValidUserValues(UserLicense license) => new Fixture()
        .Build<User>()
        .With(u => u.Email, license.Email)
        .Create();

    #endregion
}
