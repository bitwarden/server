using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationLicenses;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Stripe;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationLicenses;

[SubscriptionInfoCustomize]
[OrganizationLicenseCustomize]
[SutProviderCustomize]
public class CloudGetOrganizationLicenseQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task GetLicenseAsync_InvalidInstallationId_Throws(SutProvider<CloudGetOrganizationLicenseQuery> sutProvider,
        Organization organization, Guid installationId, int version)
    {
        sutProvider.GetDependency<IInstallationRepository>().GetByIdAsync(installationId).ReturnsNull();
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.GetLicenseAsync(organization, installationId, version));
        Assert.Contains("Invalid installation id", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task GetLicenseAsync_DisabledOrganization_Throws(SutProvider<CloudGetOrganizationLicenseQuery> sutProvider,
        Organization organization, Guid installationId, Installation installation)
    {
        installation.Enabled = false;
        sutProvider.GetDependency<IInstallationRepository>().GetByIdAsync(installationId).Returns(installation);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.GetLicenseAsync(organization, installationId));
        Assert.Contains("Invalid installation id", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task GetLicenseAsync_CreatesAndReturns(SutProvider<CloudGetOrganizationLicenseQuery> sutProvider,
        Organization organization, Guid installationId, Installation installation, SubscriptionInfo subInfo,
        byte[] licenseSignature)
    {
        installation.Enabled = true;
        sutProvider.GetDependency<IInstallationRepository>().GetByIdAsync(installationId).Returns(installation);
        sutProvider.GetDependency<IPaymentService>().GetSubscriptionAsync(organization).Returns(subInfo);
        sutProvider.GetDependency<ILicensingService>().SignLicense(Arg.Any<ILicense>()).Returns(licenseSignature);

        var result = await sutProvider.Sut.GetLicenseAsync(organization, installationId);

        Assert.Equal(LicenseType.Organization, result.LicenseType);
        Assert.Equal(organization.Id, result.Id);
        Assert.Equal(installationId, result.InstallationId);
        Assert.Equal(licenseSignature, result.SignatureBytes);
    }

    [Theory]
    [BitAutoData]
    public async Task GetLicenseAsync_MSPManagedOrganization_UsesProviderSubscription(SutProvider<CloudGetOrganizationLicenseQuery> sutProvider,
        Organization organization, Guid installationId, Installation installation, SubscriptionInfo subInfo,
        byte[] licenseSignature, Provider provider)
    {
        organization.Status = OrganizationStatusType.Managed;
        organization.ExpirationDate = null;

        subInfo.Subscription = new SubscriptionInfo.BillingSubscription(new Subscription
        {
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
        });

        installation.Enabled = true;
        sutProvider.GetDependency<IInstallationRepository>().GetByIdAsync(installationId).Returns(installation);
        sutProvider.GetDependency<IProviderRepository>().GetByOrganizationIdAsync(organization.Id).Returns(provider);
        sutProvider.GetDependency<IPaymentService>().GetSubscriptionAsync(provider).Returns(subInfo);
        sutProvider.GetDependency<ILicensingService>().SignLicense(Arg.Any<ILicense>()).Returns(licenseSignature);

        var result = await sutProvider.Sut.GetLicenseAsync(organization, installationId);

        Assert.Equal(LicenseType.Organization, result.LicenseType);
        Assert.Equal(organization.Id, result.Id);
        Assert.Equal(installationId, result.InstallationId);
        Assert.Equal(licenseSignature, result.SignatureBytes);
        Assert.Equal(DateTime.UtcNow.AddYears(1).Date, result.Expires!.Value.Date);
    }
}
