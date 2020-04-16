using Microsoft.AspNetCore.Identity;
using Bit.Core.Models.Table;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Identity
{
    public class TwoFactorRememberTokenProvider : DataProtectorTokenProvider<User>
    {
        public TwoFactorRememberTokenProvider(
            IDataProtectionProvider dataProtectionProvider,
            IOptions<TwoFactorRememberTokenProviderOptions> options,
            ILogger<DataProtectorTokenProvider<User>> logger)
            : base(dataProtectionProvider, options, logger)
        { }
    }

    public class TwoFactorRememberTokenProviderOptions : DataProtectionTokenProviderOptions
    { }
}
