using System.Reflection;
using Microsoft.Extensions.Localization;

namespace Bit.Core.Utilities;

public static class StringLocalizerExtensions
{
    public static IStringLocalizer CreateLocalizer<T>(this IStringLocalizerFactory factory) where T : class
    {
        var assemblyName = new AssemblyName(typeof(T).GetTypeInfo().Assembly.FullName);
        return factory.Create(typeof(T).Name, assemblyName.Name);
    }
}
