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
            GlobalDomains.Add(GlobalEquivalentDomainsType.Google, new List<string> { "youtube.com", "google.com", "gmail.com", "google.ad", "google.ae", "google.com.af", "google.com.ag", "google.com.ai", "google.al", "google.am", "google.co.ao", "google.com.ar", "google.as", "google.at", "google.com.au", "google.az", "google.ba", "google.com.bd", "google.be", "google.bf", "google.bg", "google.com.bh", "google.bi", "google.bj", "google.com.bn", "google.com.bo", "google.com.br", "google.bs", "google.bt", "google.co.bw", "google.by", "google.com.bz", "google.ca", "google.cd", "google.cf", "google.cg", "google.ch", "google.ci", "google.co.ck", "google.cl", "google.cm", "google.cn", "google.com.co", "google.co.cr", "google.com.cu", "google.cv", "google.com.cy", "google.cz", "google.de", "google.dj", "google.dk", "google.dm", "google.com.do", "google.dz", "google.com.ec", "google.ee", "google.com.eg", "google.es", "google.com.et", "google.fi", "google.com.fj", "google.fm", "google.fr", "google.ga", "google.ge", "google.gg", "google.com.gh", "google.com.gi", "google.gl", "google.gm", "google.gp", "google.gr", "google.com.gt", "google.gy", "google.com.hk", "google.hn", "google.hr", "google.ht", "google.hu", "google.co.id", "google.ie", "google.co.il", "google.im", "google.co.in", "google.iq", "google.is", "google.it", "google.je", "google.com.jm", "google.jo", "google.co.jp", "google.co.ke", "google.com.kh", "google.ki", "google.kg", "google.co.kr", "google.com.kw", "google.kz", "google.la", "google.com.lb", "google.li", "google.lk", "google.co.ls", "google.lt", "google.lu", "google.lv", "google.com.ly", "google.co.ma", "google.md", "google.me", "google.mg", "google.mk", "google.ml", "google.com.mm", "google.mn", "google.ms", "google.com.mt", "google.mu", "google.mv", "google.mw", "google.com.mx", "google.com.my", "google.co.mz", "google.com.na", "google.com.nf", "google.com.ng", "google.com.ni", "google.ne", "google.nl", "google.no", "google.com.np", "google.nr", "google.nu", "google.co.nz", "google.com.om", "google.com.pa", "google.com.pe", "google.com.pg", "google.com.ph", "google.com.pk", "google.pl", "google.pn", "google.com.pr", "google.ps", "google.pt", "google.com.py", "google.com.qa", "google.ro", "google.ru", "google.rw", "google.com.sa", "google.com.sb", "google.sc", "google.se", "google.com.sg", "google.sh", "google.si", "google.sk", "google.com.sl", "google.sn", "google.so", "google.sm", "google.sr", "google.st", "google.com.sv", "google.td", "google.tg", "google.co.th", "google.com.tj", "google.tk", "google.tl", "google.tm", "google.tn", "google.to", "google.com.tr", "google.tt", "google.com.tw", "google.co.tz", "google.com.ua", "google.co.ug", "google.co.uk", "google.com.uy", "google.co.uz", "google.com.vc", "google.co.ve", "google.vg", "google.co.vi", "google.com.vn", "google.vu", "google.ws", "google.rs", "google.co.za", "google.co.zm", "google.co.zw", "google.cat", });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Apple, new List<string> { "apple.com", "icloud.com", "androidapp://com.apple.android.music", });
            GlobalDomains.Add(GlobalEquivalentDomainsType.WellsFargo, new List<string> { "wellsfargo.com", "wf.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Merrill, new List<string> { "mymerrill.com", "ml.com", "merrilledge.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Citi, new List<string> { "accountonline.com", "citi.com", "citibank.com", "citicards.com", "citibankonline.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Cnet, new List<string> { "cnet.com", "cnettv.com", "com.com", "download.com", "news.com", "search.com", "upload.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Gap, new List<string> { "bananarepublic.com", "gap.com", "oldnavy.com", "piperlime.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Microsoft, new List<string> { "bing.com", "hotmail.com", "live.com", "microsoft.com", "msn.com", "passport.net", "windows.com", "microsoftonline.com", "office365.com", "microsoftstore.com", "androidapp://com.microsoft.skydrive", "androidapp://com.microsoft.office.word", "androidapp://com.microsoft.office.onenote", "androidapp://com.microsoft.office.excel", "androidapp://com.microsoft.microsoftsolitairecollection", "androidapp://com.microsoft.xboxone.smartglass" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.United, new List<string> { "ua2go.com", "ual.com", "united.com", "unitedwifi.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Yahoo, new List<string> { "overture.com", "yahoo.com", "flickr.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Zonelabs, new List<string> { "zonealarm.com", "zonelabs.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Paypal, new List<string> { "paypal.com", "paypal-search.com", "paypal.me", "androidapp://com.paypal.android.p2pmobile" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Avon, new List<string> { "avon.com", "youravon.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Diapers, new List<string> { "diapers.com", "soap.com", "wag.com", "yoyo.com", "beautybar.com", "casa.com", "afterschool.com", "vine.com", "bookworm.com", "look.com", "vinemarket.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Contacts, new List<string> { "1800contacts.com", "800contacts.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Amazon, new List<string> { "amazon.com", "amazon.co.uk", "amazon.ca", "amazon.de", "amazon.fr", "amazon.es", "amazon.it", "amazon.com.au", "amazon.co.nz", "amazon.co.jp", "amazon.in", "androidapp://com.amazon.mShop.android.shopping", "androidapp://com.amazon.windowshop", "androidapp://com.amazon.avod.thirdpartyclient", "androidapp://com.amazon.kindle", "androidapp://com.amazon.dee.app", "androidapp://com.audible.application", "androidapp://com.amazon.drive", "androidapp://com.amazon.clouddrive.photos" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Cox, new List<string> { "cox.com", "cox.net", "coxbusiness.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Norton, new List<string> { "mynortonaccount.com", "norton.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Verizon, new List<string> { "verizon.com", "verizon.net" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Buy, new List<string> { "rakuten.com", "buy.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Sirius, new List<string> { "siriusxm.com", "sirius.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Ea, new List<string> { "ea.com", "origin.com", "play4free.com", "tiberiumalliance.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Basecamp, new List<string> { "37signals.com", "basecamp.com", "basecamphq.com", "highrisehq.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Steam, new List<string> { "steampowered.com", "steamcommunity.com", "androidapp://com.valvesoftware.android.steam.community", });
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
            GlobalDomains.Add(GlobalEquivalentDomainsType.Dropbox, new List<string> { "dropbox.com", "getdropbox.com", "androidapp://com.dropbox.android", "androidapp://com.dropbox.paper", });
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
            GlobalDomains.Add(GlobalEquivalentDomainsType.Facebook, new List<string> { "facebook.com", "messenger.com", "androidapp:com.facebook.katana//", "androidapp://com.facebook.orca", "androidapp://com.facebook.lite", "androidapp:com.facebook.pages.app//", "androidapp://com.facebook.mlite", "androidapp://com.facebook.moments" });
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
            GlobalDomains.Add(GlobalEquivalentDomainsType.Ebay, new List<string> { "ebay.com", "ebay.de", "ebay.ca", "ebay.in", "ebay.co.uk", "ebay.com.au", "www.ebay.ie/", "androidapp://com.ebay.mobile" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Techdata, new List<string> { "techdata.com", "techdata.ch" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Schwab, new List<string> { "schwab.com", "schwabplan.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Tesla, new List<string> { "tesla.com", "teslamotors.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.MorganStanley, new List<string> { "morganstanley.com", "morganstanleyclientserv.com", "stockplanconnect.com", "ms.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.TaxAct, new List<string> { "taxact.com", "taxactonline.com" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Wikimedia, new List<string> { "mediawiki.org", "wikibooks.org", "wikidata.org", "wikimedia.org", "wikinews.org", "wikipedia.org", "wikiquote.org", "wikisource.org", "wikiversity.org", "wikivoyage.org", "wiktionary.org", });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Airbnb, new List<string> { "airbnb.at", "airbnb.be", "airbnb.ca", "airbnb.ch", "airbnb.cl", "airbnb.co.cr", "airbnb.co.id", "airbnb.co.in", "airbnb.co.kr", "airbnb.co.nz", "airbnb.co.uk", "airbnb.co.ve", "airbnb.com", "airbnb.com.ar", "airbnb.com.au", "airbnb.com.bo", "airbnb.com.br", "airbnb.com.bz", "airbnb.com.co", "airbnb.com.ec", "airbnb.com.gt", "airbnb.com.hk", "airbnb.com.hn", "airbnb.com.mt", "airbnb.com.my", "airbnb.com.ni", "airbnb.com.pa", "airbnb.com.pe", "airbnb.com.py", "airbnb.com.sg", "airbnb.com.sv", "airbnb.com.tr", "airbnb.com.tw", "airbnb.cz", "airbnb.de", "airbnb.dk", "airbnb.es", "airbnb.fi", "airbnb.fr", "airbnb.gr", "airbnb.gy", "airbnb.hu", "airbnb.ie", "airbnb.is", "airbnb.it", "airbnb.jp", "airbnb.mx", "airbnb.nl", "airbnb.no", "airbnb.pl", "airbnb.pt", "airbnb.ru", "airbnb.se" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Eventbrite, new List<string> { "eventbrite.at", "eventbrite.be", "eventbrite.ca", "eventbrite.ch", "eventbrite.cl", "eventbrite.co.id", "eventbrite.co.in", "eventbrite.co.kr", "eventbrite.co.nz", "eventbrite.co.uk", "eventbrite.co.ve", "eventbrite.com", "eventbrite.com.au", "eventbrite.com.bo", "eventbrite.com.br", "eventbrite.com.co", "eventbrite.com.hk", "eventbrite.com.hn", "eventbrite.com.pe", "eventbrite.com.sg", "eventbrite.com.tr", "eventbrite.com.tw", "eventbrite.cz", "eventbrite.de", "eventbrite.dk", "eventbrite.fi", "eventbrite.fr", "eventbrite.gy", "eventbrite.hu", "eventbrite.ie", "eventbrite.is", "eventbrite.it", "eventbrite.jp", "eventbrite.mx", "eventbrite.nl", "eventbrite.no", "eventbrite.pl", "eventbrite.pt", "eventbrite.ru", "eventbrite.se" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.StackExchange, new List<string> { "stackexchange.com", "superuser.com", "stackoverflow.com", "serverfault.com", "mathoverflow.net", });
            GlobalDomains.Add(GlobalEquivalentDomainsType.WordPress, new List<string> { "wordpress.com", "androidapp://org.wordpress.android" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.Patreon, new List<string> { "patreon.com", "androidapp://com.patreon.android" });
            GlobalDomains.Add(GlobalEquivalentDomainsType.SchibstedNorway, new List<string> { "finn.no", "vg.no", "e24.no", "fvn.no", "schibsted.no", "schibsted.com", "schibstedpayment.com", "androidapp://no.finn.android", "androidapp://com.agens.android.vgsnarvei", "androidapp://no.e24dinepenger.e24", "androidapp://no.vektklubb.android", "androidapp://no.fvn", });
            GlobalDomains.Add(GlobalEquivalentDomainsType.BankIDNorway, new List<string> { "portalbank.no", "difi.no", "coop.no", "3dsecure.no", });
                                                                                           

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
                    Name = "Families",
                    StripePlanId = "personal-org-annually",
                    StripStoragePlanId = "storage-gb-annually",
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
                    StripStoragePlanId = "storage-gb-monthly",
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
                    StripStoragePlanId = "storage-gb-annually",
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
                    StripStoragePlanId = "storage-gb-monthly",
                    UpgradeSortOrder = 3,
                    TrialPeriodDays = 7,
                    UseGroups = true,
                    UseDirectory = true,
                    UseEvents = true,
                    UseTotp = true,
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
                    StripStoragePlanId = "storage-gb-annually",
                    UpgradeSortOrder = 3,
                    TrialPeriodDays = 7,
                    UseGroups = true,
                    UseDirectory = true,
                    UseEvents = true,
                    UseTotp = true,
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
