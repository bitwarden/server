using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Utilities;

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
        GlobalDomains.Add(GlobalEquivalentDomainsType.WellsFargo, new List<string> { "wellsfargo.com", "wf.com", "wellsfargoadvisors.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Merrill, new List<string> { "mymerrill.com", "ml.com", "merrilledge.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Citi, new List<string> { "accountonline.com", "citi.com", "citibank.com", "citicards.com", "citibankonline.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Cnet, new List<string> { "cnet.com", "cnettv.com", "com.com", "download.com", "news.com", "search.com", "upload.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Gap, new List<string> { "bananarepublic.com", "gap.com", "oldnavy.com", "piperlime.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Microsoft, new List<string> { "bing.com", "hotmail.com", "live.com", "microsoft.com", "msn.com", "passport.net", "windows.com", "microsoftonline.com", "office.com", "office365.com", "microsoftstore.com", "xbox.com", "azure.com", "windowsazure.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.United, new List<string> { "ua2go.com", "ual.com", "united.com", "unitedwifi.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Yahoo, new List<string> { "overture.com", "yahoo.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Zonelabs, new List<string> { "zonealarm.com", "zonelabs.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.PayPal, new List<string> { "paypal.com", "paypal-search.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Avon, new List<string> { "avon.com", "youravon.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Diapers, new List<string> { "diapers.com", "soap.com", "wag.com", "yoyo.com", "beautybar.com", "casa.com", "afterschool.com", "vine.com", "bookworm.com", "look.com", "vinemarket.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Contacts, new List<string> { "1800contacts.com", "800contacts.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Amazon, new List<string> { "amazon.com", "amazon.com.be", "amazon.ae", "amazon.ca", "amazon.co.uk", "amazon.com.au", "amazon.com.br", "amazon.com.mx", "amazon.com.tr", "amazon.de", "amazon.es", "amazon.fr", "amazon.in", "amazon.it", "amazon.nl", "amazon.pl", "amazon.sa", "amazon.se", "amazon.sg" });
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
        GlobalDomains.Add(GlobalEquivalentDomainsType.Healthcare, new List<string> { "healthcare.gov", "cuidadodesalud.gov", "cms.gov" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Pepco, new List<string> { "pepco.com", "pepcoholdings.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Century21, new List<string> { "century21.com", "21online.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Comcast, new List<string> { "comcast.com", "comcast.net", "xfinity.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Cricket, new List<string> { "cricketwireless.com", "aiowireless.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Mtb, new List<string> { "mandtbank.com", "mtb.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Dropbox, new List<string> { "dropbox.com", "getdropbox.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Snapfish, new List<string> { "snapfish.com", "snapfish.ca" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Alibaba, new List<string> { "alibaba.com", "aliexpress.com", "aliyun.com", "net.cn" });
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
        GlobalDomains.Add(GlobalEquivalentDomainsType.Disney, new List<string> { "disneymoviesanywhere.com", "go.com", "disney.com", "dadt.com", "disneyplus.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Pokemon, new List<string> { "pokemon-gl.com", "pokemon.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Uv, new List<string> { "myuv.com", "uvvu.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Mdsol, new List<string> { "mdsol.com", "imedidata.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Yahavo, new List<string> { "bank-yahav.co.il", "bankhapoalim.co.il" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Sears, new List<string> { "sears.com", "shld.net" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Xiami, new List<string> { "xiami.com", "alipay.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Belkin, new List<string> { "belkin.com", "seedonk.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Turbotax, new List<string> { "turbotax.com", "intuit.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Shopify, new List<string> { "shopify.com", "myshopify.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Ebay, new List<string> { "ebay.com", "ebay.at", "ebay.be", "ebay.ca", "ebay.ch", "ebay.cn", "ebay.co.jp", "ebay.co.th", "ebay.co.uk", "ebay.com.au", "ebay.com.hk", "ebay.com.my", "ebay.com.sg", "ebay.com.tw", "ebay.de", "ebay.es", "ebay.fr", "ebay.ie", "ebay.in", "ebay.it", "ebay.nl", "ebay.ph", "ebay.pl" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Techdata, new List<string> { "techdata.com", "techdata.ch" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Schwab, new List<string> { "schwab.com", "schwabplan.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Tesla, new List<string> { "tesla.com", "teslamotors.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.MorganStanley, new List<string> { "morganstanley.com", "morganstanleyclientserv.com", "stockplanconnect.com", "ms.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.TaxAct, new List<string> { "taxact.com", "taxactonline.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Wikimedia, new List<string> { "mediawiki.org", "wikibooks.org", "wikidata.org", "wikimedia.org", "wikinews.org", "wikipedia.org", "wikiquote.org", "wikisource.org", "wikiversity.org", "wikivoyage.org", "wiktionary.org" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Airbnb, new List<string> { "airbnb.at", "airbnb.be", "airbnb.ca", "airbnb.ch", "airbnb.cl", "airbnb.co.cr", "airbnb.co.id", "airbnb.co.in", "airbnb.co.kr", "airbnb.co.nz", "airbnb.co.uk", "airbnb.co.ve", "airbnb.com", "airbnb.com.ar", "airbnb.com.au", "airbnb.com.bo", "airbnb.com.br", "airbnb.com.bz", "airbnb.com.co", "airbnb.com.ec", "airbnb.com.gt", "airbnb.com.hk", "airbnb.com.hn", "airbnb.com.mt", "airbnb.com.my", "airbnb.com.ni", "airbnb.com.pa", "airbnb.com.pe", "airbnb.com.py", "airbnb.com.sg", "airbnb.com.sv", "airbnb.com.tr", "airbnb.com.tw", "airbnb.cz", "airbnb.de", "airbnb.dk", "airbnb.es", "airbnb.fi", "airbnb.fr", "airbnb.gr", "airbnb.gy", "airbnb.hu", "airbnb.ie", "airbnb.is", "airbnb.it", "airbnb.jp", "airbnb.mx", "airbnb.nl", "airbnb.no", "airbnb.pl", "airbnb.pt", "airbnb.ru", "airbnb.se" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Eventbrite, new List<string> { "eventbrite.at", "eventbrite.be", "eventbrite.ca", "eventbrite.ch", "eventbrite.cl", "eventbrite.co", "eventbrite.co.nz", "eventbrite.co.uk", "eventbrite.com", "eventbrite.com.ar", "eventbrite.com.au", "eventbrite.com.br", "eventbrite.com.mx", "eventbrite.com.pe", "eventbrite.de", "eventbrite.dk", "eventbrite.es", "eventbrite.fi", "eventbrite.fr", "eventbrite.hk", "eventbrite.ie", "eventbrite.it", "eventbrite.nl", "eventbrite.pt", "eventbrite.se", "eventbrite.sg" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.StackExchange, new List<string> { "stackexchange.com", "superuser.com", "stackoverflow.com", "serverfault.com", "mathoverflow.net", "askubuntu.com", "stackapps.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Docusign, new List<string> { "docusign.com", "docusign.net" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Envato, new List<string> { "envato.com", "themeforest.net", "codecanyon.net", "videohive.net", "audiojungle.net", "graphicriver.net", "photodune.net", "3docean.net" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.X10Hosting, new List<string> { "x10hosting.com", "x10premium.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Cisco, new List<string> { "dnsomatic.com", "opendns.com", "umbrella.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.CedarFair, new List<string> { "cagreatamerica.com", "canadaswonderland.com", "carowinds.com", "cedarfair.com", "cedarpoint.com", "dorneypark.com", "kingsdominion.com", "knotts.com", "miadventure.com", "schlitterbahn.com", "valleyfair.com", "visitkingsisland.com", "worldsoffun.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Ubiquiti, new List<string> { "ubnt.com", "ui.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Discord, new List<string> { "discordapp.com", "discord.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Netcup, new List<string> { "netcup.de", "netcup.eu", "customercontrolpanel.de" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Yandex, new List<string> { "yandex.com", "ya.ru", "yandex.az", "yandex.by", "yandex.co.il", "yandex.com.am", "yandex.com.ge", "yandex.com.tr", "yandex.ee", "yandex.fi", "yandex.fr", "yandex.kg", "yandex.kz", "yandex.lt", "yandex.lv", "yandex.md", "yandex.pl", "yandex.ru", "yandex.tj", "yandex.tm", "yandex.ua", "yandex.uz" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Sony, new List<string> { "sonyentertainmentnetwork.com", "sony.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Proton, new List<string> { "proton.me", "protonmail.com", "protonvpn.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Ubisoft, new List<string> { "ubisoft.com", "ubi.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.TransferWise, new List<string> { "transferwise.com", "wise.com" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.TakeawayEU, new List<string> { "takeaway.com", "just-eat.dk", "just-eat.no", "just-eat.fr", "just-eat.ch", "lieferando.de", "lieferando.at", "thuisbezorgd.nl", "pyszne.pl" });
        GlobalDomains.Add(GlobalEquivalentDomainsType.Atlassian, new List<string> { "atlassian.com", "bitbucket.org", "trello.com", "statuspage.io", "atlassian.net", "jira.com" });
        #endregion

        #region Plans

        Plans = new List<Plan>
        {
            new Plan
            {
                Type = PlanType.Free,
                Product = ProductType.Free,
                Name = "Free",
                NameLocalizationKey = "planNameFree",
                DescriptionLocalizationKey = "planDescFree",
                BaseSeats = 2,
                MaxCollections = 2,
                MaxUsers = 2,

                UpgradeSortOrder = -1, // Always the lowest plan, cannot be upgraded to
                DisplaySortOrder = -1,

                AllowSeatAutoscale = false,
            },
            new Plan
            {
                Type = PlanType.FamiliesAnnually2019,
                Product = ProductType.Families,
                Name = "Families 2019",
                IsAnnual = true,
                NameLocalizationKey = "planNameFamilies",
                DescriptionLocalizationKey = "planDescFamilies",
                BaseSeats = 5,
                BaseStorageGb = 1,
                MaxUsers = 5,

                HasAdditionalStorageOption = true,
                HasPremiumAccessOption = true,
                TrialPeriodDays = 7,

                HasSelfHost = true,
                HasTotp = true,

                UpgradeSortOrder = 1,
                DisplaySortOrder = 1,
                LegacyYear = 2020,

                StripePlanId = "personal-org-annually",
                StripeStoragePlanId = "storage-gb-annually",
                StripePremiumAccessPlanId = "personal-org-premium-access-annually",
                BasePrice = 12,
                AdditionalStoragePricePerGb = 4,
                PremiumAccessOptionPrice = 40,

                AllowSeatAutoscale = false,
            },
            new Plan
            {
                Type = PlanType.TeamsAnnually2019,
                Product = ProductType.Teams,
                Name = "Teams (Annually) 2019",
                IsAnnual = true,
                NameLocalizationKey = "planNameTeams",
                DescriptionLocalizationKey = "planDescTeams",
                CanBeUsedByBusiness = true,
                BaseSeats = 5,
                BaseStorageGb = 1,

                HasAdditionalSeatsOption = true,
                HasAdditionalStorageOption = true,
                TrialPeriodDays = 7,

                HasTotp = true,

                UpgradeSortOrder = 2,
                DisplaySortOrder = 2,
                LegacyYear = 2020,

                StripePlanId = "teams-org-annually",
                StripeSeatPlanId = "teams-org-seat-annually",
                StripeStoragePlanId = "storage-gb-annually",
                BasePrice = 60,
                SeatPrice = 24,
                AdditionalStoragePricePerGb = 4,

                AllowSeatAutoscale = true,
            },
            new Plan
            {
                Type = PlanType.TeamsMonthly2019,
                Product = ProductType.Teams,
                Name = "Teams (Monthly) 2019",
                NameLocalizationKey = "planNameTeams",
                DescriptionLocalizationKey = "planDescTeams",
                CanBeUsedByBusiness = true,
                BaseSeats = 5,
                BaseStorageGb = 1,

                HasAdditionalSeatsOption = true,
                HasAdditionalStorageOption = true,
                TrialPeriodDays = 7,

                HasTotp = true,

                UpgradeSortOrder = 2,
                DisplaySortOrder = 2,
                LegacyYear = 2020,

                StripePlanId = "teams-org-monthly",
                StripeSeatPlanId = "teams-org-seat-monthly",
                StripeStoragePlanId = "storage-gb-monthly",
                BasePrice = 8,
                SeatPrice = 2.5M,
                AdditionalStoragePricePerGb = 0.5M,

                AllowSeatAutoscale = true,
            },
            new Plan
            {
                Type = PlanType.EnterpriseAnnually2019,
                Name = "Enterprise (Annually) 2019",
                IsAnnual = true,
                Product = ProductType.Enterprise,
                NameLocalizationKey = "planNameEnterprise",
                DescriptionLocalizationKey = "planDescEnterprise",
                CanBeUsedByBusiness = true,
                BaseSeats = 0,
                BaseStorageGb = 1,

                HasAdditionalSeatsOption = true,
                HasAdditionalStorageOption = true,
                TrialPeriodDays = 7,

                HasPolicies = true,
                HasSelfHost = true,
                HasGroups = true,
                HasDirectory = true,
                HasEvents = true,
                HasTotp = true,
                Has2fa = true,
                HasApi = true,
                UsersGetPremium = true,

                UpgradeSortOrder = 3,
                DisplaySortOrder = 3,
                LegacyYear = 2020,

                StripePlanId = null,
                StripeSeatPlanId = "enterprise-org-seat-annually",
                StripeStoragePlanId = "storage-gb-annually",
                BasePrice = 0,
                SeatPrice = 36,
                AdditionalStoragePricePerGb = 4,

                AllowSeatAutoscale = true,
            },
            new Plan
            {
                Type = PlanType.EnterpriseMonthly2019,
                Product = ProductType.Enterprise,
                Name = "Enterprise (Monthly) 2019",
                NameLocalizationKey = "planNameEnterprise",
                DescriptionLocalizationKey = "planDescEnterprise",
                CanBeUsedByBusiness = true,
                BaseSeats = 0,
                BaseStorageGb = 1,

                HasAdditionalSeatsOption = true,
                HasAdditionalStorageOption = true,
                TrialPeriodDays = 7,

                HasPolicies = true,
                HasGroups = true,
                HasDirectory = true,
                HasEvents = true,
                HasTotp = true,
                Has2fa = true,
                HasApi = true,
                HasSelfHost = true,
                UsersGetPremium = true,

                UpgradeSortOrder = 3,
                DisplaySortOrder = 3,
                LegacyYear = 2020,

                StripePlanId = null,
                StripeSeatPlanId = "enterprise-org-seat-monthly",
                StripeStoragePlanId = "storage-gb-monthly",
                BasePrice = 0,
                SeatPrice = 4M,
                AdditionalStoragePricePerGb = 0.5M,

                AllowSeatAutoscale = true,
            },
            new Plan
            {
                Type = PlanType.FamiliesAnnually,
                Product = ProductType.Families,
                Name = "Families",
                IsAnnual = true,
                NameLocalizationKey = "planNameFamilies",
                DescriptionLocalizationKey = "planDescFamilies",
                BaseSeats = 6,
                BaseStorageGb = 1,
                MaxUsers = 6,

                HasAdditionalStorageOption = true,
                TrialPeriodDays = 7,

                HasSelfHost = true,
                HasTotp = true,
                UsersGetPremium = true,

                UpgradeSortOrder = 1,
                DisplaySortOrder = 1,

                StripePlanId = "2020-families-org-annually",
                StripeStoragePlanId = "storage-gb-annually",
                BasePrice = 40,
                AdditionalStoragePricePerGb = 4,

                AllowSeatAutoscale = false,
            },
            new Plan
            {
                Type = PlanType.TeamsAnnually,
                Product = ProductType.Teams,
                Name = "Teams (Annually)",
                IsAnnual = true,
                NameLocalizationKey = "planNameTeams",
                DescriptionLocalizationKey = "planDescTeams",
                CanBeUsedByBusiness = true,
                BaseStorageGb = 1,
                BaseSeats = 0,

                HasAdditionalSeatsOption = true,
                HasAdditionalStorageOption = true,
                TrialPeriodDays = 7,

                Has2fa = true,
                HasApi = true,
                HasDirectory = true,
                HasEvents = true,
                HasGroups = true,
                HasTotp = true,
                UsersGetPremium = true,

                UpgradeSortOrder = 2,
                DisplaySortOrder = 2,

                StripeSeatPlanId = "2020-teams-org-seat-annually",
                StripeStoragePlanId = "storage-gb-annually",
                SeatPrice = 36,
                AdditionalStoragePricePerGb = 4,

                AllowSeatAutoscale = true,
            },
            new Plan
            {
                Type = PlanType.TeamsMonthly,
                Product = ProductType.Teams,
                Name = "Teams (Monthly)",
                NameLocalizationKey = "planNameTeams",
                DescriptionLocalizationKey = "planDescTeams",
                CanBeUsedByBusiness = true,
                BaseStorageGb = 1,
                BaseSeats = 0,

                HasAdditionalSeatsOption = true,
                HasAdditionalStorageOption = true,
                TrialPeriodDays = 7,

                Has2fa = true,
                HasApi = true,
                HasDirectory = true,
                HasEvents = true,
                HasGroups = true,
                HasTotp = true,
                UsersGetPremium = true,

                UpgradeSortOrder = 2,
                DisplaySortOrder = 2,

                StripeSeatPlanId = "2020-teams-org-seat-monthly",
                StripeStoragePlanId = "storage-gb-monthly",
                SeatPrice = 4,
                AdditionalStoragePricePerGb = 0.5M,

                AllowSeatAutoscale = true,
            },
            new Plan
            {
                Type = PlanType.EnterpriseAnnually,
                Name = "Enterprise (Annually)",
                Product = ProductType.Enterprise,
                IsAnnual = true,
                NameLocalizationKey = "planNameEnterprise",
                DescriptionLocalizationKey = "planDescEnterprise",
                CanBeUsedByBusiness = true,
                BaseSeats = 0,
                BaseStorageGb = 1,

                HasAdditionalSeatsOption = true,
                HasAdditionalStorageOption = true,
                TrialPeriodDays = 7,

                HasPolicies = true,
                HasSelfHost = true,
                HasGroups = true,
                HasDirectory = true,
                HasEvents = true,
                HasTotp = true,
                Has2fa = true,
                HasApi = true,
                HasSso = true,
                HasKeyConnector = true,
                HasScim = true,
                HasResetPassword = true,
                UsersGetPremium = true,

                UpgradeSortOrder = 3,
                DisplaySortOrder = 3,

                StripeSeatPlanId = "2020-enterprise-org-seat-annually",
                StripeStoragePlanId = "storage-gb-annually",
                BasePrice = 0,
                SeatPrice = 60,
                AdditionalStoragePricePerGb = 4,

                AllowSeatAutoscale = true,
            },
            new Plan
            {
                Type = PlanType.EnterpriseMonthly,
                Product = ProductType.Enterprise,
                Name = "Enterprise (Monthly)",
                NameLocalizationKey = "planNameEnterprise",
                DescriptionLocalizationKey = "planDescEnterprise",
                CanBeUsedByBusiness = true,
                BaseSeats = 0,
                BaseStorageGb = 1,

                HasAdditionalSeatsOption = true,
                HasAdditionalStorageOption = true,
                TrialPeriodDays = 7,

                HasPolicies = true,
                HasGroups = true,
                HasDirectory = true,
                HasEvents = true,
                HasTotp = true,
                Has2fa = true,
                HasApi = true,
                HasSelfHost = true,
                HasSso = true,
                HasKeyConnector = true,
                HasScim = true,
                HasResetPassword = true,
                UsersGetPremium = true,

                UpgradeSortOrder = 3,
                DisplaySortOrder = 3,

                StripeSeatPlanId = "2020-enterprise-org-seat-monthly",
                StripeStoragePlanId = "storage-gb-monthly",
                BasePrice = 0,
                SeatPrice = 6,
                AdditionalStoragePricePerGb = 0.5M,

                AllowSeatAutoscale = true,
            },
            new Plan
            {
                Type = PlanType.Custom,

                AllowSeatAutoscale = true,
            },
        };

        #endregion
    }

    public static IDictionary<GlobalEquivalentDomainsType, IEnumerable<string>> GlobalDomains { get; set; }
    public static IEnumerable<Plan> Plans { get; set; }
    public static IEnumerable<SponsoredPlan> SponsoredPlans { get; set; } = new[]
        {
            new SponsoredPlan
            {
                PlanSponsorshipType = PlanSponsorshipType.FamiliesForEnterprise,
                SponsoredProductType = ProductType.Families,
                SponsoringProductType = ProductType.Enterprise,
                StripePlanId = "2021-family-for-enterprise-annually",
                UsersCanSponsor = (OrganizationUserOrganizationDetails org) =>
                    GetPlan(org.PlanType).Product == ProductType.Enterprise,
            }
        };
    public static Plan GetPlan(PlanType planType) =>
        Plans.FirstOrDefault(p => p.Type == planType);
    public static SponsoredPlan GetSponsoredPlan(PlanSponsorshipType planSponsorshipType) =>
        SponsoredPlans.FirstOrDefault(p => p.PlanSponsorshipType == planSponsorshipType);
}
