using Bit.Core.Models.Table;
using Microsoft.AspNetCore.Hosting;
using System;

namespace Bit.Core.Services
{
    public class NoopLicenseVerificationService : ILicenseVerificationService
    {
        public NoopLicenseVerificationService(
            IHostingEnvironment environment,
            GlobalSettings globalSettings)
        {
            if(!environment.IsDevelopment() && globalSettings.SelfHosted)
            {
                throw new Exception($"{nameof(NoopLicenseVerificationService)} cannot be used for self hosted instances.");
            }
        }

        public bool VerifyOrganizationPlan(Organization organization)
        {
            return true;
        }

        public bool VerifyUserPremium(User user)
        {
            return user.Premium;
        }
    }
}
