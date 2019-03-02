using Bit.Core.Enums;
using Bit.Core.Models.StaticStore;
using System.Collections.Generic;

namespace Bit.Core.Utilities
{
    public class StaticStore
    {
        static StaticStore()
        {
            #region Global Domains

            GlobalDomains = new Dictionary<GlobalEquivalentDomainsType, IEnumerable<string>>();

            GlobalDomains.Add(GlobalEquivalentDomainsType.Ameritrade, new List<string> { "ameritrade.com", "tdameritrade.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.BoA, new List<string> { "bankofamerica.com", "bofa.com", "mbna.com", "usecfo.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Sprint, new List<string> { "sprint.com", "sprintpcs.com", "nextel.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Google, new List<string> { "youtube.com", "google.com", "gmail.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Apple, new List<string> { "apple.com", "icloud.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.WellsFargo, new List<string> { "wellsfargo.com", "wf.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Merrill, new List<string> { "mymerrill.com", "ml.com", "merrilledge.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Citi, new List<string> { "accountonline.com", "citi.com", "citibank.com", "citicards.com", "citibankonline.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Cnet, new List<string> { "cnet.com", "cnettv.com", "com.com", "download.com", "news.com", "search.com", "upload.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Gap, new List<string> { "bananarepublic.com", "gap.com", "oldnavy.com", "piperlime.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Microsoft, new List<string> { "bing.com", "hotmail.com", "live.com", "microsoft.com", "msn.com", "passport.net", "windows.com", "microsoftonline.com", "office365.com", "microsoftstore.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.United, new List<string> { "ua2go.com", "ual.com", "united.com", "unitedwifi.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Yahoo, new List<string> { "overture.com", "yahoo.com", "flickr.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Zonelabs, new List<string> { "zonealarm.com", "zonelabs.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.PayPal, new List<string> { "paypal.com", "paypal-search.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Avon, new List<string> { "avon.com", "youravon.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Diapers, new List<string> { "diapers.com", "soap.com", "wag.com", "yoyo.com", "beautybar.com", "casa.com", "afterschool.com", "vine.com", "bookworm.com", "look.com", "vinemarket.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Contacts, new List<string> { "1800contacts.com", "800contacts.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Amazon, new List<string> { "amazon.com", "amazon.co.uk", "amazon.ca", "amazon.de", "amazon.fr", "amazon.es", "amazon.it", "amazon.com.au", "amazon.co.nz", "amazon.in" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Cox, new List<string> { "cox.com", "cox.net", "coxbusiness.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Norton, new List<string> { "mynortonaccount.com", "norton.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Verizon, new List<string> { "verizon.com", "verizon.net" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Buy, new List<string> { "rakuten.com", "buy.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Sirius, new List<string> { "siriusxm.com", "sirius.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Ea, new List<string> { "ea.com", "origin.com", "play4free.com", "tiberiumalliance.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Basecamp, new List<string> { "37signals.com", "basecamp.com", "basecamphq.com", "highrisehq.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Steam, new List<string> { "steampowered.com", "steamcommunity.com", "steamgames.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Chart, new List<string> { "chart.io", "chartio.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Gotomeeting, new List<string> { "gotomeeting.com", "citrixonline.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Gogo, new List<string> { "gogoair.com", "gogoinflight.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Oracle, new List<string> { "mysql.com", "oracle.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Discover, new List<string> { "discover.com", "discovercard.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Dcu, new List<string> { "dcu.org", "dcu-online.org" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Healthcare, new List<string> { "healthcare.gov", "cms.gov" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Pepco, new List<string> { "pepco.com", "pepcoholdings.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Century21, new List<string> { "century21.com", "21online.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Comcast, new List<string> { "comcast.com", "comcast.net", "xfinity.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Cricket, new List<string> { "cricketwireless.com", "aiowireless.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Mtb, new List<string> { "mandtbank.com", "mtb.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Dropbox, new List<string> { "dropbox.com", "getdropbox.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Snapfish, new List<string> { "snapfish.com", "snapfish.ca" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Alibaba, new List<string> { "alibaba.com", "aliexpress.com", "aliyun.com", "net.cn", "www.net.cn" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Playstation, new List<string> { "playstation.com", "sonyentertainmentnetwork.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Mercado, new List<string> { "mercadolivre.com", "mercadolivre.com.br", "mercadolibre.com", "mercadolibre.com.ar", "mercadolibre.com.mx" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Zendesk, new List<string> { "zendesk.com", "zopim.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Autodesk, new List<string> { "autodesk.com", "tinkercad.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.RailNation, new List<string> { "railnation.ru", "railnation.de", "rail-nation.com", "railnation.gr", "railnation.us", "trucknation.de", "traviangames.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Wpcu, new List<string> { "wpcu.coop", "wpcuonline.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Mathletics, new List<string> { "mathletics.com", "mathletics.com.au", "mathletics.co.uk" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Discountbank, new List<string> { "discountbank.co.il", "telebank.co.il" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Mi, new List<string> { "mi.com", "xiaomi.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Postepay, new List<string> { "postepay.it", "poste.it" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Facebook, new List<string> { "facebook.com", "messenger.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Skysports, new List<string> { "skysports.com", "skybet.com", "skyvegas.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Disney, new List<string> { "disneymoviesanywhere.com", "go.com", "disney.com", "dadt.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Pokemon, new List<string> { "pokemon-gl.com", "pokemon.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Uv, new List<string> { "myuv.com", "uvvu.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Mdsol, new List<string> { "mdsol.com", "imedidata.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Yahavo, new List<string> { "bank-yahav.co.il", "bankhapoalim.co.il" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Sears, new List<string> { "sears.com", "shld.net" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Xiami, new List<string> { "xiami.com", "alipay.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Belkin, new List<string> { "belkin.com", "seedonk.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Turbotax, new List<string> { "turbotax.com", "intuit.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Shopify, new List<string> { "shopify.com", "myshopify.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Ebay, new List<string> { "ebay.com", "ebay.de", "ebay.ca", "ebay.in", "ebay.co.uk", "ebay.com.au" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Techdata, new List<string> { "techdata.com", "techdata.ch" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Schwab, new List<string> { "schwab.com", "schwabplan.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Tesla, new List<string> { "tesla.com", "teslamotors.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.MorganStanley, new List<string> { "morganstanley.com", "morganstanleyclientserv.com", "stockplanconnect.com", "ms.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.TaxAct, new List<string> { "taxact.com", "taxactonline.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Wikimedia, new List<string> { "mediawiki.org", "wikibooks.org", "wikidata.org", "wikimedia.org", "wikinews.org", "wikipedia.org", "wikiquote.org", "wikisource.org", "wikiversity.org", "wikivoyage.org", "wiktionary.org", });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Airbnb, new List<string> { "airbnb.at", "airbnb.be", "airbnb.ca", "airbnb.ch", "airbnb.cl", "airbnb.co.cr", "airbnb.co.id", "airbnb.co.in", "airbnb.co.kr", "airbnb.co.nz", "airbnb.co.uk", "airbnb.co.ve", "airbnb.com", "airbnb.com.ar", "airbnb.com.au", "airbnb.com.bo", "airbnb.com.br", "airbnb.com.bz", "airbnb.com.co", "airbnb.com.ec", "airbnb.com.gt", "airbnb.com.hk", "airbnb.com.hn", "airbnb.com.mt", "airbnb.com.my", "airbnb.com.ni", "airbnb.com.pa", "airbnb.com.pe", "airbnb.com.py", "airbnb.com.sg", "airbnb.com.sv", "airbnb.com.tr", "airbnb.com.tw", "airbnb.cz", "airbnb.de", "airbnb.dk", "airbnb.es", "airbnb.fi", "airbnb.fr", "airbnb.gr", "airbnb.gy", "airbnb.hu", "airbnb.ie", "airbnb.is", "airbnb.it", "airbnb.jp", "airbnb.mx", "airbnb.nl", "airbnb.no", "airbnb.pl", "airbnb.pt", "airbnb.ru", "airbnb.se" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Eventbrite, new List<string> { "eventbrite.at", "eventbrite.be", "eventbrite.ca", "eventbrite.ch", "eventbrite.cl", "eventbrite.co.id", "eventbrite.co.in", "eventbrite.co.kr", "eventbrite.co.nz", "eventbrite.co.uk", "eventbrite.co.ve", "eventbrite.com", "eventbrite.com.au", "eventbrite.com.bo", "eventbrite.com.br", "eventbrite.com.co", "eventbrite.com.hk", "eventbrite.com.hn", "eventbrite.com.pe", "eventbrite.com.sg", "eventbrite.com.tr", "eventbrite.com.tw", "eventbrite.cz", "eventbrite.de", "eventbrite.dk", "eventbrite.fi", "eventbrite.fr", "eventbrite.gy", "eventbrite.hu", "eventbrite.ie", "eventbrite.is", "eventbrite.it", "eventbrite.jp", "eventbrite.mx", "eventbrite.nl", "eventbrite.no", "eventbrite.pl", "eventbrite.pt", "eventbrite.ru", "eventbrite.se" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.StackExchange, new List<string> { "stackexchange.com", "superuser.com", "stackoverflow.com", "serverfault.com", "mathoverflow.net" });

            #endregion

            #region Plans

            Plans = new List<Plan>
            {
                new Plan
                {
                    Type = PlanType.Free,
                    BaseSeats = 2,
                    CanBuyAdditionalSeats = false,
                    MaxCollections = 2,
                    Name = "Free",
                    UpgradeSortOrder = -1 // Always the lowest plan, cannot be upgraded to
                },
                new Plan
                {
                    Type = PlanType.FamiliesAnnually,
                    BaseSeats = 5,
                    BasePrice = 12,
                    CanBuyAdditionalSeats = false,
                    CanBuyPremiumAccessAddon = true,
                    Name = "Families",
                    StripePlanId = "personal-org-annually",
                    StripeStoragePlanId = "storage-gb-annually",
                    StripePremiumAccessPlanId = "personal-org-premium-access-annually",
                    UpgradeSortOrder = 1,
                    TrialPeriodDays = 7,
                    UseTotp = true,
                    MaxStorageGb = 1,
                    SelfHost = true
                },
                new Plan
                {
                    Type = PlanType.TeamsMonthly,
                    BaseSeats = 5,
                    BasePrice = 8,
                    SeatPrice = 2.5M,
                    CanBuyAdditionalSeats = true,
                    Name = "Teams (Monthly)",
                    StripePlanId = "teams-org-monthly",
                    StripeSeatPlanId = "teams-org-seat-monthly",
                    StripeStoragePlanId = "storage-gb-monthly",
                    UpgradeSortOrder = 2,
                    TrialPeriodDays = 7,
                    UseTotp = true,
                    MaxStorageGb = 1
                },
                new Plan
                {
                    Type = PlanType.TeamsAnnually,
                    BaseSeats = 5,
                    BasePrice = 60,
                    SeatPrice = 24,
                    CanBuyAdditionalSeats = true,
                    Name = "Teams (Annually)",
                    StripePlanId = "teams-org-annually",
                    StripeSeatPlanId = "teams-org-seat-annually",
                    StripeStoragePlanId = "storage-gb-annually",
                    UpgradeSortOrder = 2,
                    TrialPeriodDays = 7,
                    UseTotp = true,
                    MaxStorageGb = 1
                },
                new Plan
                {
                    Type = PlanType.EnterpriseMonthly,
                    BaseSeats = 0,
                    BasePrice = 0,
                    SeatPrice = 4M,
                    CanBuyAdditionalSeats = true,
                    Name = "Enterprise (Monthly)",
                    StripePlanId = null,
                    StripeSeatPlanId = "enterprise-org-seat-monthly",
                    StripeStoragePlanId = "storage-gb-monthly",
                    UpgradeSortOrder = 3,
                    TrialPeriodDays = 7,
                    UseGroups = true,
                    UseDirectory = true,
                    UseEvents = true,
                    UseTotp = true,
                    Use2fa = true,
                    UseApi = true,
                    MaxStorageGb = 1,
                    SelfHost = true,
                    UsersGetPremium = true
                },
                new Plan
                {
                    Type = PlanType.EnterpriseAnnually,
                    BaseSeats = 0,
                    BasePrice = 0,
                    SeatPrice = 36,
                    CanBuyAdditionalSeats = true,
                    Name = "Enterprise (Annually)",
                    StripePlanId = null,
                    StripeSeatPlanId = "enterprise-org-seat-annually",
                    StripeStoragePlanId = "storage-gb-annually",
                    UpgradeSortOrder = 3,
                    TrialPeriodDays = 7,
                    UseGroups = true,
                    UseDirectory = true,
                    UseEvents = true,
                    UseTotp = true,
                    Use2fa = true,
                    UseApi = true,
                    MaxStorageGb = 1,
                    SelfHost = true,
                    UsersGetPremium = true
                }
            };

            #endregion
        }

        public static IDictionary<GlobalEquivalentDomainsType, IEnumerable<string>> GlobalDomains { get; set; }
        public static IEnumerable<Plan> Plans { get; set; }
    }
}
