using Bit.Core.Models.Table;
using Microsoft.AspNetCore.Hosting;
using System;
using Bit.Core.Models.Business;

namespace Bit.Core.Services
{
    public class NoopLicensingService : ILicensingService
    {
        public NoopLicensingService(
            IHostingEnvironment environment,
            GlobalSettings globalSettings)
        {
            if(!environment.IsDevelopment() && globalSettings.SelfHosted)
            {
                throw new Exception($"{nameof(NoopLicensingService)} cannot be used for self hosted instances.");
            }
        }

        public bool VerifyLicense(ILicense license)
        {
            return true;
        }

        public bool VerifyOrganizationPlan(Organization organization)
        {
            return true;
        }

        public bool VerifyUserPremium(User user)
        {
            return user.Premium;
        }

        public byte[] SignLicense(ILicense license)
        {
            return new byte[0];
        }
    }
}
