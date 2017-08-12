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
    public class RsaLicensingService : ILicensingService
    {
        private readonly X509Certificate2 _certificate;
        private readonly GlobalSettings _globalSettings;
        private IDictionary<string, UserLicense> _userLicenseCache;
        private IDictionary<string, OrganizationLicense> _organizationLicenseCache;

        public RsaLicensingService(
            IHostingEnvironment environment,
            GlobalSettings globalSettings)
        {
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

        public bool VerifyOrganizationPlan(Organization organization)
        {
            if(!_globalSettings.SelfHosted)
            {
                return true;
            }

            if(!organization.SelfHost)
            {
                return false;
            }

            var license = ReadOrganiztionLicense(organization);
            return license != null && license.VerifyData(organization) && license.VerifySignature(_certificate);
        }

        public bool VerifyUserPremium(User user)
        {
            if(!_globalSettings.SelfHosted)
            {
                return user.Premium;
            }

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
            if(_userLicenseCache != null && _userLicenseCache.ContainsKey(user.LicenseKey))
            {
                return _userLicenseCache[user.LicenseKey];
            }

            var filePath = $"{_globalSettings.LicenseDirectory}/user/{user.Id}.json";
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

            var filePath = $"{_globalSettings.LicenseDirectory}/organization/{organization.Id}.json";
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
