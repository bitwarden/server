using Bit.Seeder.Data.Enums;

namespace Bit.Seeder.Data.Static;

internal sealed record Company(
    string Domain,
    string Name,
    CompanyCategory Category,
    CompanyType Type,
    GeographicRegion Region);

/// <summary>
/// Sample company data organized by region. Add new regions by creating arrays and including them in All.
/// </summary>
internal static class Companies
{
    internal static readonly Company[] NorthAmerica =
    [
        // CRM & Sales
        new("salesforce.example", "Salesforce", CompanyCategory.CRM, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("hubspot.example", "HubSpot", CompanyCategory.CRM, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        // Security
        new("crowdstrike.example", "CrowdStrike", CompanyCategory.Security, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("okta.example", "Okta", CompanyCategory.Security, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        // Observability & DevOps
        new("datadog.example", "Datadog", CompanyCategory.DevOps, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("splunk.example", "Splunk", CompanyCategory.Analytics, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("pagerduty.example", "PagerDuty", CompanyCategory.ITServiceManagement, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        // Cloud & Infrastructure
        new("snowflake.example", "Snowflake", CompanyCategory.CloudInfrastructure, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        // HR & Workforce
        new("workday.example", "Workday", CompanyCategory.HRTalent, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("servicenow.example", "ServiceNow", CompanyCategory.ITServiceManagement, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        // Consumer Tech Giants
        new("google.example", "Google", CompanyCategory.Productivity, CompanyType.Hybrid, GeographicRegion.NorthAmerica),
        new("meta.example", "Meta", CompanyCategory.SocialMedia, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("amazon.example", "Amazon", CompanyCategory.ECommerce, CompanyType.Hybrid, GeographicRegion.NorthAmerica),
        new("netflix.example", "Netflix", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        // Developer Tools
        new("github.example", "GitHub", CompanyCategory.Developer, CompanyType.Hybrid, GeographicRegion.NorthAmerica),
        new("stripe.example", "Stripe", CompanyCategory.Financial, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        // Collaboration
        new("slack.example", "Slack", CompanyCategory.Collaboration, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("zoom.example", "Zoom", CompanyCategory.Collaboration, CompanyType.Hybrid, GeographicRegion.NorthAmerica),
        new("dropbox.example", "Dropbox", CompanyCategory.Productivity, CompanyType.Hybrid, GeographicRegion.NorthAmerica),
        // Streaming
        new("hulu.example", "Hulu", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("max.example", "Max", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("paramountplus.example", "Paramount+", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("peacocktv.example", "Peacock", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("tubi.example", "Tubi", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("pluto.example", "Pluto TV", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("sling.example", "Sling TV", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("fubo.example", "Fubo", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("pandora.example", "Pandora", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("iheart.example", "iHeart", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("crunchyroll.example", "Crunchyroll", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("vimeo.example", "Vimeo", CompanyCategory.Streaming, CompanyType.Hybrid, GeographicRegion.NorthAmerica)
    ];

    internal static readonly Company[] Europe =
    [
        // Enterprise Software
        new("sap.example", "SAP", CompanyCategory.FinanceERP, CompanyType.Enterprise, GeographicRegion.Europe),
        new("elastic.example", "Elastic", CompanyCategory.Analytics, CompanyType.Enterprise, GeographicRegion.Europe),
        // Streaming
        new("spotify.example", "Spotify", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.Europe),
        // Fintech
        new("wise.example", "Wise", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.Europe),
        new("revolut.example", "Revolut", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.Europe),
        new("klarna.example", "Klarna", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.Europe),
        new("n26.example", "N26", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.Europe),
        // Developer Tools
        new("gitlab.example", "GitLab", CompanyCategory.DevOps, CompanyType.Enterprise, GeographicRegion.Europe),
        new("contentful.example", "Contentful", CompanyCategory.Developer, CompanyType.Enterprise, GeographicRegion.Europe),
        // Consumer Services
        new("deliveroo.example", "Deliveroo", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.Europe),
        new("booking.example", "Booking.com", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.Europe),
        // Collaboration
        new("miro.example", "Miro", CompanyCategory.Collaboration, CompanyType.Enterprise, GeographicRegion.Europe),
        new("intercom.example", "Intercom", CompanyCategory.CRM, CompanyType.Enterprise, GeographicRegion.Europe),
        // Business Software
        new("sage.example", "Sage", CompanyCategory.FinanceERP, CompanyType.Enterprise, GeographicRegion.Europe),
        new("adyen.example", "Adyen", CompanyCategory.Financial, CompanyType.Enterprise, GeographicRegion.Europe)
    ];

    internal static readonly Company[] AsiaPacific =
    [
        // Chinese Tech Giants
        new("alibaba.example", "Alibaba", CompanyCategory.ECommerce, CompanyType.Hybrid, GeographicRegion.AsiaPacific),
        new("tencent.example", "Tencent", CompanyCategory.SocialMedia, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("bytedance.example", "ByteDance", CompanyCategory.SocialMedia, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("baidu.example", "Baidu", CompanyCategory.Productivity, CompanyType.Hybrid, GeographicRegion.AsiaPacific),
        // Japanese Companies
        new("rakuten.example", "Rakuten", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("line.example", "Line", CompanyCategory.SocialMedia, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("sony.example", "Sony", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("paypay.example", "PayPay", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Korean Companies
        new("samsung.example", "Samsung", CompanyCategory.Productivity, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Australian Companies
        new("atlassian.example", "Atlassian", CompanyCategory.ProjectManagement, CompanyType.Enterprise, GeographicRegion.AsiaPacific),
        // Southeast Asian Companies
        new("grab.example", "Grab", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("sea.example", "Sea Limited", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("coupang.example", "Coupang", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("lazada.example", "Lazada", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("gojek.example", "Gojek", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Indian Companies
        new("flipkart.example", "Flipkart", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Banking
        new("dbs.example", "DBS", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("sbi.example", "SBI", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("icicibank.example", "ICICI Bank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("mufg.example", "MUFG", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("maybank.example", "Maybank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("cba.example", "Commonwealth Bank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("ocbc.example", "OCBC", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("kotak.example", "Kotak Mahindra", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Airlines
        new("singaporeair.example", "Singapore Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("cathaypacific.example", "Cathay Pacific", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("ana.example", "ANA", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("qantas.example", "Qantas", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("airasia.example", "AirAsia", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("koreanair.example", "Korean Air", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("jal.example", "Japan Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("indigo.example", "IndiGo", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Travel
        new("makemytrip.example", "MakeMyTrip", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("agoda.example", "Agoda", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("traveloka.example", "Traveloka", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("trip.example", "Trip.com", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Hotels
        new("oyo.example", "OYO", CompanyCategory.Hotels, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Car Rental
        new("zoomcar.example", "Zoomcar", CompanyCategory.CarRental, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Rail
        new("irctc.example", "IRCTC", CompanyCategory.Rail, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("jreast.example", "JR East", CompanyCategory.Rail, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("korail.example", "Korail", CompanyCategory.Rail, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Ride Share
        new("ola.example", "Ola", CompanyCategory.RideShare, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("didiglobal.example", "DiDi", CompanyCategory.RideShare, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Insurance
        new("pingan.example", "Ping An", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("aia.example", "AIA", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("tal.example", "TAL", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Healthcare
        new("practo.example", "Practo", CompanyCategory.Healthcare, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("1mg.example", "1mg", CompanyCategory.Healthcare, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("halodoc.example", "Halodoc", CompanyCategory.Healthcare, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Telecom
        new("nttdocomo.example", "NTT Docomo", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("singtel.example", "Singtel", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("telstra.example", "Telstra", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("jio.example", "Jio", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("sktelecom.example", "SK Telecom", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("airtelindia.example", "Airtel India", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Education
        new("byjus.example", "Byju's", CompanyCategory.Education, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("unacademy.example", "Unacademy", CompanyCategory.Education, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("u-tokyo.example", "University of Tokyo", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.AsiaPacific),
        new("rmit.example", "RMIT University", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.AsiaPacific),
        new("sutd.example", "Singapore University of Technology and Design", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.AsiaPacific),
        new("iith.example", "IIT Hyderabad", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.AsiaPacific),
        new("sdu.example", "Seoul Digital University", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.AsiaPacific),
        // Retail
        new("uniqlo.example", "Uniqlo", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("muji.example", "Muji", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("tokopedia.example", "Tokopedia", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("jd.example", "JD.com", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("myntra.example", "Myntra", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("bigbasket.example", "BigBasket", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Food & Beverage
        new("zomato.example", "Zomato", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("swiggy.example", "Swiggy", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("foodpanda.example", "Foodpanda", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("meituan.example", "Meituan", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Automotive
        new("toyota.example", "Toyota", CompanyCategory.Automotive, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("honda.example", "Honda", CompanyCategory.Automotive, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("hyundai.example", "Hyundai", CompanyCategory.Automotive, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("byd.example", "BYD", CompanyCategory.Automotive, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("suzuki.example", "Suzuki", CompanyCategory.Automotive, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Gaming
        new("nintendo.example", "Nintendo", CompanyCategory.Gaming, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("bandainamco.example", "Bandai Namco", CompanyCategory.Gaming, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("hoyoverse.example", "HoYoverse", CompanyCategory.Gaming, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("krafton.example", "Krafton", CompanyCategory.Gaming, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("nexon.example", "Nexon", CompanyCategory.Gaming, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // News & Media
        new("nikkei.example", "Nikkei", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("scmp.example", "South China Morning Post", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("timesofindia.example", "Times of India", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("straitstimes.example", "Straits Times", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Streaming
        new("iqiyi.example", "iQIYI", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("hotstar.example", "Hotstar", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("bilibili.example", "Bilibili", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Social Media
        new("weibo.example", "Weibo", CompanyCategory.SocialMedia, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("kakaocorp.example", "KakaoTalk", CompanyCategory.SocialMedia, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("naver.example", "Naver", CompanyCategory.SocialMedia, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Energy
        new("tepco.example", "TEPCO", CompanyCategory.Energy, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("originenergy.example", "Origin Energy", CompanyCategory.Energy, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Logistics
        new("kuronekoyamato.example", "Yamato Transport", CompanyCategory.Logistics, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("sf-express.example", "SF Express", CompanyCategory.Logistics, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("auspost.example", "Australia Post", CompanyCategory.Logistics, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Financial
        new("phonepe.example", "PhonePe", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("paytm.example", "Paytm", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Fitness
        new("cultfit.example", "Cult.fit", CompanyCategory.Fitness, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Government
        new("mygovin.example", "MyGov India", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("mygovau.example", "myGov Australia", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("digitaljp.example", "Japan Digital Agency", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.AsiaPacific),
        new("smartnation.example", "Smart Nation Singapore", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.AsiaPacific),
        new("digilocker.example", "DigiLocker India", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("atoau.example", "Australian Taxation Office", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("msitkr.example", "Ministry of Science & ICT Korea", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.AsiaPacific)
    ];

    internal static readonly Company[] LatinAmerica =
    [
        // Banking
        new("bancodobrasil.example", "Banco do Brasil", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("itau.example", "Itau", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("nubank.example", "Nubank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("banorte.example", "Banorte", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("bbvamx.example", "BBVA Mexico", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("bancolombia.example", "Bancolombia", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("bradesco.example", "Bradesco", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("bcpperu.example", "BCP Peru", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // Airlines
        new("latamairlines.example", "LATAM Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("avianca.example", "Avianca", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("aeromexico.example", "Aeromexico", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("copaair.example", "Copa Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("voegol.example", "Gol Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("voeazul.example", "Azul Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // Travel
        new("despegar.example", "Despegar", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("decolar.example", "Decolar", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // ECommerce
        new("mercadolibre.example", "MercadoLibre", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("americanas.example", "Americanas", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("magazineluiza.example", "Magazine Luiza", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("falabella.example", "Falabella", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("linio.example", "Linio", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // Telecom
        new("claro.example", "Claro", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("vivo.example", "Vivo", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("telmex.example", "Telmex", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("entel.example", "Entel", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // Financial
        new("mercadopago.example", "MercadoPago", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("picpay.example", "PicPay", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("uala.example", "Uala", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // Food & Beverage
        new("ifood.example", "iFood", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("rappi.example", "Rappi", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("pedidosya.example", "PedidosYa", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // Ride Share
        new("99app.example", "99", CompanyCategory.RideShare, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("beat.example", "Beat", CompanyCategory.RideShare, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // Streaming
        new("globoplay.example", "Globoplay", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("blim.example", "Blim TV", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // Retail
        new("liverpool.example", "Liverpool", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("coppel.example", "Coppel", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("casasbahia.example", "Casas Bahia", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // News & Media
        new("globo.example", "Globo", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("eluniversal.example", "El Universal", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // Government
        new("govbr.example", "GOV.BR", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("satmx.example", "SAT Mexico", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("serpro.example", "Servico Federal de Processamento", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.LatinAmerica),
        new("dgii.example", "Direccion General de Impuestos", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.LatinAmerica),
        new("anses.example", "ANSES Argentina", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // Insurance
        new("sulamerica.example", "SulAmerica", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("portoseguro.example", "Porto Seguro", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // Healthcare
        new("drogasil.example", "Drogasil", CompanyCategory.Healthcare, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("docplanner.example", "Doctoralia", CompanyCategory.Healthcare, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // Education
        new("platzi.example", "Platzi", CompanyCategory.Education, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("domestika.example", "Domestika", CompanyCategory.Education, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("usp.example", "Universidade de Sao Paulo", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.LatinAmerica),
        new("tecmx.example", "Tecnologico de Monterrey", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.LatinAmerica),
        new("uba.example", "Universidad de Buenos Aires", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.LatinAmerica),
        // Energy
        new("petrobras.example", "Petrobras", CompanyCategory.Energy, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("ecopetrol.example", "Ecopetrol", CompanyCategory.Energy, CompanyType.Consumer, GeographicRegion.LatinAmerica)
    ];

    internal static readonly Company[] MiddleEast =
    [
        // Banking
        new("emiratesnbd.example", "Emirates NBD", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("alrajhibank.example", "Al Rajhi Bank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("qnb.example", "QNB", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("garanti.example", "Garanti BBVA", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("isbank.example", "Isbank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("kfh.example", "Kuwait Finance House", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("mashreqbank.example", "Mashreq", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("bankfab.example", "First Abu Dhabi Bank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // Airlines
        new("emirates.example", "Emirates", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("qatarairways.example", "Qatar Airways", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("etihad.example", "Etihad Airways", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("turkishairlines.example", "Turkish Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("saudia.example", "Saudia", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("flynas.example", "Flynas", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("pegasus.example", "Pegasus Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // Travel
        new("wego.example", "Wego", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("almosafer.example", "Almosafer", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // Hotels
        new("rotana.example", "Rotana Hotels", CompanyCategory.Hotels, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("jumeirah.example", "Jumeirah", CompanyCategory.Hotels, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // Telecom
        new("etisalat.example", "Etisalat", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("stc.example", "STC", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("turkcell.example", "Turkcell", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("zain.example", "Zain", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("du.example", "Du", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // ECommerce
        new("noon.example", "Noon", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("trendyol.example", "Trendyol", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("hepsiburada.example", "Hepsiburada", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("amazonae.example", "Amazon AE", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // Food & Beverage
        new("talabat.example", "Talabat", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("yemeksepeti.example", "Yemeksepeti", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("hungerstation.example", "HungerStation", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // Ride Share
        new("careem.example", "Careem", CompanyCategory.RideShare, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // Financial
        new("stcpay.example", "STC Pay", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("paytabs.example", "PayTabs", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // News & Media
        new("aljazeera.example", "Al Jazeera", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("hurriyet.example", "Hurriyet", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("arabnews.example", "Arab News", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // Real Estate
        new("bayut.example", "Bayut", CompanyCategory.RealEstate, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("propertyfinder.example", "Property Finder", CompanyCategory.RealEstate, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("sahibinden.example", "Sahibinden", CompanyCategory.RealEstate, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // Government
        new("absher.example", "Absher", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("edevlet.example", "e-Devlet", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("uaepass.example", "UAE Pass", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("mocsa.example", "Ministry of Commerce Saudi Arabia", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("hukoomi.example", "Hukoomi Qatar", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // Education
        new("aud.example", "American University in Dubai", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.MiddleEast),
        new("ksu.example", "King Saud University", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.MiddleEast),
        new("metu.example", "Middle East Technical University", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.MiddleEast),
        // Insurance
        new("tawuniya.example", "Tawuniya", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // Retail
        new("lcwaikiki.example", "LC Waikiki", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("extrastores.example", "Extra Stores", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("defacto.example", "DeFacto", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.MiddleEast)
    ];

    internal static readonly Company[] Africa =
    [
        // Banking
        new("standardbank.example", "Standard Bank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Africa),
        new("fnb.example", "FNB", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Africa),
        new("accessbank.example", "Access Bank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Africa),
        new("equitybank.example", "Equity Bank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Africa),
        new("absa.example", "Absa", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Africa),
        new("gtbank.example", "GTBank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Africa),
        new("nedbank.example", "Nedbank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Africa),
        new("kcbgroup.example", "KCB Bank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Africa),
        // Telecom
        new("mtn.example", "MTN", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.Africa),
        new("safaricom.example", "Safaricom", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.Africa),
        new("vodacom.example", "Vodacom", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.Africa),
        new("airtelafrica.example", "Airtel Africa", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.Africa),
        new("maroctelecom.example", "Maroc Telecom", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.Africa),
        new("telkomsa.example", "Telkom SA", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.Africa),
        // Financial
        new("opay.example", "OPay", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.Africa),
        new("flutterwave.example", "Flutterwave", CompanyCategory.Financial, CompanyType.Enterprise, GeographicRegion.Africa),
        new("paystack.example", "Paystack", CompanyCategory.Financial, CompanyType.Enterprise, GeographicRegion.Africa),
        new("chippercash.example", "Chipper Cash", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.Africa),
        new("moniepoint.example", "Moniepoint", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.Africa),
        // Airlines
        new("ethiopianairlines.example", "Ethiopian Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Africa),
        new("flysaa.example", "South African Airways", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Africa),
        new("kenya-airways.example", "Kenya Airways", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Africa),
        new("egyptair.example", "EgyptAir", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Africa),
        new("royalairmaroc.example", "Royal Air Maroc", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Africa),
        // ECommerce
        new("jumia.example", "Jumia", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.Africa),
        new("takealot.example", "Takealot", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.Africa),
        new("konga.example", "Konga", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.Africa),
        // Food & Beverage
        new("mrdfood.example", "Mr D Food", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.Africa),
        new("chowdeck.example", "Chowdeck", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.Africa),
        new("yassir.example", "Yassir", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.Africa),
        // Ride Share
        new("indrive.example", "inDrive", CompanyCategory.RideShare, CompanyType.Consumer, GeographicRegion.Africa),
        // News & Media
        new("news24.example", "News24", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.Africa),
        new("nationafrica.example", "Nation Africa", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.Africa),
        new("dailymaverick.example", "Daily Maverick", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.Africa),
        // Streaming
        new("showmax.example", "Showmax", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.Africa),
        new("dstv.example", "DStv", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.Africa),
        // Insurance
        new("oldmutual.example", "Old Mutual", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.Africa),
        new("discovery.example", "Discovery", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.Africa),
        // Education
        new("ulesson.example", "uLesson", CompanyCategory.Education, CompanyType.Consumer, GeographicRegion.Africa),
        new("alxafrica.example", "ALX", CompanyCategory.Education, CompanyType.Consumer, GeographicRegion.Africa),
        new("cctu.example", "Cape Coast Technical University", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.Africa),
        new("uj.example", "University of Johannesburg", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.Africa),
        new("uonbi.example", "University of Nairobi", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.Africa),
        // Government
        new("sars.example", "SARS eFiling", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.Africa),
        new("nimcng.example", "NIMC Nigeria", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.Africa),
        new("ecitizen.example", "eCitizen Kenya", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.Africa),
        new("dhaza.example", "Department of Home Affairs SA", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.Africa),
        new("irembo.example", "Irembo Rwanda", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.Africa),
        // Retail
        new("shoprite.example", "Shoprite", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.Africa),
        new("woolworths.example", "Woolworths SA", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.Africa),
        new("checkers.example", "Checkers", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.Africa),
        // Logistics
        new("thecourierguys.example", "The Courier Guy", CompanyCategory.Logistics, CompanyType.Consumer, GeographicRegion.Africa),
        // Energy
        new("eskom.example", "Eskom", CompanyCategory.Energy, CompanyType.Consumer, GeographicRegion.Africa)
    ];

    internal static readonly Company[] All = [.. NorthAmerica, .. Europe, .. AsiaPacific, .. LatinAmerica, .. MiddleEast, .. Africa];

    internal static Company[] Filter(
        CompanyType? type = null,
        GeographicRegion? region = null,
        CompanyCategory? category = null)
    {
        IEnumerable<Company> result = All;

        if (type.HasValue)
        {
            result = result.Where(c => c.Type == type.Value);
        }
        if (region.HasValue)
        {
            result = result.Where(c => c.Region == region.Value);
        }
        if (category.HasValue)
        {
            result = result.Where(c => c.Category == category.Value);
        }

        return [.. result];
    }
}
