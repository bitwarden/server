using Bit.Core.Models.Business;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Bit.Core.Services
{
    public class LicensingService : ILicensingService
    {
        private readonly X509Certificate2 _certificate;
        private readonly GlobalSettings _globalSettings;
        private readonly IUserRepository _userRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly ILogger<LicensingService> _logger;

        private IDictionary<Guid, DateTime> _userCheckCache = new Dictionary<Guid, DateTime>();

        public LicensingService(
            IUserRepository userRepository,
            IOrganizationRepository organizationRepository,
            IOrganizationUserRepository organizationUserRepository,
            IHostingEnvironment environment,
            ILogger<LicensingService> logger,
            GlobalSettings globalSettings)
        {
            _userRepository = userRepository;
            _organizationRepository = organizationRepository;
            _organizationUserRepository = organizationUserRepository;
            _logger = logger;

            var certThumbprint = "‎B34876439FCDA2846505B2EFBBA6C4A951313EBE";
            _globalSettings = globalSettings;
            _certificate = !_globalSettings.SelfHosted ? CoreHelpers.GetCertificate(certThumbprint)
                : CoreHelpers.GetEmbeddedCertificate("licensing.cer", null);
            if(_certificate == null || !_certificate.Thumbprint.Equals(CoreHelpers.CleanCertificateThumbprint(certThumbprint),
                StringComparison.InvariantCultureIgnoreCase))
            {
                throw new Exception("Invalid licensing certificate.");
            }

            if(_globalSettings.SelfHosted && !CoreHelpers.SettingHasValue(_globalSettings.LicenseDirectory))
            {
                throw new InvalidOperationException("No license directory.");
            }
        }

        public async Task ValidateOrganizationsAsync()
        {
            if(!_globalSettings.SelfHosted)
            {
                return;
            }

            var enabledOrgs = await _organizationRepository.GetManyByEnabledAsync();
            _logger.LogInformation("Validating licenses for {0} organizations.", enabledOrgs.Count);

            foreach(var org in enabledOrgs)
            {
                var license = ReadOrganiztionLicense(org);
                if(license == null)
                {
                    await DisableOrganizationAsync(org, null);
                    continue;
                }

                var totalLicensedOrgs = enabledOrgs.Count(o => o.LicenseKey.Equals(license.LicenseKey));
                if(totalLicensedOrgs > 1 || !license.VerifyData(org, _globalSettings) || !license.VerifySignature(_certificate))
                {
                    await DisableOrganizationAsync(org, license);
                }
            }
        }

        private async Task DisableOrganizationAsync(Organization org, ILicense license)
        {
            _logger.LogInformation("Organization {0}({1}) has an invalid license and is being disabled.", org.Id, org.Name);
            org.Enabled = false;
            org.ExpirationDate = license?.Expires ?? DateTime.UtcNow;
            org.RevisionDate = DateTime.UtcNow;
            await _organizationRepository.ReplaceAsync(org);
        }

        public async Task ValidateUsersAsync()
        {
            if(!_globalSettings.SelfHosted)
            {
                return;
            }

            var premiumUsers = await _userRepository.GetManyByPremiumAsync(true);
            _logger.LogInformation("Validating premium for {0} users.", premiumUsers.Count);

            foreach(var user in premiumUsers)
            {
                await ProcessUserValidationAsync(user);
            }

            var nonPremiumUsers = await _userRepository.GetManyByPremiumAsync(false);
            _logger.LogInformation("Checking to restore premium for {0} users.", nonPremiumUsers.Count);

            foreach(var user in nonPremiumUsers)
            {
                var details = await _organizationUserRepository.GetManyDetailsByUserAsync(user.Id);
                if(details.Any(d => d.SelfHost && d.UsersGetPremium && d.Enabled))
                {
                    _logger.LogInformation("Granting premium to user {0}({1}) because they are in an active organization " +
                        "with premium features.", user.Id, user.Email);

                    user.Premium = true;
                    user.MaxStorageGb = 10240; // 10 TB
                    user.RevisionDate = DateTime.UtcNow;
                    user.PremiumExpirationDate = null;
                    user.LicenseKey = null;
                    await _userRepository.ReplaceAsync(user);
                }
            }
        }

        public async Task<bool> ValidateUserPremiumAsync(User user)
        {
            if(!_globalSettings.SelfHosted)
            {
                return user.Premium;
            }

            if(!user.Premium)
            {
                return false;
            }

            // Only check once per day
            var now = DateTime.UtcNow;
            if(_userCheckCache.ContainsKey(user.Id))
            {
                var lastCheck = _userCheckCache[user.Id];
                if(lastCheck < now && now - lastCheck < TimeSpan.FromDays(1))
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

            _logger.LogInformation("Validating premium license for user {0}({1}).", user.Id, user.Email);
            return await ProcessUserValidationAsync(user);
        }

        private async Task<bool> ProcessUserValidationAsync(User user)
        {
            var license = ReadUserLicense(user);
            var valid = license != null && license.VerifyData(user) && license.VerifySignature(_certificate);

            if(!valid)
            {
                var details = await _organizationUserRepository.GetManyDetailsByUserAsync(user.Id);
                valid = details.Any(d => d.SelfHost && d.UsersGetPremium && d.Enabled);

                if(valid && (!string.IsNullOrWhiteSpace(user.LicenseKey) || user.PremiumExpirationDate.HasValue))
                {
                    // user used to be on a license but is now part of a organization that gives them premium.
                    // update the record.
                    user.PremiumExpirationDate = null;
                    user.LicenseKey = null;
                    await _userRepository.ReplaceAsync(user);
                }
            }

            if(!valid)
            {
                _logger.LogInformation("User {0}({1}) has an invalid license and premium is being disabled.",
                    user.Id, user.Email);

                user.Premium = false;
                user.PremiumExpirationDate = license?.Expires ?? DateTime.UtcNow;
                user.RevisionDate = DateTime.UtcNow;
                await _userRepository.ReplaceAsync(user);
            }

            return valid;
        }

        public bool VerifyLicense(ILicense license)
        {
            return license.VerifySignature(_certificate);
        }

        public byte[] SignLicense(ILicense license)
        {
            if(_globalSettings.SelfHosted || !_certificate.HasPrivateKey)
            {
                throw new InvalidOperationException("Cannot sign licenses.");
            }

            return license.Sign(_certificate);
        }

        private UserLicense ReadUserLicense(User user)
        {
            var filePath = $"{_globalSettings.LicenseDirectory}/user/{user.Id}.json";
            if(!File.Exists(filePath))
            {
                return null;
            }

            var data = File.ReadAllText(filePath, Encoding.UTF8);
            return JsonConvert.DeserializeObject<UserLicense>(data);
        }

        private OrganizationLicense ReadOrganiztionLicense(Organization organization)
        {
            var filePath = $"{_globalSettings.LicenseDirectory}/organization/{organization.Id}.json";
            if(!File.Exists(filePath))
            {
                return null;
            }

            var data = File.ReadAllText(filePath, Encoding.UTF8);
            return JsonConvert.DeserializeObject<OrganizationLicense>(data);
        }
    }
}
