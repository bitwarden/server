using Bit.Core.Enums;
using System.Collections.Generic;

namespace Bit.Core.Utilities
{
    public class EquivalentDomains
    {
        static EquivalentDomains()
        {
            Global = new Dictionary<GlobalEquivalentDomainsType, IEnumerable<string>>();

            Global.Add(GlobalEquivalentDomainsType.Ameritrade, new List<string> { "ameritrade.com", "tdameritrade.com" });
            Global.Add(GlobalEquivalentDomainsType.BoA, new List<string> { "bankofamerica.com", "bofa.com", "mbna.com", "usecfo.com" });
            Global.Add(GlobalEquivalentDomainsType.Sprint, new List<string> { "sprint.com", "sprintpcs.com", "nextel.com" });
            Global.Add(GlobalEquivalentDomainsType.Google, new List<string> { "youtube.com", "google.com", "gmail.com" });
            Global.Add(GlobalEquivalentDomainsType.Apple, new List<string> { "apple.com", "icloud.com" });
            Global.Add(GlobalEquivalentDomainsType.WellsFargo, new List<string> { "wellsfargo.com", "wf.com" });
            Global.Add(GlobalEquivalentDomainsType.Merrill, new List<string> { "mymerrill.com", "ml.com", "merrilledge.com" });
            Global.Add(GlobalEquivalentDomainsType.Citi, new List<string> { "accountonline.com", "citi.com", "citibank.com", "citicards.com", "citibankonline.com" });
            Global.Add(GlobalEquivalentDomainsType.Cnet, new List<string> { "cnet.com", "cnettv.com", "com.com", "download.com", "news.com", "search.com", "upload.com" });
            Global.Add(GlobalEquivalentDomainsType.Gap, new List<string> { "bananarepublic.com", "gap.com", "oldnavy.com", "piperlime.com" });
            Global.Add(GlobalEquivalentDomainsType.Microsoft, new List<string> { "bing.com", "hotmail.com", "live.com", "microsoft.com", "msn.com", "passport.net", "windows.com", "microsoftonline.com" });
            Global.Add(GlobalEquivalentDomainsType.United, new List<string> { "ua2go.com", "ual.com", "united.com", "unitedwifi.com" });
            Global.Add(GlobalEquivalentDomainsType.Yahoo, new List<string> { "overture.com", "yahoo.com", "flickr.com" });
            Global.Add(GlobalEquivalentDomainsType.Zonelabs, new List<string> { "zonealarm.com", "zonelabs.com" });
            Global.Add(GlobalEquivalentDomainsType.Paypal, new List<string> { "paypal.com", "paypal-search.com" });
            Global.Add(GlobalEquivalentDomainsType.Avon, new List<string> { "avon.com", "youravon.com" });
            Global.Add(GlobalEquivalentDomainsType.Diapers, new List<string> { "diapers.com", "soap.com", "wag.com", "yoyo.com", "beautybar.com", "casa.com", "afterschool.com", "vine.com", "bookworm.com", "look.com", "vinemarket.com" });
            Global.Add(GlobalEquivalentDomainsType.Contacts, new List<string> { "1800contacts.com", "800contacts.com" });
            Global.Add(GlobalEquivalentDomainsType.Amazon, new List<string> { "amazon.com", "amazon.co.uk", "amazon.ca", "amazon.de", "amazon.fr", "amazon.es", "amazon.it", "amazon.com.au" });
            Global.Add(GlobalEquivalentDomainsType.Cox, new List<string> { "cox.com", "cox.net", "coxbusiness.com" });
            Global.Add(GlobalEquivalentDomainsType.Norton, new List<string> { "mynortonaccount.com", "norton.com" });
            Global.Add(GlobalEquivalentDomainsType.Verizon, new List<string> { "verizon.com", "verizon.net" });
            Global.Add(GlobalEquivalentDomainsType.Buy, new List<string> { "rakuten.com", "buy.com" });
            Global.Add(GlobalEquivalentDomainsType.Sirius, new List<string> { "siriusxm.com", "sirius.com" });
            Global.Add(GlobalEquivalentDomainsType.Ea, new List<string> { "ea.com", "origin.com", "play4free.com", "tiberiumalliance.com" });
            Global.Add(GlobalEquivalentDomainsType.Basecamp, new List<string> { "37signals.com", "basecamp.com", "basecamphq.com", "highrisehq.com" });
            Global.Add(GlobalEquivalentDomainsType.Steam, new List<string> { "steampowered.com", "steamcommunity.com" });
            Global.Add(GlobalEquivalentDomainsType.Chart, new List<string> { "chart.io", "chartio.com" });
            Global.Add(GlobalEquivalentDomainsType.Gotomeeting, new List<string> { "gotomeeting.com", "citrixonline.com" });
            Global.Add(GlobalEquivalentDomainsType.Gogo, new List<string> { "gogoair.com", "gogoinflight.com" });
            Global.Add(GlobalEquivalentDomainsType.Oracle, new List<string> { "mysql.com", "oracle.com" });
            Global.Add(GlobalEquivalentDomainsType.Discover, new List<string> { "discover.com", "discovercard.com" });
            Global.Add(GlobalEquivalentDomainsType.Dcu, new List<string> { "dcu.org", "dcu-online.org" });
            Global.Add(GlobalEquivalentDomainsType.Healthcare, new List<string> { "healthcare.gov", "cms.gov" });
            Global.Add(GlobalEquivalentDomainsType.Pepco, new List<string> { "pepco.com", "pepcoholdings.com" });
            Global.Add(GlobalEquivalentDomainsType.Century21, new List<string> { "century21.com", "21online.com" });
            Global.Add(GlobalEquivalentDomainsType.Comcast, new List<string> { "comcast.com", "comcast.net", "xfinity.com" });
            Global.Add(GlobalEquivalentDomainsType.Cricket, new List<string> { "cricketwireless.com", "aiowireless.com" });
            Global.Add(GlobalEquivalentDomainsType.Mtb, new List<string> { "mandtbank.com", "mtb.com" });
            Global.Add(GlobalEquivalentDomainsType.Dropbox, new List<string> { "dropbox.com", "getdropbox.com" });
            Global.Add(GlobalEquivalentDomainsType.Snapfish, new List<string> { "snapfish.com", "snapfish.ca" });
            Global.Add(GlobalEquivalentDomainsType.Alibaba, new List<string> { "alibaba.com", "aliexpress.com", "aliyun.com", "net.cn", "www.net.cn" });
            Global.Add(GlobalEquivalentDomainsType.Playstation, new List<string> { "playstation.com", "sonyentertainmentnetwork.com" });
            Global.Add(GlobalEquivalentDomainsType.Mercado, new List<string> { "mercadolivre.com", "mercadolivre.com.br", "mercadolibre.com", "mercadolibre.com.ar", "mercadolibre.com.mx" });
            Global.Add(GlobalEquivalentDomainsType.Zendesk, new List<string> { "zendesk.com", "zopim.com" });
            Global.Add(GlobalEquivalentDomainsType.Autodesk, new List<string> { "autodesk.com", "tinkercad.com" });
            Global.Add(GlobalEquivalentDomainsType.RailNation, new List<string> { "railnation.ru", "railnation.de", "rail-nation.com", "railnation.gr", "railnation.us", "trucknation.de", "traviangames.com" });
            Global.Add(GlobalEquivalentDomainsType.Wpcu, new List<string> { "wpcu.coop", "wpcuonline.com" });
            Global.Add(GlobalEquivalentDomainsType.Mathletics, new List<string> { "mathletics.com", "mathletics.com.au", "mathletics.co.uk" });
            Global.Add(GlobalEquivalentDomainsType.Discountbank, new List<string> { "discountbank.co.il", "telebank.co.il" });
            Global.Add(GlobalEquivalentDomainsType.Mi, new List<string> { "mi.com", "xiaomi.com" });
            Global.Add(GlobalEquivalentDomainsType.Postepay, new List<string> { "postepay.it", "poste.it" });
            Global.Add(GlobalEquivalentDomainsType.Facebook, new List<string> { "facebook.com", "messenger.com" });
            Global.Add(GlobalEquivalentDomainsType.Skysports, new List<string> { "skysports.com", "skybet.com", "skyvegas.com" });
            Global.Add(GlobalEquivalentDomainsType.Disney, new List<string> { "disneymoviesanywhere.com", "go.com", "disney.com", "dadt.com" });
            Global.Add(GlobalEquivalentDomainsType.Pokemon, new List<string> { "pokemon-gl.com", "pokemon.com" });
            Global.Add(GlobalEquivalentDomainsType.Uv, new List<string> { "myuv.com", "uvvu.com" });
            Global.Add(GlobalEquivalentDomainsType.Mdsol, new List<string> { "mdsol.com", "imedidata.com" });
            Global.Add(GlobalEquivalentDomainsType.Yahavo, new List<string> { "bank-yahav.co.il", "bankhapoalim.co.il" });
            Global.Add(GlobalEquivalentDomainsType.Sears, new List<string> { "sears.com", "shld.net" });
            Global.Add(GlobalEquivalentDomainsType.Xiami, new List<string> { "xiami.com", "alipay.com" });
            Global.Add(GlobalEquivalentDomainsType.Belkin, new List<string> { "belkin.com", "seedonk.com" });
            Global.Add(GlobalEquivalentDomainsType.Turbotax, new List<string> { "turbotax.com", "intuit.com" });
            Global.Add(GlobalEquivalentDomainsType.Shopify, new List<string> { "shopify.com", "myshopify.com" });
            Global.Add(GlobalEquivalentDomainsType.Ebay, new List<string> { "ebay.com", "ebay.de", "ebay.ca", "ebay.in", "ebay.co.uk", "ebay.com.au" });
            Global.Add(GlobalEquivalentDomainsType.Techdata, new List<string> { "techdata.com", "techdata.ch" });
            Global.Add(GlobalEquivalentDomainsType.Schwab, new List<string> { "schwab.com", "schwabplan.com" });
            Global.Add(GlobalEquivalentDomainsType.Mozilla, new List<string> { "firefox.com", "mozilla.org" });
            Global.Add(GlobalEquivalentDomainsType.Tesla, new List<string> { "tesla.com", "teslamotors.com" });
            Global.Add(GlobalEquivalentDomainsType.MorganStanley, new List<string> { "morganstanley.com", "morganstanleyclientserv.com", "stockplanconnect.com", "ms.com" });
            Global.Add(GlobalEquivalentDomainsType.TaxAct, new List<string> { "taxact.com", "taxactonline.com" });
        }

        public static IDictionary<GlobalEquivalentDomainsType, IEnumerable<string>> Global { get; set; }

    }
}
