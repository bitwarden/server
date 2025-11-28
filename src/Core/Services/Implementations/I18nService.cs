#nullable enable

using Bit.Core.Resources;
using Microsoft.Extensions.Localization;

namespace Bit.Core.Services;

public class I18nService : II18nService
{
    private readonly IStringLocalizer _localizer;

    public I18nService(IStringLocalizerFactory factory)
    {
        var assemblyName = typeof(SharedResources).Assembly.GetName()!;
        _localizer = factory.Create("SharedResources", assemblyName.Name!);
    }

    public LocalizedString GetLocalizedHtmlString(string key)
    {
        return _localizer[key];
    }

    public LocalizedString GetLocalizedHtmlString(string key, params object?[] args)
    {
#nullable disable // IStringLocalizer does actually support null args, it is annotated incorrectly: https://github.com/dotnet/aspnetcore/issues/44251
        return _localizer[key, args];
#nullable enable
    }

    public string Translate(string key, params object?[] args)
    {
        return string.Format(GetLocalizedHtmlString(key).ToString(), args);
    }

    public string T(string key, params object?[] args)
    {
        return Translate(key, args);
    }
}
