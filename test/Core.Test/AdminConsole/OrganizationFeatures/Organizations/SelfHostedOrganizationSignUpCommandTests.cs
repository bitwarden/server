using System.Security.Claims;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Organizations;

[SutProviderCustomize]
public class SelfHostedOrganizationSignUpCommandTests
{
    [Theory, BitAutoData]
    public async Task SignUpAsync_WithValidRequest_CreatesOrganizationSuccessfully(
        User owner, string ownerKey, string collectionName, string publicKey,
        string privateKey, List<Device> devices,
        SutProvider<SelfHostedOrganizationSignUpCommand> sutProvider)
    {
        // Arrange
        var globalSettings = sutProvider.GetDependency<IGlobalSettings>();
        var license = CreateValidOrganizationLicense(globalSettings);

        SetupCommonMocks(sutProvider, owner);
        SetupLicenseValidation(sutProvider, license);

        sutProvider.GetDependency<IDeviceRepository>()
            .GetManyByUserIdAsync(owner.Id)
            .Returns(devices);

        // Act
        var result = await sutProvider.Sut.SignUpAsync(license, owner, ownerKey, collectionName, publicKey, privateKey);

        // Assert
        Assert.NotNull(result.organization);
        Assert.NotNull(result.organizationUser);

        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .CreateAsync(result.organization);

        await sutProvider.GetDependency<IOrganizationApiKeyRepository>()
            .Received(1)
            .CreateAsync(Arg.Is<OrganizationApiKey>(key =>
                key.OrganizationId == result.organization.Id &&
                key.Type == OrganizationApiKeyType.Default &&
                !string.IsNullOrEmpty(key.ApiKey)));

        await sutProvider.GetDependency<IApplicationCacheService>()
            .Received(1)
            .UpsertOrganizationAbilityAsync(result.organization);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .CreateAsync(Arg.Is<OrganizationUser>(user =>
                user.OrganizationId == result.organization.Id &&
                user.UserId == owner.Id &&
                user.Key == ownerKey &&
                user.Type == OrganizationUserType.Owner &&
                user.Status == OrganizationUserStatusType.Confirmed));

        await sutProvider.GetDependency<ICollectionRepository>()
            .Received(1)
            .CreateAsync(
                Arg.Is<Collection>(c => c.Name == collectionName && c.OrganizationId == result.organization.Id),
                Arg.Is<IEnumerable<CollectionAccessSelection>>(groups => groups == null),
                Arg.Is<IEnumerable<CollectionAccessSelection>>(access =>
                    access.Any(a => a.Id == result.organizationUser.Id && a.Manage && !a.ReadOnly && !a.HidePasswords)));

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncOrgKeysAsync(owner.Id);
    }

    [Theory, BitAutoData]
    public async Task SignUpAsync_WithPremiumLicense_ThrowsBadRequestException(
        User owner, string ownerKey, string collectionName,
        string publicKey, string privateKey,
        SutProvider<SelfHostedOrganizationSignUpCommand> sutProvider)
    {
        // Arrange
        var globalSettings = sutProvider.GetDependency<IGlobalSettings>();
        var license = CreateValidOrganizationLicense(globalSettings, LicenseType.User);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SignUpAsync(license, owner, ownerKey, collectionName, publicKey, privateKey));

        Assert.Contains("Premium licenses cannot be applied to an organization", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task SignUpAsync_WithInvalidLicense_ThrowsBadRequestException(
        User owner, string ownerKey, string collectionName,
        string publicKey, string privateKey,
        SutProvider<SelfHostedOrganizationSignUpCommand> sutProvider)
    {
        // Arrange
        var globalSettings = sutProvider.GetDependency<IGlobalSettings>();
        var license = CreateValidOrganizationLicense(globalSettings);
        license.CanUse(globalSettings, sutProvider.GetDependency<ILicensingService>(), null, out _)
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SignUpAsync(license, owner, ownerKey, collectionName, publicKey, privateKey));
    }

    [Theory, BitAutoData]
    public async Task SignUpAsync_WithLicenseAlreadyInUse_ThrowsBadRequestException(
        User owner, string ownerKey, string collectionName,
        string publicKey, string privateKey, Organization existingOrganization,
        SutProvider<SelfHostedOrganizationSignUpCommand> sutProvider)
    {
        // Arrange
        var globalSettings = sutProvider.GetDependency<IGlobalSettings>();
        var license = CreateValidOrganizationLicense(globalSettings);
        existingOrganization.LicenseKey = license.LicenseKey;

        SetupCommonMocks(sutProvider, owner);
        SetupLicenseValidation(sutProvider, license);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetManyByEnabledAsync()
            .Returns(new List<Organization> { existingOrganization });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SignUpAsync(license, owner, ownerKey, collectionName, publicKey, privateKey));

        Assert.Contains("License is already in use", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task SignUpAsync_WithSingleOrgPolicy_ThrowsBadRequestException(
        User owner, string ownerKey, string collectionName,
        string publicKey, string privateKey,
        SutProvider<SelfHostedOrganizationSignUpCommand> sutProvider)
    {
        // Arrange
        var globalSettings = sutProvider.GetDependency<IGlobalSettings>();
        var license = CreateValidOrganizationLicense(globalSettings);

        SetupCommonMocks(sutProvider, owner);
        SetupLicenseValidation(sutProvider, license);

        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(owner.Id, PolicyType.SingleOrg)
            .Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SignUpAsync(license, owner, ownerKey, collectionName, publicKey, privateKey));

        Assert.Contains("You may not create an organization", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task SignUpAsync_WithClaimsPrincipal_UsesClaimsPrincipalToCreateOrganization(
        User owner, string ownerKey, string collectionName,
        string publicKey, string privateKey,ClaimsPrincipal claimsPrincipal,
        SutProvider<SelfHostedOrganizationSignUpCommand> sutProvider)
    {
        // Arrange
        var globalSettings = sutProvider.GetDependency<IGlobalSettings>();
        var license = CreateValidOrganizationLicense(globalSettings);

        SetupCommonMocks(sutProvider, owner);
        SetupLicenseValidation(sutProvider, license);

        sutProvider.GetDependency<ILicensingService>()
            .GetClaimsPrincipalFromLicense(license)
            .Returns(claimsPrincipal);

        sutProvider.GetDependency<IDeviceRepository>()
            .GetManyByUserIdAsync(owner.Id)
            .Returns(new List<Device>());

        // Act
        var result = await sutProvider.Sut.SignUpAsync(license, owner, ownerKey, collectionName, publicKey, privateKey);

        // Assert
        Assert.NotNull(result.organization);
        Assert.NotNull(result.organizationUser);

        sutProvider.GetDependency<ILicensingService>()
            .Received(1)
            .GetClaimsPrincipalFromLicense(license);
    }

    [Theory, BitAutoData]
    public async Task SignUpAsync_WithoutCollectionName_DoesNotCreateCollection(
        User owner, string ownerKey, string publicKey, string privateKey,
        SutProvider<SelfHostedOrganizationSignUpCommand> sutProvider)
    {
        // Arrange
        var globalSettings = sutProvider.GetDependency<IGlobalSettings>();
        var license = CreateValidOrganizationLicense(globalSettings);

        SetupCommonMocks(sutProvider, owner);
        SetupLicenseValidation(sutProvider, license);

        sutProvider.GetDependency<IDeviceRepository>()
            .GetManyByUserIdAsync(owner.Id)
            .Returns(new List<Device>());

        // Act
        var result = await sutProvider.Sut.SignUpAsync(license, owner, ownerKey, null, publicKey, privateKey);

        // Assert
        Assert.NotNull(result.organization);
        Assert.NotNull(result.organizationUser);

        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceive()
            .CreateAsync(Arg.Any<Collection>(), Arg.Is<IEnumerable<CollectionAccessSelection>>(x => true), Arg.Is<IEnumerable<CollectionAccessSelection>>(x => true));
    }

    [Theory, BitAutoData]
    public async Task SignUpAsync_WithDevices_RegistersDevicesForPushNotifications(
        User owner, string ownerKey, string collectionName,
        string publicKey, string privateKey, List<Device> devices,
        SutProvider<SelfHostedOrganizationSignUpCommand> sutProvider)
    {
        // Arrange
        var globalSettings = sutProvider.GetDependency<IGlobalSettings>();
        var license = CreateValidOrganizationLicense(globalSettings);

        foreach (var device in devices)
        {
            device.PushToken = "push-token-" + device.Id;
        }

        SetupCommonMocks(sutProvider, owner);
        SetupLicenseValidation(sutProvider, license);

        sutProvider.GetDependency<IDeviceRepository>()
            .GetManyByUserIdAsync(owner.Id)
            .Returns(devices);

        // Act
        var result = await sutProvider.Sut.SignUpAsync(license, owner, ownerKey, collectionName, publicKey, privateKey);

        // Assert
        Assert.NotNull(result.organization);
        Assert.NotNull(result.organizationUser);

        var expectedDeviceIds = devices.Select(d => d.Id.ToString());
        await sutProvider.GetDependency<IPushRegistrationService>()
            .Received(1)
            .AddUserRegistrationOrganizationAsync(
                Arg.Is<IEnumerable<string>>(ids => ids.SequenceEqual(expectedDeviceIds)),
                result.organization.Id.ToString());
    }

    [Theory, BitAutoData]
    public async Task SignUpAsync_OnException_CleansUpOrganization(
        User owner, string ownerKey, string collectionName,
        string publicKey, string privateKey,
        SutProvider<SelfHostedOrganizationSignUpCommand> sutProvider)
    {
        // Arrange
        var globalSettings = sutProvider.GetDependency<IGlobalSettings>();
        var license = CreateValidOrganizationLicense(globalSettings);

        SetupCommonMocks(sutProvider, owner);
        SetupLicenseValidation(sutProvider, license);

        sutProvider.GetDependency<IOrganizationApiKeyRepository>()
            .CreateAsync(Arg.Any<OrganizationApiKey>())
            .Throws(new Exception("Test exception"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(
            () => sutProvider.Sut.SignUpAsync(license, owner, ownerKey, collectionName, publicKey, privateKey));

        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .DeleteAsync(Arg.Any<Organization>());

        await sutProvider.GetDependency<IApplicationCacheService>()
            .Received(1)
            .DeleteOrganizationAbilityAsync(Arg.Any<Guid>());
    }

    private void SetupCommonMocks(
        SutProvider<SelfHostedOrganizationSignUpCommand> sutProvider,
        User owner)
    {
        var globalSettings = sutProvider.GetDependency<IGlobalSettings>();

        sutProvider.GetDependency<IOrganizationRepository>()
            .CreateAsync(Arg.Any<Organization>())
            .Returns(callInfo =>
            {
                var org = callInfo.Arg<Organization>();
                org.Id = Guid.NewGuid();
                return Task.FromResult(org);
            });

        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(owner.Id, PolicyType.SingleOrg)
            .Returns(false);

        globalSettings.LicenseDirectory.Returns("/tmp/licenses");
    }

    private void SetupLicenseValidation(
        SutProvider<SelfHostedOrganizationSignUpCommand> sutProvider,
        OrganizationLicense license)
    {
        var globalSettings = sutProvider.GetDependency<IGlobalSettings>();

        sutProvider.GetDependency<ILicensingService>()
            .VerifyLicense(license)
            .Returns(true);

        license.CanUse(globalSettings, sutProvider.GetDependency<ILicensingService>(), null, out _)
            .Returns(true);
    }

    private OrganizationLicense CreateValidOrganizationLicense(
        IGlobalSettings globalSettings,
        LicenseType licenseType = LicenseType.Organization)
    {

        return new OrganizationLicense
        {
            LicenseType = licenseType,
            Signature = Guid.NewGuid().ToString().Replace('-', '+'),
            Issued = DateTime.UtcNow.AddDays(-1),
            Expires = DateTime.UtcNow.AddDays(10),
            Version = OrganizationLicense.CurrentLicenseFileVersion,
            InstallationId = globalSettings.Installation.Id,
            Enabled = true,
            SelfHost = true
        };
    }
}
