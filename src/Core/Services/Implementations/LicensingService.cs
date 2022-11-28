using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class LicensingService : ILicensingService
{
    private readonly X509Certificate2 _certificate;
    private readonly IGlobalSettings _globalSettings;
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IMailService _mailService;
    private readonly ILogger<LicensingService> _logger;

    private IDictionary<Guid, DateTime> _userCheckCache = new Dictionary<Guid, DateTime>();

    public LicensingService(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IMailService mailService,
        IWebHostEnvironment environment,
        ILogger<LicensingService> logger,
        IGlobalSettings globalSettings)
    {
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _mailService = mailService;
        _logger = logger;
        _globalSettings = globalSettings;

        var certThumbprint = environment.IsDevelopment() ?
            "207E64A231E8AA32AAF68A61037C075EBEBD553F" :
            "‎B34876439FCDA2846505B2EFBBA6C4A951313EBE";
        if (_globalSettings.SelfHosted)
        {
            _certificate = CoreHelpers.GetEmbeddedCertificateAsync(environment.IsDevelopment() ? "licensing_dev.cer" : "licensing.cer", null)
                .GetAwaiter().GetResult();
        }
        else if (CoreHelpers.SettingHasValue(_globalSettings.Storage?.ConnectionString) &&
            CoreHelpers.SettingHasValue(_globalSettings.LicenseCertificatePassword))
        {
            _certificate = CoreHelpers.GetBlobCertificateAsync(globalSettings.Storage.ConnectionString, "certificates",
                "licensing.pfx", _globalSettings.LicenseCertificatePassword)
                .GetAwaiter().GetResult();
        }
        else
        {
            _certificate = CoreHelpers.GetCertificate(certThumbprint);
        }

        if (_certificate == null || !_certificate.Thumbprint.Equals(CoreHelpers.CleanCertificateThumbprint(certThumbprint),
            StringComparison.InvariantCultureIgnoreCase))
        {
            throw new Exception("Invalid licensing certificate.");
        }

        if (_globalSettings.SelfHosted && !CoreHelpers.SettingHasValue(_globalSettings.LicenseDirectory))
        {
            throw new InvalidOperationException("No license directory.");
        }
    }

    public async Task ValidateOrganizationsAsync()
    {
        if (!_globalSettings.SelfHosted)
        {
            return;
        }

        var enabledOrgs = await _organizationRepository.GetManyByEnabledAsync();
        _logger.LogInformation(Constants.BypassFiltersEventId, null,
            "Validating licenses for {NumberOfOrganizations} organizations.", enabledOrgs.Count);

        var exceptions = new List<Exception>();

        foreach (var org in enabledOrgs)
        {
            try
            {
                var license = await ReadOrganizationLicenseAsync(org);
                if (license == null)
                {
                    await DisableOrganizationAsync(org, null, "No license file.");
                    continue;
                }

                var totalLicensedOrgs = enabledOrgs.Count(o => string.Equals(o.LicenseKey, license.LicenseKey));
                if (totalLicensedOrgs > 1)
                {
                    await DisableOrganizationAsync(org, license, "Multiple organizations.");
                    continue;
                }

                if (!license.VerifyData(org, _globalSettings))
                {
                    await DisableOrganizationAsync(org, license, "Invalid data.");
                    continue;
                }

                if (!license.VerifySignature(_certificate))
                {
                    await DisableOrganizationAsync(org, license, "Invalid signature.");
                    continue;
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        if (exceptions.Any())
        {
            throw new AggregateException("There were one or more exceptions while validating organizations.", exceptions);
        }
    }

    private async Task DisableOrganizationAsync(Organization org, ILicense license, string reason)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId, null,
            "Organization {0} ({1}) has an invalid license and is being disabled. Reason: {2}",
            org.Id, org.Name, reason);
        org.Enabled = false;
        org.ExpirationDate = license?.Expires ?? DateTime.UtcNow;
        org.RevisionDate = DateTime.UtcNow;
        await _organizationRepository.ReplaceAsync(org);

        await _mailService.SendLicenseExpiredAsync(new List<string> { org.BillingEmail }, org.Name);
    }

    public async Task ValidateUsersAsync()
    {
        if (!_globalSettings.SelfHosted)
        {
            return;
        }

        var premiumUsers = await _userRepository.GetManyByPremiumAsync(true);
        _logger.LogInformation(Constants.BypassFiltersEventId, null,
            "Validating premium for {0} users.", premiumUsers.Count);

        foreach (var user in premiumUsers)
        {
            await ProcessUserValidationAsync(user);
        }
    }

    public async Task<bool> ValidateUserPremiumAsync(User user)
    {
        if (!_globalSettings.SelfHosted)
        {
            return user.Premium;
        }

        if (!user.Premium)
        {
            return false;
        }

        // Only check once per day
        var now = DateTime.UtcNow;
        if (_userCheckCache.ContainsKey(user.Id))
        {
            var lastCheck = _userCheckCache[user.Id];
            if (lastCheck < now && now - lastCheck < TimeSpan.FromDays(1))
            {
                return user.Premium;
            }
            else
            {
                _userCheckCache[user.Id] = now;
            }
        }
        else
        {
            _userCheckCache.Add(user.Id, now);
        }

        _logger.LogInformation(Constants.BypassFiltersEventId, null,
            "Validating premium license for user {0}({1}).", user.Id, user.Email);
        return await ProcessUserValidationAsync(user);
    }

    private async Task<bool> ProcessUserValidationAsync(User user)
    {
        var license = ReadUserLicense(user);
        if (license == null)
        {
            await DisablePremiumAsync(user, null, "No license file.");
            return false;
        }

        if (!license.VerifyData(user))
        {
            await DisablePremiumAsync(user, license, "Invalid data.");
            return false;
        }

        if (!license.VerifySignature(_certificate))
        {
            await DisablePremiumAsync(user, license, "Invalid signature.");
            return false;
        }

        return true;
    }

    private async Task DisablePremiumAsync(User user, ILicense license, string reason)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId, null,
            "User {0}({1}) has an invalid license and premium is being disabled. Reason: {2}",
            user.Id, user.Email, reason);

        user.Premium = false;
        user.PremiumExpirationDate = license?.Expires ?? DateTime.UtcNow;
        user.RevisionDate = DateTime.UtcNow;
        await _userRepository.ReplaceAsync(user);

        await _mailService.SendLicenseExpiredAsync(new List<string> { user.Email });
    }

    public bool VerifyLicense(ILicense license)
    {
        return license.VerifySignature(_certificate);
    }

    public byte[] SignLicense(ILicense license)
    {
        if (_globalSettings.SelfHosted || !_certificate.HasPrivateKey)
        {
            throw new InvalidOperationException("Cannot sign licenses.");
        }

        return license.Sign(_certificate);
    }

    private UserLicense ReadUserLicense(User user)
    {
        var filePath = $"{_globalSettings.LicenseDirectory}/user/{user.Id}.json";
        if (!File.Exists(filePath))
        {
            return null;
        }

        var data = File.ReadAllText(filePath, Encoding.UTF8);
        return JsonSerializer.Deserialize<UserLicense>(data);
    }

    public Task<OrganizationLicense> ReadOrganizationLicenseAsync(Organization organization) =>
        ReadOrganizationLicenseAsync(organization.Id);
    public async Task<OrganizationLicense> ReadOrganizationLicenseAsync(Guid organizationId)
    {
        var filePath = Path.Combine(_globalSettings.LicenseDirectory, "organization", $"{organizationId}.json");
        if (!File.Exists(filePath))
        {
            return null;
        }

        using var fs = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<OrganizationLicense>(fs);
    }
}
