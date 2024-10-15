using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Licenses;
using Bit.Core.Billing.Licenses.ClaimsFactory;
using Bit.Core.Billing.Licenses.OrganizationLicenses;
using Bit.Core.Billing.Licenses.UserLicenses;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Bit.Core.Services;

public class LicensingService : ILicensingService
{
    private readonly X509Certificate2 _certificate;
    private readonly IGlobalSettings _globalSettings;
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IMailService _mailService;
    private readonly ILogger<LicensingService> _logger;
    private readonly IValidateEntityAgainstLicenseCommandHandler _validateEntityAgainstLicenseCommandHandler;
    private readonly ILicenseClaimsFactory<OrganizationLicense> _organizationLicenseClaimsFactory;
    private readonly ILicenseClaimsFactory<UserLicense> _userLicenseClaimsFactory;

    private readonly Dictionary<Guid, DateTime> _userCheckCache = new();

    public LicensingService(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IMailService mailService,
        IWebHostEnvironment environment,
        ILogger<LicensingService> logger,
        IGlobalSettings globalSettings,
        IValidateEntityAgainstLicenseCommandHandler validateEntityAgainstLicenseCommandHandler,
        ILicenseClaimsFactory<OrganizationLicense> organizationLicenseClaimsFactory,
        ILicenseClaimsFactory<UserLicense> userLicenseClaimsFactory)
    {
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _mailService = mailService;
        _logger = logger;
        _globalSettings = globalSettings;
        _validateEntityAgainstLicenseCommandHandler = validateEntityAgainstLicenseCommandHandler;
        _organizationLicenseClaimsFactory = organizationLicenseClaimsFactory;
        _userLicenseClaimsFactory = userLicenseClaimsFactory;

        var certThumbprint = environment.IsDevelopment() ?
            "207E64A231E8AA32AAF68A61037C075EBEBD553F" :
            "‎B34876439FCDA2846505B2EFBBA6C4A951313EBE";

        _certificate = GetCertificate(environment, globalSettings, certThumbprint);

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

    // This one is just used in ValidateOrganizationsJob and the logic can be moved there
    public async Task ValidateOrganizationsAsync()
    {
        if (!_globalSettings.SelfHosted)
        {
            return;
        }

        var enabledOrgs = await _organizationRepository.GetManyByEnabledAsync();

        _logger.LogInformation(Core.Constants.BypassFiltersEventId, null,
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

                var dataValidationResult = _validateEntityAgainstLicenseCommandHandler.Handle(
                    new ValidateEntityAgainstLicenseCommand { License = license, Organization = org }
                );

                if (!dataValidationResult.Succeeded)
                {
                    await DisableOrganizationAsync(org, license, string.Join(' ', dataValidationResult.Errors));
                    continue;
                }

                if (!VerifyLicenseSignature(license))
                {
                    await DisableOrganizationAsync(org, license, "Invalid signature.");
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        if (exceptions.Count != 0)
        {
            throw new AggregateException("There were one or more exceptions while validating organizations.", exceptions);
        }
    }

    // This one is just used in ValidateUsersJob and the logic can be moved there
    public async Task ValidateUsersAsync()
    {
        if (!_globalSettings.SelfHosted)
        {
            return;
        }

        var premiumUsers = await _userRepository.GetManyByPremiumAsync(true);

        _logger.LogInformation(
            Constants.BypassFiltersEventId,
            null,
            "Validating premium for {PremiumUsersCount} users.",
            premiumUsers.Count);

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
        if (!_userCheckCache.TryAdd(user.Id, now))
        {
            var lastCheck = _userCheckCache[user.Id];
            if (lastCheck < now && now - lastCheck < TimeSpan.FromDays(1))
            {
                return user.Premium;
            }

            _userCheckCache[user.Id] = now;
        }

        _logger.LogInformation(
            Constants.BypassFiltersEventId,
            null,
            "Validating premium license for user {UserId}({UserEmail}).",
            user.Id, user.Email);

        return await ProcessUserValidationAsync(user);
    }

    public bool VerifyLicenseSignature(ILicense license)
    {
        using var rsa = _certificate.GetRSAPublicKey();

        if (rsa is null)
        {
            throw new InvalidOperationException("Could not get RSA public key.");
        }

        return rsa.VerifyData(
            license.EncodedData,
            license.SignatureBytes,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }

    public byte[] SignLicense(ILicense license)
    {
        if (_globalSettings.SelfHosted)
        {
            throw new InvalidOperationException("Cannot sign licenses on self-hosted instances");
        }

        if (!_certificate.HasPrivateKey)
        {
            throw new InvalidOperationException("Cannot sign licenses using a certificate without a private key");
        }

        using var rsa = _certificate.GetRSAPrivateKey();

        if (rsa is null)
        {
            throw new InvalidOperationException("Could not get RSA private key when attempting to sign license");
        }

        return rsa.SignData(license.EncodedData, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    public async Task<string> GenerateToken(ILicense license)
    {
        if (_globalSettings.SelfHosted || !_certificate.HasPrivateKey)
        {
            throw new InvalidOperationException("Cannot generate tokens.");
        }

        var claims = license switch
        {
            OrganizationLicense organizationLicense => await _organizationLicenseClaimsFactory.GenerateClaimsAsync(organizationLicense),
            UserLicense userLicense => await _userLicenseClaimsFactory.GenerateClaimsAsync(userLicense),
            _ => throw new InvalidOperationException("Invalid license type.")
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = "Bitwarden",
            Audience = license.Id.ToString(),
            NotBefore = license.Issued,
            Expires = license.Expires,
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(_certificate.GetRSAPrivateKey()), SecurityAlgorithms.RsaSha256)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateJwtSecurityToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
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

        await using var fs = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<OrganizationLicense>(fs);
    }

    public async Task WriteLicenseToDiskAsync(Guid entityId, ILicense license)
    {
        if (string.IsNullOrWhiteSpace(_globalSettings.LicenseDirectory))
        {
            throw new InvalidOperationException("License directory is not configured in global settings.");
        }

        var directory = license.LicenseType switch
        {
            LicenseType.Organization => $"{_globalSettings.LicenseDirectory}/organization",
            LicenseType.User => $"{_globalSettings.LicenseDirectory}/user",
            _ => throw new InvalidOperationException("Invalid license type.")
        };

        Directory.CreateDirectory(directory);
        await using var fs = File.OpenWrite(Path.Combine(directory, $"{entityId}.json"));
        await JsonSerializer.SerializeAsync(fs, license, JsonHelpers.Indented);
    }

    private async Task<bool> ProcessUserValidationAsync(User user)
    {
        var license = ReadUserLicense(user);
        if (license == null)
        {
            await DisablePremiumAsync(user, null, "No license file.");
            return false;
        }

        var dataValidationResult = _validateEntityAgainstLicenseCommandHandler.Handle(
            new ValidateEntityAgainstLicenseCommand { License = license, User = user }
        );

        if (!dataValidationResult.Succeeded)
        {
            await DisablePremiumAsync(user, license, string.Join(' ', dataValidationResult.Errors));
            return false;
        }

        if (!VerifyLicenseSignature(license))
        {
            await DisablePremiumAsync(user, license, "Invalid signature.");
            return false;
        }

        return true;
    }

    private async Task DisablePremiumAsync(User user, ILicense license, string reason)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId,
            null,
            "User {UserId}({UserEmail}) has an invalid license and premium is being disabled. Reason: {Reason}",
            user.Id,
            user.Email,
            reason);

        user.Premium = false;
        user.PremiumExpirationDate = license?.Expires ?? DateTime.UtcNow;
        user.RevisionDate = DateTime.UtcNow;

        await _userRepository.ReplaceAsync(user);
        await _mailService.SendLicenseExpiredAsync(new List<string> { user.Email });
    }

    private async Task DisableOrganizationAsync(Organization org, ILicense license, string reason)
    {
        _logger.LogInformation(
            Core.Constants.BypassFiltersEventId,
            null,
            "Organization {OrganizationId} ({OrganizationDisplayName}) has an invalid license and is being disabled. Reason: {Reason}",
            org.Id, org.DisplayName(), reason);

        org.Enabled = false;
        org.ExpirationDate = license?.Expires ?? DateTime.UtcNow;
        org.RevisionDate = DateTime.UtcNow;

        await _organizationRepository.ReplaceAsync(org);
        await _mailService.SendLicenseExpiredAsync(new List<string> { org.BillingEmail }, org.DisplayName());
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

    private X509Certificate2 GetCertificate(IWebHostEnvironment environment, IGlobalSettings globalSettings, string certThumbprint)
    {
        if (_globalSettings.SelfHosted)
        {
            return CoreHelpers.GetEmbeddedCertificateAsync(environment.IsDevelopment() ? "licensing_dev.cer" : "licensing.cer", null)
                .GetAwaiter().GetResult();
        }

        if (CoreHelpers.SettingHasValue(_globalSettings.Storage?.ConnectionString) &&
            CoreHelpers.SettingHasValue(_globalSettings.LicenseCertificatePassword))
        {
            return CoreHelpers.GetBlobCertificateAsync(
                globalSettings.Storage.ConnectionString,
                "certificates",
                "licensing.pfx",
                _globalSettings.LicenseCertificatePassword).GetAwaiter().GetResult();
        }

        return CoreHelpers.GetCertificate(certThumbprint);
    }
}
