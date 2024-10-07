using AutoFixture;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Licenses;
using Bit.Core.Billing.Licenses.OrganizationLicenses;
using Bit.Core.Billing.Licenses.UserLicenses;
using Bit.Core.Entities;
using Bit.Core.Settings;
using Bit.Core.Test.Billing.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Billing.Licenses;

[SutProviderCustomize]
public class ValidateEntityAgainstLicenseCommandTests
{
    #region OrganizationLicense tests

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseIsValid_ReturnsSuccess(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.True(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseNotYetIssued_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        license.Issued = DateTime.UtcNow.AddDays(1);

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseExpired_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        license.Expires = DateTime.UtcNow.AddDays(-1);

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidVersion_ThrowsNotSupportedException(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        license.Version = OrganizationLicense.CurrentLicenseFileVersion + 2;

        Assert.Throws<NotSupportedException>(() => sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        ));
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidInstallationId_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        sutProvider.GetDependency<IGlobalSettings>().Installation.Id.Returns(Guid.NewGuid());

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidLicenseKey_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        license.LicenseKey = "invalid";

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithEnabledMismatch_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        organization.Enabled = !license.Enabled;

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidPlanType_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        license.PlanType = PlanType.EnterpriseAnnually;
        organization.PlanType = PlanType.TeamsMonthly;

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidSeats_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        organization.Seats = license.Seats + 1;

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidMaxCollections_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        license.MaxCollections = 1;
        organization.MaxCollections = 2;

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidUseGroups_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        organization.UseGroups = !license.UseGroups;

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidUseDirectory_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        organization.UseDirectory = !license.UseDirectory;

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidUseTotp_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        organization.UseTotp = !license.UseTotp;

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidSelfHost_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        organization.SelfHost = !license.SelfHost;

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidName_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        organization.Name = "invalid";

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidUsersGetPremium_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        organization.UsersGetPremium = !license.UsersGetPremium;

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidUseEvents_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        organization.UseEvents = !license.UseEvents;

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidUse2fa_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        organization.Use2fa = !license.Use2fa;

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidUseApi_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        organization.UseApi = !license.UseApi;

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidUsePolicies_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        organization.UsePolicies = !license.UsePolicies;

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidUseSso_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        organization.UseSso = !license.UseSso;

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidUseResetPassword_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        organization.UseResetPassword = !license.UseResetPassword;

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidUseKeyConnector_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        organization.UseKeyConnector = !license.UseKeyConnector;

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidUseScim_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        organization.UseScim = !license.UseScim;

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidUseCustomPermissions_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        organization.UseCustomPermissions = !license.UseCustomPermissions;

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidUseSecretsManager_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        organization.UseSecretsManager = !license.UseSecretsManager;

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidUsePasswordManager_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        organization.UsePasswordManager = !license.UsePasswordManager;

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidSmSeats_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        organization.SmSeats = license.SmSeats + 1;

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [OrganizationLicenseCustomize]
    public void Handle_OrganizationLicenseWithInvalidSmServiceAccounts_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        OrganizationLicense license)
    {
        var organization = ArrangeValidOrganizationValues(license);
        organization.SmServiceAccounts = license.SmServiceAccounts + 1;

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, Organization = organization }
        );

        Assert.False(result.Succeeded);
    }

    private static Organization ArrangeValidOrganizationValues(OrganizationLicense license) => new Fixture()
        .Build<Organization>()
        .With(o => o.LicenseKey, license.LicenseKey)
        .With(o => o.Enabled, license.Enabled)
        .With(o => o.PlanType, license.PlanType)
        .With(o => o.Seats, license.Seats)
        .With(o => o.MaxCollections, license.MaxCollections)
        .With(o => o.UseGroups, license.UseGroups)
        .With(o => o.UseDirectory, license.UseDirectory)
        .With(o => o.UseTotp, license.UseTotp)
        .With(o => o.SelfHost, license.SelfHost)
        .With(o => o.Name, license.Name)
        .With(o => o.UsersGetPremium, license.UsersGetPremium)
        .With(o => o.UseEvents, license.UseEvents)
        .With(o => o.Use2fa, license.Use2fa)
        .With(o => o.UseApi, license.UseApi)
        .With(o => o.UsePolicies, license.UsePolicies)
        .With(o => o.UseSso, license.UseSso)
        .With(o => o.UseResetPassword, license.UseResetPassword)
        .With(o => o.UseKeyConnector, license.UseKeyConnector)
        .With(o => o.UseScim, license.UseScim)
        .With(o => o.UseCustomPermissions, license.UseCustomPermissions)
        .With(o => o.UseSecretsManager, license.UseSecretsManager)
        .With(o => o.UsePasswordManager, license.UsePasswordManager)
        .With(o => o.SmSeats, license.SmSeats)
        .With(o => o.SmServiceAccounts, license.SmServiceAccounts)
        .Create();

    #endregion

    #region UserLicense tests

    [Theory]
    [BitAutoData]
    [UserLicenseCustomize]
    public void Handle_UserLicenseWithValidData_ReturnsSuccess(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        UserLicense license)
    {
        var user = ArrangeValidUserValues(license);

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, User = user }
        );

        Assert.True(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [UserLicenseCustomize]
    public void Handle_UserLicenseNotYetIssued_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        UserLicense license)
    {
        var user = ArrangeValidUserValues(license);
        license.Issued = DateTime.UtcNow.AddDays(1);

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, User = user }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [UserLicenseCustomize]
    public void Handle_UserLicenseExpired_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        UserLicense license)
    {
        var user = ArrangeValidUserValues(license);
        license.Expires = DateTime.UtcNow.AddDays(-1);

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, User = user }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [UserLicenseCustomize]
    public void Handle_UserLicenseWithInvalidVersion_ThrowsNotSupportedException(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        UserLicense license)
    {
        var user = ArrangeValidUserValues(license);
        license.Version = UserLicense.CurrentLicenseFileVersion + 2;

        Assert.Throws<NotSupportedException>(() => sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, User = user }
        ));
    }

    [Theory]
    [BitAutoData]
    [UserLicenseCustomize]
    public void Handle_UserLicenseWithInvalidLicenseKey_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        UserLicense license)
    {
        var user = ArrangeValidUserValues(license);
        license.LicenseKey = "invalid";

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, User = user }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [UserLicenseCustomize]
    public void Handle_UserLicenseWithInvalidPremium_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        UserLicense license)
    {
        var user = ArrangeValidUserValues(license);
        user.Premium = !license.Premium;

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, User = user }
        );

        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    [UserLicenseCustomize]
    public void Handle_UserLicenseWithInvalidEmail_ReturnsFailure(
        SutProvider<ValidateEntityAgainstLicenseCommandHandler> sutProvider,
        UserLicense license)
    {
        var user = ArrangeValidUserValues(license);
        user.Email = "invalid";

        var result = sutProvider.Sut.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, User = user }
        );

        Assert.False(result.Succeeded);
    }

    private static User ArrangeValidUserValues(UserLicense license) => new Fixture()
        .Build<User>()
        .With(u => u.LicenseKey, license.LicenseKey)
        .With(u => u.Premium, license.Premium)
        .With(u => u.Email, license.Email)
        .Create();

    #endregion
}
