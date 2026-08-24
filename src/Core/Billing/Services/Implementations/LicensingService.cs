// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Licenses.Models;
using Bit.Core.Billing.Licenses.Services;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Bit.Core.Billing.Services;

public class LicensingService : ILicensingService
{
    private const string _productionCertThumbprint = "‎B34876439FCDA2846505B2EFBBA6C4A951313EBE";
    private const string _developmentCertThumbprint = "207E64A231E8AA32AAF68A61037C075EBEBD553F";
    private readonly X509Certificate2 _creationCertificate;
    private readonly HashSet<X509Certificate2> _verificationCertificates;
    private readonly IGlobalSettings _globalSettings;
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IMailService _mailService;
    private readonly ILogger<LicensingService> _logger;
    private readonly ILicenseClaimsFactory<Organization> _organizationLicenseClaimsFactory;
    private readonly ILicenseClaimsFactory<User> _userLicenseClaimsFactory;
    private readonly IPushNotificationService _pushNotificationService;

    private IDictionary<Guid, DateTime> _userCheckCache = new Dictionary<Guid, DateTime>();

    public LicensingService(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IMailService mailService,
        IWebHostEnvironment environment,
        ILogger<LicensingService> logger,
        IGlobalSettings globalSettings,
        ILicenseClaimsFactory<Organization> organizationLicenseClaimsFactory,
        ILicenseClaimsFactory<User> userLicenseClaimsFactory,
        IPushNotificationService pushNotificationService)
    {
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _mailService = mailService;
        _logger = logger;
        _globalSettings = globalSettings;
        _organizationLicenseClaimsFactory = organizationLicenseClaimsFactory;
        _userLicenseClaimsFactory = userLicenseClaimsFactory;
        _pushNotificationService = pushNotificationService;


        // Load license creation cert
        var creationCertThumbprint = environment.IsDevelopment() ? _developmentCertThumbprint : _productionCertThumbprint;
        _verificationCertificates = new HashSet<X509Certificate2>();
        if (_globalSettings.SelfHosted)
        {
            X509Certificate2 devCert = null;
            X509Certificate2 prodCert = CoreHelpers.GetEmbeddedCertificateAsync("licensing.cer", null).GetAwaiter().GetResult();

            if (environment.IsDevelopment())
            {
                devCert = CoreHelpers.GetEmbeddedCertificateAsync("licensing_dev.cer", null).GetAwaiter().GetResult();
                _creationCertificate = devCert;
                // All self host envs accept prod cert. Creation cert added below to handle dev self-hosts
                _verificationCertificates.Add(prodCert);
            }
            else
            {
                _creationCertificate = prodCert;
            }

            // non-production environments can use dev cert-generated licenses
            if (!environment.IsProduction())
            {
                devCert ??= CoreHelpers.GetEmbeddedCertificateAsync("licensing_dev.cer", null).GetAwaiter().GetResult();
                _verificationCertificates.Add(devCert);
            }
        }
        else if (CoreHelpers.SettingHasValue(_globalSettings.LicenseCertificatePath) && CoreHelpers.SettingHasValue(_globalSettings.LicenseCertificatePassword))
        {
            _creationCertificate = CoreHelpers.GetCertificate(_globalSettings.LicenseCertificatePath, _globalSettings.LicenseCertificatePassword);
        }
        else if (CoreHelpers.SettingHasValue(_globalSettings.Storage?.ConnectionString) &&
            CoreHelpers.SettingHasValue(_globalSettings.LicenseCertificatePassword))
        {
            _creationCertificate = CoreHelpers.GetBlobCertificateAsync(globalSettings.Storage.ConnectionString, "certificates",
                "licensing.pfx", _globalSettings.LicenseCertificatePassword)
                .GetAwaiter().GetResult();
        }
        else
        {
            _creationCertificate = CoreHelpers.GetCertificate(creationCertThumbprint);
        }
        // Creation cert can always be used to verify
        _verificationCertificates.Add(_creationCertificate);

        if (_creationCertificate == null || !_creationCertificate.Thumbprint.Equals(CoreHelpers.CleanCertificateThumbprint(creationCertThumbprint),
            StringComparison.InvariantCultureIgnoreCase))
        {
            throw new Exception("Invalid licensing certificate.");
        }
        var allowedThumbprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            CoreHelpers.CleanCertificateThumbprint(_productionCertThumbprint),
            CoreHelpers.CleanCertificateThumbprint(_developmentCertThumbprint)
        };
        if (_verificationCertificates is null || _verificationCertificates.Count == 0
            || _verificationCertificates.Any(c => !allowedThumbprints.Contains(c.Thumbprint)))
        {
            throw new Exception("Invalid license verifying certificate.");
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

                if (!license.VerifyData(org, GetClaimsPrincipalFromLicense(license), _globalSettings))
                {
                    await DisableOrganizationAsync(org, license, "Invalid data.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(license.Token) && !_verificationCertificates.Any(c => license.VerifySignature(c)))
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
        _logger.LogInformation(Core.Constants.BypassFiltersEventId, null,
            "Organization {0} ({1}) has an invalid license and is being disabled. Reason: {2}",
            org.Id, org.DisplayName(), reason);
        org.Enabled = false;
        org.ExpirationDate = license?.Expires ?? DateTime.UtcNow;
        org.RevisionDate = DateTime.UtcNow;
        await _organizationRepository.ReplaceAsync(org);

        await _mailService.SendLicenseExpiredAsync(new List<string> { org.BillingEmail }, org.DisplayName());
    }

    public async Task ValidateUsersAsync()
    {
        if (!_globalSettings.SelfHosted)
        {
            return;
        }

        var premiumUsers = await _userRepository.GetManyByPremiumAsync(true);
        _logger.LogInformation(Core.Constants.BypassFiltersEventId, null,
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
        if (_userCheckCache.TryGetValue(user.Id, out var lastCheck))
        {
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

        _logger.LogInformation(Core.Constants.BypassFiltersEventId, null,
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

        var claimsPrincipal = GetClaimsPrincipalFromLicense(license);
        if (!license.VerifyData(user, claimsPrincipal))
        {
            await DisablePremiumAsync(user, license, "Invalid data.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(license.Token) && !_verificationCertificates.Any(c => license.VerifySignature(c)))
        {
            await DisablePremiumAsync(user, license, "Invalid signature.");
            return false;
        }

        return true;
    }

    private async Task DisablePremiumAsync(User user, ILicense license, string reason)
    {
        _logger.LogInformation(Core.Constants.BypassFiltersEventId, null,
            "User {0}({1}) has an invalid license and premium is being disabled. Reason: {2}",
            user.Id, user.Email, reason);

        user.Premium = false;
        user.PremiumExpirationDate = license?.Expires ?? DateTime.UtcNow;
        user.RevisionDate = DateTime.UtcNow;
        await _userRepository.ReplaceAsync(user);

        await _mailService.SendLicenseExpiredAsync(new List<string> { user.Email });

        await _pushNotificationService.PushAsync(new PushNotification<PremiumStatusPushNotification>
        {
            Type = PushType.PremiumStatusChanged,
            Target = NotificationTarget.User,
            TargetId = user.Id,
            Payload = new PremiumStatusPushNotification
            {
                UserId = user.Id,
                Premium = user.Premium,
            },
            ExcludeCurrentContext = false,
        });
    }

    public bool VerifyLicense(ILicense license)
    {
        if (string.IsNullOrWhiteSpace(license.Token))
        {
            return _verificationCertificates.Any((c) => license.VerifySignature(c));
        }

        try
        {
            _ = GetClaimsPrincipalFromLicense(license);
            return true;
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Invalid token.");
            return false;
        }
    }

    public byte[] SignLicense(ILicense license)
    {
        if (_globalSettings.SelfHosted || !_creationCertificate.HasPrivateKey)
        {
            throw new InvalidOperationException("Cannot sign licenses.");
        }

        return license.Sign(_creationCertificate);
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

    public ClaimsPrincipal GetClaimsPrincipalFromLicense(ILicense license)
    {
        if (string.IsNullOrWhiteSpace(license.Token))
        {
            return null;
        }

        var audience = license switch
        {
            OrganizationLicense orgLicense => $"organization:{orgLicense.Id}",
            UserLicense userLicense => $"user:{userLicense.Id}",
            _ => throw new ArgumentException("Unsupported license type.", nameof(license)),
        };

        var token = license.Token;
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = _verificationCertificates.Select(c => new X509SecurityKey(c)),
            ValidateIssuer = true,
            ValidIssuer = "bitwarden",
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RequireExpirationTime = true
        };

        try
        {
            return tokenHandler.ValidateToken(token, validationParameters, out _);
        }
        catch (Exception ex)
        {
            // Token exceptions thrown are interpreted by the client as Identity errors and cause the user to logout
            // Mask them by rethrowing as BadRequestException
            throw new BadRequestException($"Invalid license. {ex.Message}");
        }
    }

    public async Task<string> CreateOrganizationTokenAsync(Organization organization, Guid installationId, SubscriptionInfo subscriptionInfo)
    {
        var licenseContext = new LicenseContext
        {
            InstallationId = installationId,
            SubscriptionInfo = subscriptionInfo,
        };

        var claims = await _organizationLicenseClaimsFactory.GenerateClaims(organization, licenseContext);
        var audience = $"organization:{organization.Id}";

        return GenerateToken(claims, audience);
    }

    public async Task<string> CreateUserTokenAsync(User user, SubscriptionInfo subscriptionInfo)
    {
        var licenseContext = new LicenseContext { SubscriptionInfo = subscriptionInfo };
        var claims = await _userLicenseClaimsFactory.GenerateClaims(user, licenseContext);
        var audience = $"user:{user.Id}";

        return GenerateToken(claims, audience);
    }

    private string GenerateToken(List<Claim> claims, string audience)
    {
        if (claims.All(claim => claim.Type != JwtClaimTypes.JwtId))
        {
            claims.Add(new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString()));
        }

        var securityKey = new RsaSecurityKey(_creationCertificate.GetRSAPrivateKey());
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = "bitwarden",
            Audience = audience,
            NotBefore = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddYears(1), // Org expiration is a claim
            SigningCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public async Task WriteUserLicenseAsync(User user, UserLicense license)
    {
        var dir = $"{_globalSettings.LicenseDirectory}/user";
        Directory.CreateDirectory(dir);
        await using var fs = File.OpenWrite(Path.Combine(dir, $"{user.Id}.json"));
        await JsonSerializer.SerializeAsync(fs, license, JsonHelpers.Indented);
    }
}
