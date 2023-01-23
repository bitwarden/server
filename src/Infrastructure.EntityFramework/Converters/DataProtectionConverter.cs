using Bit.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Bit.Infrastructure.EntityFramework.Converters;
public class DataProtectionConverter : ValueConverter<string, string>
{
    public DataProtectionConverter(IDataProtector dataProtector) :
        base(s => Protect(dataProtector, s), s => Unprotect(dataProtector, s))
    { }

    private static string Protect(IDataProtector dataProtector, string value)
    {
        if (value?.StartsWith(Constants.DatabaseFieldProtectedPrefix) ?? true)
        {
            return value;
        }

        return string.Concat(
            Constants.DatabaseFieldProtectedPrefix, dataProtector.Protect(value));
    }

    private static string Unprotect(IDataProtector dataProtector, string value)
    {
        if (!value?.StartsWith(Constants.DatabaseFieldProtectedPrefix) ?? true)
        {
            return value;
        }

        return dataProtector.Unprotect(
            value.Substring(Constants.DatabaseFieldProtectedPrefix.Length));
    }
}
