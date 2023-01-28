using System.Reflection;
using Bit.Core.Resources;
using Microsoft.Extensions.Localization;

namespace Bit.Core.Services;

public class I18nService : II18nService
{
    private readonly IStringLocalizer _localizer;

    public I18nService(IStringLocalizerFactory factory)
    {
        var assemblyName = new AssemblyName(typeof(SharedResources).GetTypeInfo().Assembly.FullName);
        _localizer = factory.Create("SharedResources", assemblyName.Name);
    }

    public LocalizedString GetLocalizedHtmlString(string key)
    {
        return _localizer[key];
    }

    public LocalizedString GetLocalizedHtmlString(string key, params object[] args)
    {
        return _localizer[key, args];
    }

    public string Translate(string key, params object[] args)
    {
        return string.Format(GetLocalizedHtmlString(key).ToString(), args);
    }

    public string T(string key, params object[] args)
    {
        return Translate(key, args);
    }
}
