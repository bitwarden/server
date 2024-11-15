using System.Collections.Immutable;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Models.StaticStore.Plans;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Utilities;

public static class StaticStore
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
        GlobalDomains.Add(GlobalEquivalentDomainsType.Pinterest, new List<string> { "pinterest.com", "pinterest.com.au", "pinterest.cl", "pinterest.de", "pinterest.dk", "pinterest.es", "pinterest.fr", "pinterest.co.uk", "pinterest.jp", "pinterest.co.kr", "pinterest.nz", "pinterest.pt", "pinterest.se" });
        #endregion

        Plans = new List<Plan>
        {
            new EnterprisePlan(true),
            new EnterprisePlan(false),
            new TeamsStarterPlan(),
            new TeamsPlan(true),
            new TeamsPlan(false),

            new Enterprise2023Plan(true),
            new Enterprise2023Plan(false),
            new Enterprise2020Plan(true),
            new Enterprise2020Plan(false),
            new TeamsStarterPlan2023(),
            new Teams2023Plan(true),
            new Teams2023Plan(false),
            new Teams2020Plan(true),
            new Teams2020Plan(false),
            new FamiliesPlan(),
            new FreePlan(),
            new CustomPlan(),

            new Enterprise2019Plan(true),
            new Enterprise2019Plan(false),
            new Teams2019Plan(true),
            new Teams2019Plan(false),
            new Families2019Plan(),
        }.ToImmutableList();
    }

    public static IDictionary<GlobalEquivalentDomainsType, IEnumerable<string>> GlobalDomains { get; set; }
    public static IEnumerable<Plan> Plans { get; }
    public static IEnumerable<SponsoredPlan> SponsoredPlans { get; set; } = new[]
        {
            new SponsoredPlan
            {
                PlanSponsorshipType = PlanSponsorshipType.FamiliesForEnterprise,
                SponsoredProductTierType = ProductTierType.Families,
                SponsoringProductTierType = ProductTierType.Enterprise,
                StripePlanId = "2021-family-for-enterprise-annually",
                UsersCanSponsor = (OrganizationUserOrganizationDetails org) =>
                    GetPlan(org.PlanType).ProductTier == ProductTierType.Enterprise,
            }
        };

    public static Plan GetPlan(PlanType planType) => Plans.SingleOrDefault(p => p.Type == planType);

    public static SponsoredPlan GetSponsoredPlan(PlanSponsorshipType planSponsorshipType) =>
        SponsoredPlans.FirstOrDefault(p => p.PlanSponsorshipType == planSponsorshipType);

    /// <summary>
    /// Determines if the stripe plan id is an addon item by checking if the provided stripe plan id
    /// matches either the <see cref="Plan.PasswordManagerPlanFeatures.StripeStoragePlanId"/> or <see cref="Plan.SecretsManagerPlanFeatures.StripeServiceAccountPlanId"/>
    /// in any <see cref="Plans"/>.
    /// </summary>
    /// <param name="stripePlanId"></param>
    /// <returns>
    /// True if the stripePlanId is a addon product, false otherwise
    /// </returns>
    public static bool IsAddonSubscriptionItem(string stripePlanId)
    {
        return Plans.Any(p =>
                p.PasswordManager.StripeStoragePlanId == stripePlanId ||
                (p.SecretsManager?.StripeServiceAccountPlanId == stripePlanId));
    }

    public static readonly IEnumerable<TaxIdType> SupportedTaxIdTypes =
    [
        new() { Country = "AD", Code = "ad_nrt", Description = "Andorran NRT number", Example = "A-123456-Z" },
        new()
        {
            Country = "AR",
            Code = "ar_cuit",
            Description = "Argentinian tax ID number",
            Example = "12-3456789-01"
        },
        new()
        {
            Country = "AU",
            Code = "au_abn",
            Description = "Australian Business Number (AU ABN)",
            Example = "12345678912"
        },
        new()
        {
            Country = "AU",
            Code = "au_arn",
            Description = "Australian Taxation Office Reference Number",
            Example = "123456789123"
        },
        new() { Country = "AT", Code = "eu_vat", Description = "European VAT number", Example = "ATU12345678" },
        new() { Country = "BH", Code = "bh_vat", Description = "Bahraini VAT Number", Example = "123456789012345" },
        new() { Country = "BY", Code = "by_tin", Description = "Belarus TIN Number", Example = "123456789" },
        new() { Country = "BE", Code = "eu_vat", Description = "European VAT number", Example = "BE0123456789" },
        new() { Country = "BO", Code = "bo_tin", Description = "Bolivian tax ID", Example = "123456789" },
        new()
        {
            Country = "BR",
            Code = "br_cnpj",
            Description = "Brazilian CNPJ number",
            Example = "01.234.456/5432-10"
        },
        new() { Country = "BR", Code = "br_cpf", Description = "Brazilian CPF number", Example = "123.456.789-87" },
        new()
        {
            Country = "BG",
            Code = "bg_uic",
            Description = "Bulgaria Unified Identification Code",
            Example = "123456789"
        },
        new() { Country = "BG", Code = "eu_vat", Description = "European VAT number", Example = "BG0123456789" },
        new() { Country = "CA", Code = "ca_bn", Description = "Canadian BN", Example = "123456789" },
        new()
        {
            Country = "CA",
            Code = "ca_gst_hst",
            Description = "Canadian GST/HST number",
            Example = "123456789RT0002"
        },
        new()
        {
            Country = "CA",
            Code = "ca_pst_bc",
            Description = "Canadian PST number (British Columbia)",
            Example = "PST-1234-5678"
        },
        new()
        {
            Country = "CA",
            Code = "ca_pst_mb",
            Description = "Canadian PST number (Manitoba)",
            Example = "123456-7"
        },
        new()
        {
            Country = "CA",
            Code = "ca_pst_sk",
            Description = "Canadian PST number (Saskatchewan)",
            Example = "1234567"
        },
        new()
        {
            Country = "CA",
            Code = "ca_qst",
            Description = "Canadian QST number (Québec)",
            Example = "1234567890TQ1234"
        },
        new() { Country = "CL", Code = "cl_tin", Description = "Chilean TIN", Example = "12.345.678-K" },
        new() { Country = "CN", Code = "cn_tin", Description = "Chinese tax ID", Example = "123456789012345678" },
        new() { Country = "CO", Code = "co_nit", Description = "Colombian NIT number", Example = "123.456.789-0" },
        new() { Country = "CR", Code = "cr_tin", Description = "Costa Rican tax ID", Example = "1-234-567890" },
        new() { Country = "HR", Code = "eu_vat", Description = "European VAT number", Example = "HR12345678912" },
        new()
        {
            Country = "HR",
            Code = "hr_oib",
            Description = "Croatian Personal Identification Number",
            Example = "12345678901"
        },
        new() { Country = "CY", Code = "eu_vat", Description = "European VAT number", Example = "CY12345678Z" },
        new() { Country = "CZ", Code = "eu_vat", Description = "European VAT number", Example = "CZ1234567890" },
        new() { Country = "DK", Code = "eu_vat", Description = "European VAT number", Example = "DK12345678" },
        new() { Country = "DO", Code = "do_rcn", Description = "Dominican RCN number", Example = "123-4567890-1" },
        new() { Country = "EC", Code = "ec_ruc", Description = "Ecuadorian RUC number", Example = "1234567890001" },
        new()
        {
            Country = "EG",
            Code = "eg_tin",
            Description = "Egyptian Tax Identification Number",
            Example = "123456789"
        },

        new()
        {
            Country = "SV",
            Code = "sv_nit",
            Description = "El Salvadorian NIT number",
            Example = "1234-567890-123-4"
        },

        new() { Country = "EE", Code = "eu_vat", Description = "European VAT number", Example = "EE123456789" },

        new()
        {
            Country = "EU",
            Code = "eu_oss_vat",
            Description = "European One Stop Shop VAT number for non-Union scheme",
            Example = "EU123456789"
        },
        new() { Country = "FI", Code = "eu_vat", Description = "European VAT number", Example = "FI12345678" },
        new() { Country = "FR", Code = "eu_vat", Description = "European VAT number", Example = "FRAB123456789" },
        new() { Country = "GE", Code = "ge_vat", Description = "Georgian VAT", Example = "123456789" },
        new()
        {
            Country = "DE",
            Code = "de_stn",
            Description = "German Tax Number (Steuernummer)",
            Example = "1234567890"
        },
        new() { Country = "DE", Code = "eu_vat", Description = "European VAT number", Example = "DE123456789" },
        new() { Country = "GR", Code = "eu_vat", Description = "European VAT number", Example = "EL123456789" },
        new() { Country = "HK", Code = "hk_br", Description = "Hong Kong BR number", Example = "12345678" },
        new() { Country = "HU", Code = "eu_vat", Description = "European VAT number", Example = "HU12345678" },
        new()
        {
            Country = "HU",
            Code = "hu_tin",
            Description = "Hungary tax number (adószám)",
            Example = "12345678-1-23"
        },
        new() { Country = "IS", Code = "is_vat", Description = "Icelandic VAT", Example = "123456" },
        new() { Country = "IN", Code = "in_gst", Description = "Indian GST number", Example = "12ABCDE3456FGZH" },
        new()
        {
            Country = "ID",
            Code = "id_npwp",
            Description = "Indonesian NPWP number",
            Example = "012.345.678.9-012.345"
        },
        new() { Country = "IE", Code = "eu_vat", Description = "European VAT number", Example = "IE1234567AB" },
        new() { Country = "IL", Code = "il_vat", Description = "Israel VAT", Example = "000012345" },
        new() { Country = "IT", Code = "eu_vat", Description = "European VAT number", Example = "IT12345678912" },
        new()
        {
            Country = "JP",
            Code = "jp_cn",
            Description = "Japanese Corporate Number (*Hōjin Bangō*)",
            Example = "1234567891234"
        },
        new()
        {
            Country = "JP",
            Code = "jp_rn",
            Description =
                "Japanese Registered Foreign Businesses' Registration Number (*Tōroku Kokugai Jigyōsha no Tōroku Bangō*)",
            Example = "12345"
        },
        new()
        {
            Country = "JP",
            Code = "jp_trn",
            Description = "Japanese Tax Registration Number (*Tōroku Bangō*)",
            Example = "T1234567891234"
        },
        new()
        {
            Country = "KZ",
            Code = "kz_bin",
            Description = "Kazakhstani Business Identification Number",
            Example = "123456789012"
        },
        new()
        {
            Country = "KE",
            Code = "ke_pin",
            Description = "Kenya Revenue Authority Personal Identification Number",
            Example = "P000111111A"
        },
        new() { Country = "LV", Code = "eu_vat", Description = "European VAT number", Example = "LV12345678912" },
        new()
        {
            Country = "LI",
            Code = "li_uid",
            Description = "Liechtensteinian UID number",
            Example = "CHE123456789"
        },
        new() { Country = "LI", Code = "li_vat", Description = "Liechtensteinian VAT number", Example = "12345" },
        new() { Country = "LT", Code = "eu_vat", Description = "European VAT number", Example = "LT123456789123" },
        new() { Country = "LU", Code = "eu_vat", Description = "European VAT number", Example = "LU12345678" },
        new() { Country = "MY", Code = "my_frp", Description = "Malaysian FRP number", Example = "12345678" },
        new() { Country = "MY", Code = "my_itn", Description = "Malaysian ITN", Example = "C 1234567890" },
        new() { Country = "MY", Code = "my_sst", Description = "Malaysian SST number", Example = "A12-3456-78912345" },
        new() { Country = "MT", Code = "eu_vat", Description = "European VAT number", Example = "MT12345678" },
        new() { Country = "MX", Code = "mx_rfc", Description = "Mexican RFC number", Example = "ABC010203AB9" },
        new() { Country = "MD", Code = "md_vat", Description = "Moldova VAT Number", Example = "1234567" },
        new() { Country = "MA", Code = "ma_vat", Description = "Morocco VAT Number", Example = "12345678" },
        new() { Country = "NL", Code = "eu_vat", Description = "European VAT number", Example = "NL123456789B12" },
        new() { Country = "NZ", Code = "nz_gst", Description = "New Zealand GST number", Example = "123456789" },
        new() { Country = "NG", Code = "ng_tin", Description = "Nigerian TIN Number", Example = "12345678-0001" },
        new() { Country = "NO", Code = "no_vat", Description = "Norwegian VAT number", Example = "123456789MVA" },
        new()
        {
            Country = "NO",
            Code = "no_voec",
            Description = "Norwegian VAT on e-commerce number",
            Example = "1234567"
        },
        new() { Country = "OM", Code = "om_vat", Description = "Omani VAT Number", Example = "OM1234567890" },
        new() { Country = "PE", Code = "pe_ruc", Description = "Peruvian RUC number", Example = "12345678901" },
        new()
        {
            Country = "PH",
            Code = "ph_tin",
            Description = "Philippines Tax Identification Number",
            Example = "123456789012"
        },
        new() { Country = "PL", Code = "eu_vat", Description = "European VAT number", Example = "PL1234567890" },
        new() { Country = "PT", Code = "eu_vat", Description = "European VAT number", Example = "PT123456789" },
        new() { Country = "RO", Code = "eu_vat", Description = "European VAT number", Example = "RO1234567891" },
        new() { Country = "RO", Code = "ro_tin", Description = "Romanian tax ID number", Example = "1234567890123" },
        new() { Country = "RU", Code = "ru_inn", Description = "Russian INN", Example = "1234567891" },
        new() { Country = "RU", Code = "ru_kpp", Description = "Russian KPP", Example = "123456789" },
        new() { Country = "SA", Code = "sa_vat", Description = "Saudi Arabia VAT", Example = "123456789012345" },
        new() { Country = "RS", Code = "rs_pib", Description = "Serbian PIB number", Example = "123456789" },
        new() { Country = "SG", Code = "sg_gst", Description = "Singaporean GST", Example = "M12345678X" },
        new() { Country = "SG", Code = "sg_uen", Description = "Singaporean UEN", Example = "123456789F" },
        new() { Country = "SK", Code = "eu_vat", Description = "European VAT number", Example = "SK1234567891" },
        new() { Country = "SI", Code = "eu_vat", Description = "European VAT number", Example = "SI12345678" },
        new()
        {
            Country = "SI",
            Code = "si_tin",
            Description = "Slovenia tax number (davčna številka)",
            Example = "12345678"
        },
        new() { Country = "ZA", Code = "za_vat", Description = "South African VAT number", Example = "4123456789" },
        new() { Country = "KR", Code = "kr_brn", Description = "Korean BRN", Example = "123-45-67890" },
        new() { Country = "ES", Code = "es_cif", Description = "Spanish NIF/CIF number", Example = "A12345678" },
        new() { Country = "ES", Code = "eu_vat", Description = "European VAT number", Example = "ESA1234567Z" },
        new() { Country = "SE", Code = "eu_vat", Description = "European VAT number", Example = "SE123456789123" },
        new()
        {
            Country = "CH",
            Code = "ch_uid",
            Description = "Switzerland UID number",
            Example = "CHE-123.456.789 HR"
        },
        new()
        {
            Country = "CH",
            Code = "ch_vat",
            Description = "Switzerland VAT number",
            Example = "CHE-123.456.789 MWST"
        },
        new() { Country = "TW", Code = "tw_vat", Description = "Taiwanese VAT", Example = "12345678" },
        new() { Country = "TZ", Code = "tz_vat", Description = "Tanzania VAT Number", Example = "12345678A" },
        new() { Country = "TH", Code = "th_vat", Description = "Thai VAT", Example = "1234567891234" },
        new() { Country = "TR", Code = "tr_tin", Description = "Turkish TIN Number", Example = "0123456789" },
        new() { Country = "UA", Code = "ua_vat", Description = "Ukrainian VAT", Example = "123456789" },
        new()
        {
            Country = "AE",
            Code = "ae_trn",
            Description = "United Arab Emirates TRN",
            Example = "123456789012345"
        },
        new() { Country = "GB", Code = "eu_vat", Description = "Northern Ireland VAT number", Example = "XI123456789" },
        new() { Country = "GB", Code = "gb_vat", Description = "United Kingdom VAT number", Example = "GB123456789" },
        new() { Country = "US", Code = "us_ein", Description = "United States EIN", Example = "12-3456789" },
        new() { Country = "UY", Code = "uy_ruc", Description = "Uruguayan RUC number", Example = "123456789012" },
        new() { Country = "UZ", Code = "uz_tin", Description = "Uzbekistan TIN Number", Example = "123456789" },
        new() { Country = "UZ", Code = "uz_vat", Description = "Uzbekistan VAT Number", Example = "123456789012" },
        new() { Country = "VE", Code = "ve_rif", Description = "Venezuelan RIF number", Example = "A-12345678-9" },
        new() { Country = "VN", Code = "vn_tin", Description = "Vietnamese tax ID number", Example = "1234567890" }
    ];
}
