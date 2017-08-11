using Bit.Core.Models.Business;
using Bit.Core.Models.Table;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Bit.Core.Services
{
    public class RsaLicenseVerificationService : ILicenseVerificationService
    {
        private readonly X509Certificate2 _certificate;
        private readonly GlobalSettings _globalSettings;
        private IDictionary<string, UserLicense> _userLicenseCache;
        private IDictionary<string, OrganizationLicense> _organizationLicenseCache;

        public RsaLicenseVerificationService(
            IHostingEnvironment environment,
            GlobalSettings globalSettings)
        {
            if(!environment.IsDevelopment() && !globalSettings.SelfHosted)
            {
                throw new Exception($"{nameof(RsaLicenseVerificationService)} can only be used for self hosted instances.");
            }

            _globalSettings = globalSettings;
            _certificate = CoreHelpers.GetEmbeddedCertificate("licensing.cer", null);
            if(!_certificate.Thumbprint.Equals(CoreHelpers.CleanCertificateThumbprint(
                "‎207e64a231e8aa32aaf68a61037c075ebebd553f"), StringComparison.InvariantCultureIgnoreCase))
            {
                throw new Exception("Invalid licensing certificate.");
            }

            if(!CoreHelpers.SettingHasValue(_globalSettings.LicenseDirectory))
            {
                throw new InvalidOperationException("No license directory.");
            }
        }

        public bool VerifyOrganizationPlan(Organization organization)
        {
            if(_globalSettings.SelfHosted && !organization.SelfHost)
            {
                return false;
            }

            var license = ReadOrganiztionLicense(organization);
            return license != null && license.VerifyData(organization) && license.VerifySignature(_certificate);
        }

        public bool VerifyUserPremium(User user)
        {
            if(!user.Premium)
            {
                return false;
            }

            var license = ReadUserLicense(user);
            return license != null && license.VerifyData(user) && license.VerifySignature(_certificate);
        }

        public bool VerifyLicense(ILicense license)
        {
            return license.VerifySignature(_certificate);
        }

        private UserLicense ReadUserLicense(User user)
        {
            if(_userLicenseCache != null && _userLicenseCache.ContainsKey(user.LicenseKey))
            {
                return _userLicenseCache[user.LicenseKey];
            }

            var filePath = $"{_globalSettings.LicenseDirectory}/user/{user.LicenseKey}.json";
            if(!File.Exists(filePath))
            {
                return null;
            }

            var data = File.ReadAllText(filePath, Encoding.UTF8);
            var obj = JsonConvert.DeserializeObject<UserLicense>(data);
            if(_userLicenseCache == null)
            {
                _userLicenseCache = new Dictionary<string, UserLicense>();
            }
            _userLicenseCache.Add(obj.LicenseKey, obj);
            return obj;
        }

        private OrganizationLicense ReadOrganiztionLicense(Organization organization)
        {
            if(_organizationLicenseCache != null && _organizationLicenseCache.ContainsKey(organization.LicenseKey))
            {
                return _organizationLicenseCache[organization.LicenseKey];
            }

            var filePath = $"{_globalSettings.LicenseDirectory}/organization/{organization.LicenseKey}.json";
            if(!File.Exists(filePath))
            {
                return null;
            }

            var data = File.ReadAllText(filePath, Encoding.UTF8);
            var obj = JsonConvert.DeserializeObject<OrganizationLicense>(data);
            if(_organizationLicenseCache == null)
            {
                _organizationLicenseCache = new Dictionary<string, OrganizationLicense>();
            }
            _organizationLicenseCache.Add(obj.LicenseKey, obj);
            return obj;
        }
    }
}
