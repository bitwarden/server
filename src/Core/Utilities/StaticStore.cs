// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Enums;

namespace Bit.Core.Utilities;

public static class StaticStore
{
    static StaticStore()
    {
        #region Global Domains

        GlobalDomains = new Dictionary<GlobalEquivalentDomainsType, IEnumerable<string>>();

        GlobalDomains.Add(GlobalEquivalentDomainsType.Google, new List<string> { "youtube.com", "google.com", "gmail.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Apple, new List<string> { "apple.com", "icloud.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Merrill, new List<string> { "mymerrill.com", "ml.com", "merrilledge.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Microsoft, new List<string> { "bing.com", "hotmail.com", "live.com", "microsoft.com", "msn.com", "passport.net", "windows.com", "microsoftonline.com", "office.com", "office365.com", "microsoftstore.com", "xbox.com", "azure.com", "windowsazure.com", "cloud.microsoft" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.PayPal, new List<string> { "paypal.com", "paypal-search.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Amazon, new List<string> { "amazon.com", "amazon.com.be", "amazon.ae", "amazon.ca", "amazon.co.uk", "amazon.com.au", "amazon.com.br", "amazon.com.mx", "amazon.com.tr", "amazon.de", "amazon.es", "amazon.fr", "amazon.in", "amazon.it", "amazon.nl", "amazon.pl", "amazon.sa", "amazon.se", "amazon.sg" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Comcast, new List<string> { "comcast.com", "comcast.net", "xfinity.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Alibaba, new List<string> { "alibaba.com", "aliexpress.com", "aliyun.com", "net.cn" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Autodesk, new List<string> { "autodesk.com", "tinkercad.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Facebook, new List<string> { "facebook.com", "messenger.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Disney, new List<string> { "disneymoviesanywhere.com", "go.com", "disney.com", "dadt.com", "disneyplus.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Turbotax, new List<string> { "turbotax.com", "intuit.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Ebay, new List<string> { "ebay.com", "ebay.at", "ebay.be", "ebay.ca", "ebay.ch", "ebay.cn", "ebay.co.jp", "ebay.co.th", "ebay.co.uk", "ebay.com.au", "ebay.com.hk", "ebay.com.my", "ebay.com.sg", "ebay.com.tw", "ebay.de", "ebay.es", "ebay.fr", "ebay.ie", "ebay.in", "ebay.it", "ebay.nl", "ebay.ph", "ebay.pl" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.MorganStanley, new List<string> { "morganstanley.com", "morganstanleyclientserv.com", "stockplanconnect.com", "ms.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Airbnb, new List<string> { "airbnb.at", "airbnb.be", "airbnb.ca", "airbnb.ch", "airbnb.cl", "airbnb.co.cr", "airbnb.co.id", "airbnb.co.in", "airbnb.co.kr", "airbnb.co.nz", "airbnb.co.uk", "airbnb.co.ve", "airbnb.com", "airbnb.com.ar", "airbnb.com.au", "airbnb.com.bo", "airbnb.com.br", "airbnb.com.bz", "airbnb.com.co", "airbnb.com.ec", "airbnb.com.gt", "airbnb.com.hk", "airbnb.com.hn", "airbnb.com.mt", "airbnb.com.my", "airbnb.com.ni", "airbnb.com.pa", "airbnb.com.pe", "airbnb.com.py", "airbnb.com.sg", "airbnb.com.sv", "airbnb.com.tr", "airbnb.com.tw", "airbnb.cz", "airbnb.de", "airbnb.dk", "airbnb.es", "airbnb.fi", "airbnb.fr", "airbnb.gr", "airbnb.gy", "airbnb.hu", "airbnb.ie", "airbnb.is", "airbnb.it", "airbnb.jp", "airbnb.mx", "airbnb.nl", "airbnb.no", "airbnb.pl", "airbnb.pt", "airbnb.ru", "airbnb.se" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.StackExchange, new List<string> { "stackexchange.com", "superuser.com", "stackoverflow.com", "serverfault.com", "mathoverflow.net", "askubuntu.com", "stackapps.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Docusign, new List<string> { "docusign.com", "docusign.net" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Ubiquiti, new List<string> { "ubnt.com", "ui.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Yandex, new List<string> { "yandex.com", "ya.ru", "yandex.az", "yandex.by", "yandex.co.il", "yandex.com.am", "yandex.com.ge", "yandex.com.tr", "yandex.ee", "yandex.fi", "yandex.fr", "yandex.kg", "yandex.kz", "yandex.lt", "yandex.lv", "yandex.md", "yandex.pl", "yandex.ru", "yandex.tj", "yandex.tm", "yandex.ua", "yandex.uz" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Proton, new List<string> { "proton.me", "protonmail.com", "protonvpn.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Atlassian, new List<string> { "atlassian.com", "bitbucket.org", "trello.com", "statuspage.io", "atlassian.net", "jira.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Twitter, new List<string> { "twitter.com", "x.com" });
        #endregion
    }

    public static IDictionary<GlobalEquivalentDomainsType, IEnumerable<string>> GlobalDomains { get; set; }
}
