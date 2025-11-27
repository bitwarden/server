using System.Security.Claims;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Licenses.Extensions;
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
        string publicKey, string privateKey,
        SutProvider<SelfHostedOrganizationSignUpCommand> sutProvider)
    {
        // Arrange
        var globalSettings = sutProvider.GetDependency<IGlobalSettings>();
        var license = CreateValidOrganizationLicense(globalSettings);

        SetupCommonMocks(sutProvider, owner);

        // Create a ClaimsPrincipal that matches the license for VerifyData validation
        var claims = CreateMatchingClaims(license, globalSettings);
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        sutProvider.GetDependency<ILicensingService>()
            .VerifyLicense(license)
            .Returns(true);

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
        // VerifyData should pass (SetupLicenseValidation sets up matching claims)
        // The exception should be thrown when creating the API key, which happens after VerifyData
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

        // Create a ClaimsPrincipal that matches the license for VerifyData validation
        var claims = CreateMatchingClaims(license, globalSettings);
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        sutProvider.GetDependency<ILicensingService>()
            .GetClaimsPrincipalFromLicense(license)
            .Returns(claimsPrincipal);
    }

    private List<Claim> CreateMatchingClaims(OrganizationLicense license, IGlobalSettings globalSettings)
    {
        var issued = license.Issued;
        var expires = license.Expires ?? DateTime.UtcNow.AddYears(1);

        var claims = new List<Claim>
        {
            new Claim("LicenseType", LicenseType.Organization.ToString()),
            new Claim("Id", Guid.NewGuid().ToString()),
            new Claim("Enabled", license.Enabled.ToString()),
            new Claim("PlanType", ((int)(license.PlanType != 0 ? license.PlanType : PlanType.EnterpriseAnnually)).ToString()),
            new Claim("UsePolicies", (license.UsePolicies || false).ToString()),
            new Claim("UseSso", (license.UseSso || false).ToString()),
            new Claim("UseKeyConnector", (license.UseKeyConnector || false).ToString()),
            new Claim("UseScim", (license.UseScim || false).ToString()),
            new Claim("UseGroups", (license.UseGroups || false).ToString()),
            new Claim("UseEvents", (license.UseEvents || false).ToString()),
            new Claim("UseDirectory", (license.UseDirectory || false).ToString()),
            new Claim("UseTotp", (license.UseTotp || false).ToString()),
            new Claim("Use2fa", (license.Use2fa || false).ToString()),
            new Claim("UseApi", (license.UseApi || false).ToString()),
            new Claim("UseResetPassword", (license.UseResetPassword || false).ToString()),
            new Claim("SelfHost", license.SelfHost.ToString()),
            new Claim("UsersGetPremium", (license.UsersGetPremium || false).ToString()),
            new Claim("UseCustomPermissions", (license.UseCustomPermissions || false).ToString()),
            new Claim("UsePasswordManager", (license.UsePasswordManager || true).ToString()),
            new Claim("UseSecretsManager", (license.UseSecretsManager || false).ToString()),
            new Claim("UseOrganizationDomains", (license.UseOrganizationDomains || false).ToString()),
            new Claim("UseAdminSponsoredFamilies", (license.UseAdminSponsoredFamilies || false).ToString()),
            new Claim("UseAutomaticUserConfirmation", (license.UseAutomaticUserConfirmation || false).ToString()),
            new Claim("Issued", issued.ToString("O")),
            new Claim("Expires", expires.ToString("O")),
            new Claim("InstallationId", license.InstallationId.ToString()),
            new Claim("LicenseKey", license.LicenseKey ?? Guid.NewGuid().ToString()),
            new Claim("Name", license.Name ?? "Test Organization")
        };

        if (license.Seats.HasValue)
        {
            claims.Add(new Claim("Seats", license.Seats.Value.ToString()));
        }

        if (license.MaxCollections.HasValue)
        {
            claims.Add(new Claim("MaxCollections", license.MaxCollections.Value.ToString()));
        }

        if (license.SmSeats.HasValue)
        {
            claims.Add(new Claim("SmSeats", license.SmSeats.Value.ToString()));
        }

        if (license.SmServiceAccounts.HasValue)
        {
            claims.Add(new Claim("SmServiceAccounts", license.SmServiceAccounts.Value.ToString()));
        }

        return claims;
    }

    private OrganizationLicense CreateValidOrganizationLicense(
        IGlobalSettings globalSettings,
        LicenseType licenseType = LicenseType.Organization)
    {
        var issued = DateTime.UtcNow.AddDays(-1);
        var expires = DateTime.UtcNow.AddDays(10);

        return new OrganizationLicense
        {
            LicenseType = licenseType,
            Signature = Guid.NewGuid().ToString().Replace('-', '+'),
            Issued = issued,
            Expires = expires,
            Version = OrganizationLicense.CurrentLicenseFileVersion,
            InstallationId = globalSettings.Installation.Id,
            Enabled = true,
            SelfHost = true,
            LicenseKey = Guid.NewGuid().ToString(),
            Name = "Test Organization",
            PlanType = PlanType.EnterpriseAnnually,
            Seats = 10,
            MaxCollections = null,
            UsePolicies = true,
            UseSso = true,
            UseKeyConnector = false,
            UseScim = true,
            UseGroups = true,
            UseEvents = true,
            UseDirectory = true,
            UseTotp = true,
            Use2fa = true,
            UseApi = true,
            UseResetPassword = true,
            UsersGetPremium = true,
            UseCustomPermissions = true,
            UsePasswordManager = true,
            UseSecretsManager = false,
            SmSeats = null,
            SmServiceAccounts = null,
            UseOrganizationDomains = false,
            UseAdminSponsoredFamilies = false,
            UseAutomaticUserConfirmation = false,
            Token = "mock-token" // Ensure token exists so VerifyData is called
        };
    }

    private void UpdateLicenseFromClaims(OrganizationLicense license, ClaimsPrincipal claimsPrincipal, IGlobalSettings globalSettings)
    {
        // Update license properties to match claims for VerifyData validation
        var expires = claimsPrincipal.GetValue<DateTime>("Expires");
        var issued = claimsPrincipal.GetValue<DateTime>("Issued");

        if (expires != default(DateTime))
        {
            license.Expires = expires;
        }

        if (issued != default(DateTime))
        {
            license.Issued = issued;
        }

        var licenseKey = claimsPrincipal.GetValue<string>("LicenseKey");
        if (!string.IsNullOrEmpty(licenseKey))
        {
            license.LicenseKey = licenseKey;
        }

        var name = claimsPrincipal.GetValue<string>("Name");
        if (!string.IsNullOrEmpty(name))
        {
            license.Name = name;
        }

        var planType = claimsPrincipal.GetValue<PlanType>("PlanType");
        if (planType != 0)
        {
            license.PlanType = planType;
        }

        // Update other properties to match claims
        license.Enabled = claimsPrincipal.GetValue<bool>("Enabled");
        license.SelfHost = claimsPrincipal.GetValue<bool>("SelfHost");
        license.Seats = claimsPrincipal.GetValue<int?>("Seats");
        license.MaxCollections = claimsPrincipal.GetValue<short?>("MaxCollections");
        license.UseGroups = claimsPrincipal.GetValue<bool>("UseGroups");
        license.UseDirectory = claimsPrincipal.GetValue<bool>("UseDirectory");
        license.UseTotp = claimsPrincipal.GetValue<bool>("UseTotp");
        license.UsersGetPremium = claimsPrincipal.GetValue<bool>("UsersGetPremium");
        license.UseEvents = claimsPrincipal.GetValue<bool>("UseEvents");
        license.Use2fa = claimsPrincipal.GetValue<bool>("Use2fa");
        license.UseApi = claimsPrincipal.GetValue<bool>("UseApi");
        license.UsePolicies = claimsPrincipal.GetValue<bool>("UsePolicies");
        license.UseSso = claimsPrincipal.GetValue<bool>("UseSso");
        license.UseResetPassword = claimsPrincipal.GetValue<bool>("UseResetPassword");
        license.UseKeyConnector = claimsPrincipal.GetValue<bool>("UseKeyConnector");
        license.UseScim = claimsPrincipal.GetValue<bool>("UseScim");
        license.UseCustomPermissions = claimsPrincipal.GetValue<bool>("UseCustomPermissions");
        license.UseSecretsManager = claimsPrincipal.GetValue<bool>("UseSecretsManager");
        license.UsePasswordManager = claimsPrincipal.GetValue<bool>("UsePasswordManager");
        license.SmSeats = claimsPrincipal.GetValue<int?>("SmSeats");
        license.SmServiceAccounts = claimsPrincipal.GetValue<int?>("SmServiceAccounts");
        license.UseAdminSponsoredFamilies = claimsPrincipal.GetValue<bool>("UseAdminSponsoredFamilies");
        license.UseOrganizationDomains = claimsPrincipal.GetValue<bool>("UseOrganizationDomains");
        license.UseAutomaticUserConfirmation = claimsPrincipal.GetValue<bool>("UseAutomaticUserConfirmation");
    }
}
