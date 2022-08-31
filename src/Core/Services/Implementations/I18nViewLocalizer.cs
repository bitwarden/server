using System.Reflection;
using Bit.Core.Resources;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;

namespace Bit.Core.Services;

public class I18nViewLocalizer : IViewLocalizer
{
    private readonly IStringLocalizer _stringLocalizer;
    private readonly IHtmlLocalizer _htmlLocalizer;

    public I18nViewLocalizer(IStringLocalizerFactory stringFactory,
        IHtmlLocalizerFactory htmlFactory)
    {
        var assemblyName = new AssemblyName(typeof(SharedResources).GetTypeInfo().Assembly.FullName);
        _stringLocalizer = stringFactory.Create("SharedResources", assemblyName.Name);
        _htmlLocalizer = htmlFactory.Create("SharedResources", assemblyName.Name);
    }

    public LocalizedHtmlString this[string name] => _htmlLocalizer[name];
    public LocalizedHtmlString this[string name, params object[] args] => _htmlLocalizer[name, args];

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        _stringLocalizer.GetAllStrings(includeParentCultures);

    public LocalizedString GetString(string name) => _stringLocalizer[name];
    public LocalizedString GetString(string name, params object[] arguments) =>
        _stringLocalizer[name, arguments];
}
