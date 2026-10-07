// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Bit.Infrastructure.EntityFramework.Converters;

public class DataProtectionConverter : ValueConverter<string, string>
{
    public DataProtectionConverter(IDataProtector dataProtector) :
        base(
            s => DatabaseFieldProtectionHelper.Protect(dataProtector, s),
            s => DatabaseFieldProtectionHelper.Unprotect(dataProtector, s))
    { }
}
