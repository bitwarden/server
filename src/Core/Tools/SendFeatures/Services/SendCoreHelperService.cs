using Bit.Core.Utilities;

namespace Bit.Core.Tools.Services;

public class SendCoreHelperService : ISendCoreHelperService
{
    public string SecureRandomString(int length, bool useUpperCase, bool useSpecial)
    {
        return CoreHelpers.SecureRandomString(length, upper: useUpperCase, special: useSpecial);
    }

}
