using Microsoft.Extensions.Localization;

namespace Bit.Core.Services;

public interface II18nService
{
    LocalizedString GetLocalizedHtmlString(string key);
    LocalizedString GetLocalizedHtmlString(string key, params object[] args);
    string Translate(string key, params object[] args);
    string T(string key, params object[] args);
}
