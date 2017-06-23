using Microsoft.AspNetCore.Identity;
using Bit.Core.Models.Table;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;

namespace Bit.Core.Identity
{
    public class TwoFactorRememberTokenProvider : DataProtectorTokenProvider<User>
    {
        private readonly GlobalSettings _globalSettings;

        public TwoFactorRememberTokenProvider(
            IDataProtectionProvider dataProtectionProvider,
            IOptions<TwoFactorRememberTokenProviderOptions> options)
            : base(dataProtectionProvider, options)
        { }
    }

    public class TwoFactorRememberTokenProviderOptions : DataProtectionTokenProviderOptions
    { }
}
