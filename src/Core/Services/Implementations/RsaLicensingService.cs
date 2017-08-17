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
    public class RsaLicensingService : ILicensingService
    {
        private readonly X509Certificate2 _certificate;
        private readonly GlobalSettings _globalSettings;
        private readonly IUserRepository _userRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly ILogger<RsaLicensingService> _logger;

        private IDictionary<Guid, DateTime> _userCheckCache = new Dictionary<Guid, DateTime>();

        public RsaLicensingService(
            IUserRepository userRepository,
            IOrganizationRepository organizationRepository,
            IHostingEnvironment environment,
            ILogger<RsaLicensingService> logger,
            GlobalSettings globalSettings)
        {
            _userRepository = userRepository;
            _organizationRepository = organizationRepository;
            _logger = logger;

            var certThumbprint = "‎207e64a231e8aa32aaf68a61037c075ebebd553f";
            _globalSettings = globalSettings;
            _certificate = !_globalSettings.SelfHosted ? CoreHelpers.GetCertificate(certThumbprint)
                : CoreHelpers.GetEmbeddedCertificate("licensing.cer", null);
            if(_certificate == null || !_certificate.Thumbprint.Equals(CoreHelpers.CleanCertificateThumbprint(certThumbprint),
                StringComparison.InvariantCultureIgnoreCase))
            {
                throw new Exception("Invalid licensing certificate.");
            }

            if(!CoreHelpers.SettingHasValue(_globalSettings.LicenseDirectory))
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

            var orgs = await _organizationRepository.GetManyAsync();
            _logger.LogInformation("Validating licenses for {0} organizations.", orgs.Count);

            foreach(var org in orgs.Where(o => o.Enabled))
            {
                var license = ReadOrganiztionLicense(org);
                if(license == null || !license.VerifyData(org, _globalSettings) || !license.VerifySignature(_certificate))
                {
                    _logger.LogInformation("Organization {0}({1}) has an invalid license and is being disabled.",
                        org.Id, org.Name);

                    org.Enabled = false;
                    org.ExpirationDate = license.Expires;
                    org.RevisionDate = DateTime.UtcNow;
                    await _organizationRepository.ReplaceAsync(org);
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

            var license = ReadUserLicense(user);
            var licensedForPremium = license != null && license.VerifyData(user) && license.VerifySignature(_certificate);
            if(!licensedForPremium)
            {
                _logger.LogInformation("User {0}({1}) has an invalid license and premium is being disabled.",
                    user.Id, user.Email);

                user.Premium = false;
                user.PremiumExpirationDate = license.Expires;
                user.RevisionDate = DateTime.UtcNow;
                await _userRepository.ReplaceAsync(user);
            }

            return licensedForPremium;
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
