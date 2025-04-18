namespace Bit.Core.Tools.Services;

public interface ISendCoreHelperService
{
    string SecureRandomString(int length, bool useUpperCase, bool useSpecial);
}
