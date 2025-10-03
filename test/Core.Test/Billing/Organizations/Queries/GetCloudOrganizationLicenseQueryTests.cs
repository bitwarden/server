using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Organizations.Queries;
using Bit.Core.Billing.Services;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Platform.Installations;
using Bit.Core.Test.AutoFixture;
using Bit.Core.Test.Billing.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.Queries;

[SubscriptionInfoCustomize]
[OrganizationLicenseCustomize]
[SutProviderCustomize]
public class GetCloudOrganizationLicenseQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task GetLicenseAsync_InvalidInstallationId_Throws(SutProvider<GetCloudOrganizationLicenseQuery> sutProvider,
        Organization organization, Guid installationId, int version)
    {
        sutProvider.GetDependency<IInstallationRepository>().GetByIdAsync(installationId).ReturnsNull();
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.GetLicenseAsync(organization, installationId, version));
        Assert.Contains("Invalid installation id", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task GetLicenseAsync_DisabledOrganization_Throws(SutProvider<GetCloudOrganizationLicenseQuery> sutProvider,
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
    public async Task GetLicenseAsync_CreatesAndReturns(SutProvider<GetCloudOrganizationLicenseQuery> sutProvider,
        Organization organization, Guid installationId, Installation installation, SubscriptionInfo subInfo,
        byte[] licenseSignature)
    {
        installation.Enabled = true;
        sutProvider.GetDependency<IInstallationRepository>().GetByIdAsync(installationId).Returns(installation);
        sutProvider.GetDependency<IStripePaymentService>().GetSubscriptionAsync(organization).Returns(subInfo);
        sutProvider.GetDependency<ILicensingService>().SignLicense(Arg.Any<ILicense>()).Returns(licenseSignature);

        var result = await sutProvider.Sut.GetLicenseAsync(organization, installationId);

        Assert.Equal(LicenseType.Organization, result.LicenseType);
        Assert.Equal(organization.Id, result.Id);
        Assert.Equal(installationId, result.InstallationId);
        Assert.Equal(licenseSignature, result.SignatureBytes);
        Assert.Equal(string.Empty, result.Token);
    }

    [Theory]
    [BitAutoData]
    public async Task GetLicenseAsync_WhenFeatureFlagEnabled_CreatesToken(SutProvider<GetCloudOrganizationLicenseQuery> sutProvider,
        Organization organization, Guid installationId, Installation installation, SubscriptionInfo subInfo,
        byte[] licenseSignature, string token)
    {
        installation.Enabled = true;
        sutProvider.GetDependency<IInstallationRepository>().GetByIdAsync(installationId).Returns(installation);
        sutProvider.GetDependency<IStripePaymentService>().GetSubscriptionAsync(organization).Returns(subInfo);
        sutProvider.GetDependency<ILicensingService>().SignLicense(Arg.Any<ILicense>()).Returns(licenseSignature);
        sutProvider.GetDependency<ILicensingService>()
            .CreateOrganizationTokenAsync(organization, installationId, subInfo)
            .Returns(token);

        var result = await sutProvider.Sut.GetLicenseAsync(organization, installationId);

        Assert.Equal(token, result.Token);
    }

    [Theory]
    [BitAutoData]
    public async Task GetLicenseAsync_MSPManagedOrganization_UsesProviderSubscription(SutProvider<GetCloudOrganizationLicenseQuery> sutProvider,
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
        sutProvider.GetDependency<IStripePaymentService>().GetSubscriptionAsync(provider).Returns(subInfo);
        sutProvider.GetDependency<ILicensingService>().SignLicense(Arg.Any<ILicense>()).Returns(licenseSignature);

        var result = await sutProvider.Sut.GetLicenseAsync(organization, installationId);

        Assert.Equal(LicenseType.Organization, result.LicenseType);
        Assert.Equal(organization.Id, result.Id);
        Assert.Equal(installationId, result.InstallationId);
        Assert.Equal(licenseSignature, result.SignatureBytes);
        Assert.Equal(DateTime.UtcNow.AddYears(1).Date, result.Expires!.Value.Date);
    }
}
