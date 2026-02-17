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
        new("salesforce.com", "Salesforce", CompanyCategory.CRM, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("hubspot.com", "HubSpot", CompanyCategory.CRM, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        // Security
        new("crowdstrike.com", "CrowdStrike", CompanyCategory.Security, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("okta.com", "Okta", CompanyCategory.Security, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        // Observability & DevOps
        new("datadog.com", "Datadog", CompanyCategory.DevOps, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("splunk.com", "Splunk", CompanyCategory.Analytics, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("pagerduty.com", "PagerDuty", CompanyCategory.ITServiceManagement, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        // Cloud & Infrastructure
        new("snowflake.com", "Snowflake", CompanyCategory.CloudInfrastructure, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        // HR & Workforce
        new("workday.com", "Workday", CompanyCategory.HRTalent, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("servicenow.com", "ServiceNow", CompanyCategory.ITServiceManagement, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        // Consumer Tech Giants
        new("google.com", "Google", CompanyCategory.Productivity, CompanyType.Hybrid, GeographicRegion.NorthAmerica),
        new("meta.com", "Meta", CompanyCategory.SocialMedia, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("amazon.com", "Amazon", CompanyCategory.ECommerce, CompanyType.Hybrid, GeographicRegion.NorthAmerica),
        new("netflix.com", "Netflix", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        // Developer Tools
        new("github.com", "GitHub", CompanyCategory.Developer, CompanyType.Hybrid, GeographicRegion.NorthAmerica),
        new("stripe.com", "Stripe", CompanyCategory.Financial, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        // Collaboration
        new("slack.com", "Slack", CompanyCategory.Collaboration, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("zoom.us", "Zoom", CompanyCategory.Collaboration, CompanyType.Hybrid, GeographicRegion.NorthAmerica),
        new("dropbox.com", "Dropbox", CompanyCategory.Productivity, CompanyType.Hybrid, GeographicRegion.NorthAmerica),
        // Streaming
        new("spotify.com", "Spotify", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        // Banking
        new("chase.com", "Chase", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("bankofamerica.com", "Bank of America", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("wellsfargo.com", "Wells Fargo", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("citi.com", "Citibank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("usbank.com", "U.S. Bank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("capitalone.com", "Capital One", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("td.com", "TD Bank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("pnc.com", "PNC Bank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("schwab.com", "Charles Schwab", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("ally.com", "Ally Bank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        // Airlines
        new("united.com", "United Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("delta.com", "Delta Air Lines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("aa.com", "American Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("southwest.com", "Southwest Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("jetblue.com", "JetBlue", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("alaskaair.com", "Alaska Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("spirit.com", "Spirit Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("flyfrontier.com", "Frontier Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("hawaiianairlines.com", "Hawaiian Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("aircanada.com", "Air Canada", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("westjet.com", "WestJet", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        // Travel
        new("expedia.com", "Expedia", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("kayak.com", "Kayak", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("tripadvisor.com", "TripAdvisor", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("priceline.com", "Priceline", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("vrbo.com", "Vrbo", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("airbnb.com", "Airbnb", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("orbitz.com", "Orbitz", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("hotwire.com", "Hotwire", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("travelocity.com", "Travelocity", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        // Hotels
        new("hilton.com", "Hilton", CompanyCategory.Hotels, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("marriott.com", "Marriott", CompanyCategory.Hotels, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("ihg.com", "IHG", CompanyCategory.Hotels, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("hyatt.com", "Hyatt", CompanyCategory.Hotels, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("wyndhamhotels.com", "Wyndham", CompanyCategory.Hotels, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("bestwestern.com", "Best Western", CompanyCategory.Hotels, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("choicehotels.com", "Choice Hotels", CompanyCategory.Hotels, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        // Car Rental
        new("hertz.com", "Hertz", CompanyCategory.CarRental, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("enterprise.com", "Enterprise", CompanyCategory.CarRental, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("avis.com", "Avis", CompanyCategory.CarRental, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("budget.com", "Budget", CompanyCategory.CarRental, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("nationalcar.com", "National", CompanyCategory.CarRental, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("turo.com", "Turo", CompanyCategory.CarRental, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        // Rail
        new("amtrak.com", "Amtrak", CompanyCategory.Rail, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        // Ride Share
        new("uber.com", "Uber", CompanyCategory.RideShare, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("lyft.com", "Lyft", CompanyCategory.RideShare, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        // Insurance
        new("geico.com", "Geico", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("progressive.com", "Progressive", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("statefarm.com", "State Farm", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("allstate.com", "Allstate", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("usaa.com", "USAA", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("libertymutual.com", "Liberty Mutual", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        // Healthcare
        new("mychart.com", "MyChart", CompanyCategory.Healthcare, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("onemedical.com", "One Medical", CompanyCategory.Healthcare, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("cvs.com", "CVS", CompanyCategory.Healthcare, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("walgreens.com", "Walgreens", CompanyCategory.Healthcare, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("uhc.com", "UnitedHealthcare", CompanyCategory.Healthcare, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("anthem.com", "Anthem", CompanyCategory.Healthcare, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("teladoc.com", "Teladoc", CompanyCategory.Healthcare, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        // Telecom
        new("verizon.com", "Verizon", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("t-mobile.com", "T-Mobile", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("att.com", "AT&T", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("xfinity.com", "Xfinity", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("spectrum.com", "Spectrum", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("cox.com", "Cox", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        // Education
        new("coursera.org", "Coursera", CompanyCategory.Education, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("khanacademy.org", "Khan Academy", CompanyCategory.Education, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("udemy.com", "Udemy", CompanyCategory.Education, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("linkedin.com", "LinkedIn Learning", CompanyCategory.Education, CompanyType.Hybrid, GeographicRegion.NorthAmerica),
        new("instructure.com", "Canvas", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("edx.org", "edX", CompanyCategory.Education, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("westlakestate.edu", "Westlake State University", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("pacificridge.edu", "Pacific Ridge University", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("northernplains.edu", "Northern Plains Community College", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("heartlandtech.edu", "Heartland Technical Institute", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("silvercreek.k12.us", "Silver Creek School District", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("cascadiacollege.edu", "Cascadia College", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("summitvalley.edu", "Summit Valley University", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("copperfieldacademy.edu", "Copperfield Academy", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        // Retail
        new("target.com", "Target", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("walmart.com", "Walmart", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("costco.com", "Costco", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("homedepot.com", "Home Depot", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("lowes.com", "Lowe's", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("bestbuy.com", "Best Buy", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("macys.com", "Macy's", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("nordstrom.com", "Nordstrom", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("nike.com", "Nike", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("rei.com", "REI", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("gap.com", "Gap", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("kohls.com", "Kohl's", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("wayfair.com", "Wayfair", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("etsy.com", "Etsy", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        // Food & Beverage
        new("starbucks.com", "Starbucks", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("mcdonalds.com", "McDonald's", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("chipotle.com", "Chipotle", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("doordash.com", "DoorDash", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("ubereats.com", "Uber Eats", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("grubhub.com", "Grubhub", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("dominos.com", "Domino's", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("chick-fil-a.com", "Chick-fil-A", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("instacart.com", "Instacart", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        // Automotive
        new("tesla.com", "Tesla", CompanyCategory.Automotive, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("ford.com", "Ford", CompanyCategory.Automotive, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("gm.com", "General Motors", CompanyCategory.Automotive, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("carmax.com", "CarMax", CompanyCategory.Automotive, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("carvana.com", "Carvana", CompanyCategory.Automotive, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        // Gaming
        new("store.steampowered.com", "Steam", CompanyCategory.Gaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("epicgames.com", "Epic Games", CompanyCategory.Gaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("xbox.com", "Xbox", CompanyCategory.Gaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("playstation.com", "PlayStation", CompanyCategory.Gaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("twitch.tv", "Twitch", CompanyCategory.Gaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("ea.com", "Electronic Arts", CompanyCategory.Gaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("roblox.com", "Roblox", CompanyCategory.Gaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        // News & Media
        new("nytimes.com", "New York Times", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("wsj.com", "Wall Street Journal", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("washingtonpost.com", "Washington Post", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("cnn.com", "CNN", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("bloomberg.com", "Bloomberg", CompanyCategory.News, CompanyType.Hybrid, GeographicRegion.NorthAmerica),
        new("reuters.com", "Reuters", CompanyCategory.News, CompanyType.Hybrid, GeographicRegion.NorthAmerica),
        // Streaming (additional)
        new("disneyplus.com", "Disney+", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("hulu.com", "Hulu", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("paramountplus.com", "Paramount+", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("max.com", "Max", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("peacocktv.com", "Peacock", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("tv.apple.com", "Apple TV+", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        // Social Media (additional)
        new("x.com", "X", CompanyCategory.SocialMedia, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("reddit.com", "Reddit", CompanyCategory.SocialMedia, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("pinterest.com", "Pinterest", CompanyCategory.SocialMedia, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("tiktok.com", "TikTok", CompanyCategory.SocialMedia, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("snapchat.com", "Snapchat", CompanyCategory.SocialMedia, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("discord.com", "Discord", CompanyCategory.SocialMedia, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        // Energy
        new("duke-energy.com", "Duke Energy", CompanyCategory.Energy, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("pge.com", "PG&E", CompanyCategory.Energy, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("coned.com", "Con Edison", CompanyCategory.Energy, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        // Real Estate
        new("zillow.com", "Zillow", CompanyCategory.RealEstate, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("redfin.com", "Redfin", CompanyCategory.RealEstate, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("realtor.com", "Realtor.com", CompanyCategory.RealEstate, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("apartments.com", "Apartments.com", CompanyCategory.RealEstate, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        // Logistics
        new("fedex.com", "FedEx", CompanyCategory.Logistics, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("ups.com", "UPS", CompanyCategory.Logistics, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("usps.com", "USPS", CompanyCategory.Logistics, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        // Fitness
        new("onepeloton.com", "Peloton", CompanyCategory.Fitness, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("planetfitness.com", "Planet Fitness", CompanyCategory.Fitness, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("strava.com", "Strava", CompanyCategory.Fitness, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("myfitnesspal.com", "MyFitnessPal", CompanyCategory.Fitness, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        // Government
        new("irs.gov", "IRS", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("ssa.gov", "Social Security", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("login.gov", "Login.gov", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("digitalinfra.gov", "Department of Digital Infrastructure", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("publicsafety.gov", "Bureau of Public Safety", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("consumerprotect.gov", "Office of Consumer Protection", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("transportsafety.gov", "National Transportation Safety Board", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("civicrecords.gov", "Civic Records Administration", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("cascadia.state.gov", "State of Cascadia Portal", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("parkswildlife.gov", "National Parks & Wildlife Service", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        // ECommerce (additional)
        new("ebay.com", "eBay", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("wish.com", "Wish", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        // Productivity (additional)
        new("apple.com", "Apple", CompanyCategory.Productivity, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("microsoft.com", "Microsoft", CompanyCategory.Productivity, CompanyType.Hybrid, GeographicRegion.NorthAmerica),
        // Marketing
        new("mailchimp.com", "Mailchimp", CompanyCategory.Marketing, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("canva.com", "Canva", CompanyCategory.Marketing, CompanyType.Hybrid, GeographicRegion.NorthAmerica),
        // Financial (additional)
        new("paypal.com", "PayPal", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("venmo.com", "Venmo", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("robinhood.com", "Robinhood", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        new("coinbase.com", "Coinbase", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.NorthAmerica),
        // Security (additional)
        new("zscaler.com", "Zscaler", CompanyCategory.Security, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("sentinelone.com", "SentinelOne", CompanyCategory.Security, CompanyType.Enterprise, GeographicRegion.NorthAmerica),
        new("nordvpn.com", "NordVPN", CompanyCategory.Security, CompanyType.Consumer, GeographicRegion.NorthAmerica)
    ];

    internal static readonly Company[] Europe =
    [
        // Enterprise Software
        new("sap.com", "SAP", CompanyCategory.FinanceERP, CompanyType.Enterprise, GeographicRegion.Europe),
        new("elastic.co", "Elastic", CompanyCategory.Analytics, CompanyType.Enterprise, GeographicRegion.Europe),
        new("atlassian.com", "Atlassian", CompanyCategory.ProjectManagement, CompanyType.Enterprise, GeographicRegion.Europe),
        // Fintech
        new("wise.com", "Wise", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.Europe),
        new("revolut.com", "Revolut", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.Europe),
        new("klarna.com", "Klarna", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.Europe),
        new("n26.com", "N26", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.Europe),
        // Developer Tools
        new("gitlab.com", "GitLab", CompanyCategory.DevOps, CompanyType.Enterprise, GeographicRegion.Europe),
        new("contentful.com", "Contentful", CompanyCategory.Developer, CompanyType.Enterprise, GeographicRegion.Europe),
        // Consumer Services
        new("deliveroo.com", "Deliveroo", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.Europe),
        new("booking.com", "Booking.com", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.Europe),
        // Collaboration
        new("miro.com", "Miro", CompanyCategory.Collaboration, CompanyType.Enterprise, GeographicRegion.Europe),
        new("intercom.io", "Intercom", CompanyCategory.CRM, CompanyType.Enterprise, GeographicRegion.Europe),
        // Business Software
        new("sage.com", "Sage", CompanyCategory.FinanceERP, CompanyType.Enterprise, GeographicRegion.Europe),
        new("adyen.com", "Adyen", CompanyCategory.Financial, CompanyType.Enterprise, GeographicRegion.Europe),
        // Banking
        new("hsbc.com", "HSBC", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Europe),
        new("barclays.co.uk", "Barclays", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Europe),
        new("db.com", "Deutsche Bank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Europe),
        new("bnpparibas.com", "BNP Paribas", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Europe),
        new("ing.com", "ING", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Europe),
        new("santander.com", "Santander", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Europe),
        new("ubs.com", "UBS", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Europe),
        new("lloydsbank.com", "Lloyds Bank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Europe),
        new("rabobank.com", "Rabobank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Europe),
        new("monzo.com", "Monzo", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Europe),
        new("starlingbank.com", "Starling Bank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Europe),
        new("nationwide.co.uk", "Nationwide", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Europe),
        // Airlines
        new("lufthansa.com", "Lufthansa", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Europe),
        new("britishairways.com", "British Airways", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Europe),
        new("airfrance.com", "Air France", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Europe),
        new("klm.com", "KLM", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Europe),
        new("ryanair.com", "Ryanair", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Europe),
        new("easyjet.com", "easyJet", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Europe),
        new("flysas.com", "SAS", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Europe),
        new("swiss.com", "Swiss", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Europe),
        new("vueling.com", "Vueling", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Europe),
        new("iberia.com", "Iberia", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Europe),
        new("norwegian.com", "Norwegian", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Europe),
        new("finnair.com", "Finnair", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Europe),
        new("wizzair.com", "Wizz Air", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Europe),
        new("tap.pt", "TAP Portugal", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Europe),
        new("aegeanair.com", "Aegean Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Europe),
        // Travel
        new("skyscanner.net", "Skyscanner", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.Europe),
        new("thetrainline.com", "Trainline", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.Europe),
        new("omio.com", "Omio", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.Europe),
        new("getyourguide.com", "GetYourGuide", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.Europe),
        new("lastminute.com", "Lastminute.com", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.Europe),
        new("opodo.com", "Opodo", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.Europe),
        // Hotels
        new("accor.com", "Accor", CompanyCategory.Hotels, CompanyType.Consumer, GeographicRegion.Europe),
        new("premierinn.com", "Premier Inn", CompanyCategory.Hotels, CompanyType.Consumer, GeographicRegion.Europe),
        new("travelodge.co.uk", "Travelodge", CompanyCategory.Hotels, CompanyType.Consumer, GeographicRegion.Europe),
        new("nh-hotels.com", "NH Hotels", CompanyCategory.Hotels, CompanyType.Consumer, GeographicRegion.Europe),
        new("melia.com", "Melia Hotels", CompanyCategory.Hotels, CompanyType.Consumer, GeographicRegion.Europe),
        new("scandichotels.com", "Scandic Hotels", CompanyCategory.Hotels, CompanyType.Consumer, GeographicRegion.Europe),
        // Car Rental
        new("europcar.com", "Europcar", CompanyCategory.CarRental, CompanyType.Consumer, GeographicRegion.Europe),
        new("sixt.com", "Sixt", CompanyCategory.CarRental, CompanyType.Consumer, GeographicRegion.Europe),
        // Rail
        new("eurostar.com", "Eurostar", CompanyCategory.Rail, CompanyType.Consumer, GeographicRegion.Europe),
        new("bahn.de", "Deutsche Bahn", CompanyCategory.Rail, CompanyType.Consumer, GeographicRegion.Europe),
        new("sncf.com", "SNCF", CompanyCategory.Rail, CompanyType.Consumer, GeographicRegion.Europe),
        new("trenitalia.com", "Trenitalia", CompanyCategory.Rail, CompanyType.Consumer, GeographicRegion.Europe),
        new("sbb.ch", "SBB", CompanyCategory.Rail, CompanyType.Consumer, GeographicRegion.Europe),
        new("renfe.com", "Renfe", CompanyCategory.Rail, CompanyType.Consumer, GeographicRegion.Europe),
        new("ns.nl", "NS Dutch Railways", CompanyCategory.Rail, CompanyType.Consumer, GeographicRegion.Europe),
        new("thalys.com", "Thalys", CompanyCategory.Rail, CompanyType.Consumer, GeographicRegion.Europe),
        // Ride Share
        new("bolt.eu", "Bolt", CompanyCategory.RideShare, CompanyType.Consumer, GeographicRegion.Europe),
        new("freenow.com", "Free Now", CompanyCategory.RideShare, CompanyType.Consumer, GeographicRegion.Europe),
        new("blablacar.com", "BlaBlaCar", CompanyCategory.RideShare, CompanyType.Consumer, GeographicRegion.Europe),
        // Insurance
        new("allianz.com", "Allianz", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.Europe),
        new("axa.com", "AXA", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.Europe),
        new("zurich.com", "Zurich Insurance", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.Europe),
        new("aviva.com", "Aviva", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.Europe),
        new("generali.com", "Generali", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.Europe),
        new("aegon.com", "Aegon", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.Europe),
        // Healthcare
        new("doctolib.fr", "Doctolib", CompanyCategory.Healthcare, CompanyType.Consumer, GeographicRegion.Europe),
        new("nhs.uk", "NHS", CompanyCategory.Healthcare, CompanyType.Consumer, GeographicRegion.Europe),
        new("babylonhealth.com", "Babylon Health", CompanyCategory.Healthcare, CompanyType.Consumer, GeographicRegion.Europe),
        new("shop-apotheke.com", "Shop Apotheke", CompanyCategory.Healthcare, CompanyType.Consumer, GeographicRegion.Europe),
        // Telecom
        new("vodafone.com", "Vodafone", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.Europe),
        new("telekom.de", "Deutsche Telekom", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.Europe),
        new("orange.com", "Orange", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.Europe),
        new("telefonica.com", "Telefonica", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.Europe),
        new("bt.com", "BT", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.Europe),
        new("three.co.uk", "Three", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.Europe),
        new("swisscom.ch", "Swisscom", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.Europe),
        new("telenor.com", "Telenor", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.Europe),
        // Education
        new("futurelearn.com", "FutureLearn", CompanyCategory.Education, CompanyType.Consumer, GeographicRegion.Europe),
        new("duolingo.com", "Duolingo", CompanyCategory.Education, CompanyType.Consumer, GeographicRegion.Europe),
        new("babbel.com", "Babbel", CompanyCategory.Education, CompanyType.Consumer, GeographicRegion.Europe),
        new("rnp.ac.uk", "Royal Northern Polytechnic", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.Europe),
        new("westberg.se", "Westberg University", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.Europe),
        new("institut-lumiere.fr", "Institut Lumière", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.Europe),
        new("rheinberg.de", "Rheinberg Technical University", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.Europe),
        new("acs.nl", "Amsterdam College of Sciences", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.Europe),
        new("torrealta.es", "Universidad Torre Alta", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.Europe),
        // Retail
        new("ikea.com", "IKEA", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.Europe),
        new("zara.com", "Zara", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.Europe),
        new("hm.com", "H&M", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.Europe),
        new("asos.com", "ASOS", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.Europe),
        new("zalando.com", "Zalando", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.Europe),
        new("johnlewis.com", "John Lewis", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.Europe),
        new("tesco.com", "Tesco", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.Europe),
        new("carrefour.com", "Carrefour", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.Europe),
        new("primark.com", "Primark", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.Europe),
        new("decathlon.com", "Decathlon", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.Europe),
        new("lidl.com", "Lidl", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.Europe),
        new("aldi.com", "Aldi", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.Europe),
        // Food & Beverage
        new("just-eat.com", "Just Eat", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.Europe),
        new("nespresso.com", "Nespresso", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.Europe),
        new("costa.co.uk", "Costa Coffee", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.Europe),
        new("hellofresh.com", "HelloFresh", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.Europe),
        new("wolt.com", "Wolt", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.Europe),
        new("glovo.com", "Glovo", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.Europe),
        // Automotive
        new("bmw.com", "BMW", CompanyCategory.Automotive, CompanyType.Consumer, GeographicRegion.Europe),
        new("mercedes-benz.com", "Mercedes-Benz", CompanyCategory.Automotive, CompanyType.Consumer, GeographicRegion.Europe),
        new("volkswagen.com", "Volkswagen", CompanyCategory.Automotive, CompanyType.Consumer, GeographicRegion.Europe),
        new("volvocars.com", "Volvo", CompanyCategory.Automotive, CompanyType.Consumer, GeographicRegion.Europe),
        new("audi.com", "Audi", CompanyCategory.Automotive, CompanyType.Consumer, GeographicRegion.Europe),
        new("porsche.com", "Porsche", CompanyCategory.Automotive, CompanyType.Consumer, GeographicRegion.Europe),
        new("renault.com", "Renault", CompanyCategory.Automotive, CompanyType.Consumer, GeographicRegion.Europe),
        new("ferrari.com", "Ferrari", CompanyCategory.Automotive, CompanyType.Consumer, GeographicRegion.Europe),
        // Gaming
        new("ubisoft.com", "Ubisoft", CompanyCategory.Gaming, CompanyType.Consumer, GeographicRegion.Europe),
        new("gog.com", "GOG", CompanyCategory.Gaming, CompanyType.Consumer, GeographicRegion.Europe),
        new("supercell.com", "Supercell", CompanyCategory.Gaming, CompanyType.Consumer, GeographicRegion.Europe),
        // News & Media
        new("bbc.co.uk", "BBC", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.Europe),
        new("theguardian.com", "The Guardian", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.Europe),
        new("spiegel.de", "Der Spiegel", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.Europe),
        new("lemonde.fr", "Le Monde", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.Europe),
        new("ft.com", "Financial Times", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.Europe),
        new("elpais.com", "El Pais", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.Europe),
        new("corriere.it", "Corriere della Sera", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.Europe),
        // Streaming (additional)
        new("sky.com", "Sky", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.Europe),
        new("dazn.com", "DAZN", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.Europe),
        new("canalplus.com", "Canal+", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.Europe),
        // Social Media (additional)
        new("vk.com", "VK", CompanyCategory.SocialMedia, CompanyType.Consumer, GeographicRegion.Europe),
        new("vinted.com", "Vinted", CompanyCategory.SocialMedia, CompanyType.Consumer, GeographicRegion.Europe),
        // Energy
        new("shell.com", "Shell", CompanyCategory.Energy, CompanyType.Consumer, GeographicRegion.Europe),
        new("bp.com", "BP", CompanyCategory.Energy, CompanyType.Consumer, GeographicRegion.Europe),
        new("edf.fr", "EDF", CompanyCategory.Energy, CompanyType.Consumer, GeographicRegion.Europe),
        new("vattenfall.com", "Vattenfall", CompanyCategory.Energy, CompanyType.Consumer, GeographicRegion.Europe),
        new("engie.com", "Engie", CompanyCategory.Energy, CompanyType.Consumer, GeographicRegion.Europe),
        // Real Estate
        new("rightmove.co.uk", "Rightmove", CompanyCategory.RealEstate, CompanyType.Consumer, GeographicRegion.Europe),
        new("zoopla.co.uk", "Zoopla", CompanyCategory.RealEstate, CompanyType.Consumer, GeographicRegion.Europe),
        new("immobilienscout24.de", "ImmobilienScout24", CompanyCategory.RealEstate, CompanyType.Consumer, GeographicRegion.Europe),
        new("idealista.com", "Idealista", CompanyCategory.RealEstate, CompanyType.Consumer, GeographicRegion.Europe),
        // Logistics
        new("dhl.com", "DHL", CompanyCategory.Logistics, CompanyType.Consumer, GeographicRegion.Europe),
        new("royalmail.com", "Royal Mail", CompanyCategory.Logistics, CompanyType.Consumer, GeographicRegion.Europe),
        new("postnl.nl", "PostNL", CompanyCategory.Logistics, CompanyType.Consumer, GeographicRegion.Europe),
        new("dpd.com", "DPD", CompanyCategory.Logistics, CompanyType.Consumer, GeographicRegion.Europe),
        // Fitness
        new("puregym.com", "PureGym", CompanyCategory.Fitness, CompanyType.Consumer, GeographicRegion.Europe),
        new("thegymgroup.com", "The Gym Group", CompanyCategory.Fitness, CompanyType.Consumer, GeographicRegion.Europe),
        new("freeletics.com", "Freeletics", CompanyCategory.Fitness, CompanyType.Consumer, GeographicRegion.Europe),
        // Government
        new("gov.uk", "GOV.UK", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.Europe),
        new("service-public.fr", "Service-Public.fr", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.Europe),
        new("elster.de", "ELSTER", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.Europe),
        new("digitalservices.europa.eu", "EU Digital Services Agency", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.Europe),
        new("mfa.gov.uk", "Ministry of Foreign Affairs UK", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.Europe),
        new("bundesamt-cyber.de", "Federal Office for Cybersecurity", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.Europe),
        new("agenzia-fiscale.it", "Agenzia delle Entrate Digitale", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.Europe),
        new("riksarkivet.se", "National Archives of Sweden", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.Europe),
        new("transport.gov.ie", "Department of Transport Ireland", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.Europe)
    ];

    internal static readonly Company[] AsiaPacific =
    [
        // Chinese Tech Giants
        new("alibaba.com", "Alibaba", CompanyCategory.ECommerce, CompanyType.Hybrid, GeographicRegion.AsiaPacific),
        new("tencent.com", "Tencent", CompanyCategory.SocialMedia, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("bytedance.com", "ByteDance", CompanyCategory.SocialMedia, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("wechat.com", "WeChat", CompanyCategory.SocialMedia, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Japanese Companies
        new("rakuten.com", "Rakuten", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("line.me", "Line", CompanyCategory.SocialMedia, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("sony.com", "Sony", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("paypay.ne.jp", "PayPay", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Korean Companies
        new("samsung.com", "Samsung", CompanyCategory.Productivity, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Southeast Asian Companies
        new("grab.com", "Grab", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("sea.com", "Sea Limited", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("shopee.com", "Shopee", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("lazada.com", "Lazada", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("gojek.com", "Gojek", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Indian Companies
        new("flipkart.com", "Flipkart", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Banking
        new("icbc.com.cn", "ICBC", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("dbs.com", "DBS", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("mufg.jp", "MUFG", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("smbc.co.jp", "SMBC", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("anz.com.au", "ANZ", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("commbank.com.au", "Commonwealth Bank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("hdfcbank.com", "HDFC Bank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("kotak.com", "Kotak Mahindra", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("sbi.co.in", "State Bank of India", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("nab.com.au", "NAB", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Airlines
        new("singaporeair.com", "Singapore Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("ana.co.jp", "ANA", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("jal.co.jp", "Japan Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("cathaypacific.com", "Cathay Pacific", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("qantas.com", "Qantas", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("airindia.com", "Air India", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("koreanair.com", "Korean Air", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("thaiairways.com", "Thai Airways", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("vietnamairlines.com", "Vietnam Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("airasia.com", "AirAsia", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("jetstar.com", "Jetstar", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("cebuair.com", "Cebu Pacific", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Travel
        new("makemytrip.com", "MakeMyTrip", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("agoda.com", "Agoda", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("trip.com", "Trip.com", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("traveloka.com", "Traveloka", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("klook.com", "Klook", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Hotels
        new("shangri-la.com", "Shangri-La", CompanyCategory.Hotels, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("apahotel.com", "APA Hotels", CompanyCategory.Hotels, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("oyorooms.com", "OYO", CompanyCategory.Hotels, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("tajhotels.com", "Taj Hotels", CompanyCategory.Hotels, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Rail
        new("jreast.co.jp", "JR East", CompanyCategory.Rail, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("irctc.co.in", "IRCTC", CompanyCategory.Rail, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("korail.com", "Korail", CompanyCategory.Rail, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Ride Share
        new("ola.com", "Ola", CompanyCategory.RideShare, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("didiglobal.com", "DiDi", CompanyCategory.RideShare, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Insurance
        new("pingan.com", "Ping An", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("aia.com", "AIA", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("lifeinsurance.com.au", "TAL", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Healthcare
        new("practo.com", "Practo", CompanyCategory.Healthcare, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("1mg.com", "1mg", CompanyCategory.Healthcare, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("halodoc.com", "Halodoc", CompanyCategory.Healthcare, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Telecom
        new("nttdocomo.co.jp", "NTT Docomo", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("singtel.com", "Singtel", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("telstra.com.au", "Telstra", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("jio.com", "Jio", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("sktelecom.com", "SK Telecom", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("airtel.in", "Airtel India", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Education
        new("byjus.com", "Byju's", CompanyCategory.Education, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("unacademy.com", "Unacademy", CompanyCategory.Education, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("sakura.ac.jp", "Sakura National University", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.AsiaPacific),
        new("meltechcollege.edu.au", "Melbourne Technical College", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.AsiaPacific),
        new("sitd.edu.sg", "Singapore Institute of Technology & Design", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.AsiaPacific),
        new("hgu.edu.in", "Hyderabad Global University", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.AsiaPacific),
        new("sdu.ac.kr", "Seoul Digital University", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.AsiaPacific),
        // Retail
        new("uniqlo.com", "Uniqlo", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("muji.com", "Muji", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("tokopedia.com", "Tokopedia", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("jd.com", "JD.com", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("myntra.com", "Myntra", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("bigbasket.com", "BigBasket", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Food & Beverage
        new("zomato.com", "Zomato", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("swiggy.com", "Swiggy", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("foodpanda.com", "Foodpanda", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("meituan.com", "Meituan", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Automotive
        new("toyota.com", "Toyota", CompanyCategory.Automotive, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("honda.com", "Honda", CompanyCategory.Automotive, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("hyundai.com", "Hyundai", CompanyCategory.Automotive, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("byd.com", "BYD", CompanyCategory.Automotive, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("suzuki.com", "Suzuki", CompanyCategory.Automotive, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Gaming
        new("nintendo.com", "Nintendo", CompanyCategory.Gaming, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("bandainamcoent.com", "Bandai Namco", CompanyCategory.Gaming, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("hoyoverse.com", "HoYoverse", CompanyCategory.Gaming, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("garena.com", "Garena", CompanyCategory.Gaming, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("nexon.com", "Nexon", CompanyCategory.Gaming, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // News & Media
        new("nikkei.com", "Nikkei", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("scmp.com", "South China Morning Post", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("timesofindia.com", "Times of India", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("straitstimes.com", "Straits Times", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Streaming (additional)
        new("iq.com", "iQIYI", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("hotstar.com", "Hotstar", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("viki.com", "Viki", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Social Media (additional)
        new("weibo.com", "Weibo", CompanyCategory.SocialMedia, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("kakaocorp.com", "KakaoTalk", CompanyCategory.SocialMedia, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("naver.com", "Naver", CompanyCategory.SocialMedia, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Energy
        new("tepco.co.jp", "TEPCO", CompanyCategory.Energy, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("originenergy.com.au", "Origin Energy", CompanyCategory.Energy, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Logistics
        new("kuronekoyamato.co.jp", "Yamato Transport", CompanyCategory.Logistics, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("sf-express.com", "SF Express", CompanyCategory.Logistics, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("auspost.com.au", "Australia Post", CompanyCategory.Logistics, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Financial (additional)
        new("phonepe.com", "PhonePe", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("gpay.com", "Google Pay India", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Fitness
        new("cultfit.com", "Cult.fit", CompanyCategory.Fitness, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        // Government
        new("mygov.in", "MyGov India", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("my.gov.au", "myGov Australia", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("digital.go.jp", "Japan Digital Agency", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.AsiaPacific),
        new("smartnation.gov.sg", "Smart Nation Singapore", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.AsiaPacific),
        new("digilocker.gov.in", "DigiLocker India", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("ato.gov.au", "Australian Taxation Office", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.AsiaPacific),
        new("minof-science.go.kr", "Ministry of Science & ICT Korea", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.AsiaPacific)
    ];

    internal static readonly Company[] LatinAmerica =
    [
        // Banking
        new("bancodobrasil.com.br", "Banco do Brasil", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("itau.com.br", "Itau", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("nubank.com.br", "Nubank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("banorte.com", "Banorte", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("bbva.mx", "BBVA Mexico", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("bancolombia.com", "Bancolombia", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("bradesco.com.br", "Bradesco", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("bcp.com.pe", "BCP Peru", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // Airlines
        new("latamairlines.com", "LATAM Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("avianca.com", "Avianca", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("aeromexico.com", "Aeromexico", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("copaair.com", "Copa Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("voegol.com.br", "Gol Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("voeazul.com.br", "Azul Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // Travel
        new("despegar.com", "Despegar", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("decolar.com", "Decolar", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // ECommerce
        new("mercadolibre.com", "MercadoLibre", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("americanas.com.br", "Americanas", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("magazineluiza.com.br", "Magazine Luiza", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("falabella.com", "Falabella", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("linio.com", "Linio", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // Telecom
        new("claro.com.br", "Claro", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("vivo.com.br", "Vivo", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("telmex.com", "Telmex", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("entel.cl", "Entel", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // Financial
        new("mercadopago.com", "MercadoPago", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("picpay.com", "PicPay", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("ualabis.com.ar", "Uala", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // Food & Beverage
        new("ifood.com.br", "iFood", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("rappi.com", "Rappi", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("pedidosya.com", "PedidosYa", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // Ride Share
        new("99app.com", "99", CompanyCategory.RideShare, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("beat.co", "Beat", CompanyCategory.RideShare, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // Streaming
        new("globoplay.globo.com", "Globoplay", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("blim.com", "Blim TV", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // Retail
        new("liverpool.com.mx", "Liverpool", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("coppel.com", "Coppel", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("casasbahia.com.br", "Casas Bahia", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // News & Media
        new("globo.com", "Globo", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("eluniversal.com.mx", "El Universal", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // Government
        new("gov.br", "GOV.BR", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("sat.gob.mx", "SAT Mexico", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("serpro.gov.br", "Serviço Federal de Processamento", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.LatinAmerica),
        new("dgii.gob.do", "Dirección General de Impuestos", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.LatinAmerica),
        new("anses.gob.ar", "ANSES Argentina", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // Insurance
        new("sulamerica.com.br", "SulAmerica", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("portoseguro.com.br", "Porto Seguro", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // Healthcare
        new("drogasil.com.br", "Drogasil", CompanyCategory.Healthcare, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("docplanner.com", "Doctoralia", CompanyCategory.Healthcare, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        // Education
        new("platzi.com", "Platzi", CompanyCategory.Education, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("domestika.org", "Domestika", CompanyCategory.Education, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("unovasp.edu.br", "Universidade Nova de São Paulo", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.LatinAmerica),
        new("itpacifico.edu.mx", "Instituto Tecnológico del Pacífico", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.LatinAmerica),
        new("uac.edu.ar", "Universidad Austral de Ciencias", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.LatinAmerica),
        // Energy
        new("petrobras.com.br", "Petrobras", CompanyCategory.Energy, CompanyType.Consumer, GeographicRegion.LatinAmerica),
        new("ecopetrol.com.co", "Ecopetrol", CompanyCategory.Energy, CompanyType.Consumer, GeographicRegion.LatinAmerica)
    ];

    internal static readonly Company[] MiddleEast =
    [
        // Banking
        new("emiratesnbd.com", "Emirates NBD", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("alrajhibank.com.sa", "Al Rajhi Bank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("qnb.com", "QNB", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("garanti.com.tr", "Garanti BBVA", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("isbank.com.tr", "Isbank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("kfh.com", "Kuwait Finance House", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("mashreqbank.com", "Mashreq", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("bankfab.com", "First Abu Dhabi Bank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // Airlines
        new("emirates.com", "Emirates", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("qatarairways.com", "Qatar Airways", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("etihad.com", "Etihad Airways", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("turkishairlines.com", "Turkish Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("saudia.com", "Saudia", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("flynas.com", "Flynas", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("pegasus.aero", "Pegasus Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // Travel
        new("wego.com", "Wego", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("almosafer.com", "Almosafer", CompanyCategory.Travel, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // Hotels
        new("rotana.com", "Rotana Hotels", CompanyCategory.Hotels, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("jumeirah.com", "Jumeirah", CompanyCategory.Hotels, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // Telecom
        new("etisalat.ae", "Etisalat", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("stc.com.sa", "STC", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("turkcell.com.tr", "Turkcell", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("zain.com", "Zain", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("du.ae", "Du", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // ECommerce
        new("noon.com", "Noon", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("trendyol.com", "Trendyol", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("hepsiburada.com", "Hepsiburada", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("amazon.ae", "Amazon AE", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // Food & Beverage
        new("talabat.com", "Talabat", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("yemeksepeti.com", "Yemeksepeti", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("hungerstation.com", "HungerStation", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // Ride Share
        new("careem.com", "Careem", CompanyCategory.RideShare, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // Financial
        new("stcpay.com.sa", "STC Pay", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("paytabs.com", "PayTabs", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // News & Media
        new("aljazeera.com", "Al Jazeera", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("hurriyet.com.tr", "Hurriyet", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("arabnews.com", "Arab News", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // Real Estate
        new("bayut.com", "Bayut", CompanyCategory.RealEstate, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("propertyfinder.ae", "Property Finder", CompanyCategory.RealEstate, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("sahibinden.com", "Sahibinden", CompanyCategory.RealEstate, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // Government
        new("absher.sa", "Absher", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("turkiye.gov.tr", "e-Devlet", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("smartgov.ae", "Smart Government UAE", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.MiddleEast),
        new("moc.gov.sa", "Ministry of Commerce Saudi Arabia", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.MiddleEast),
        new("hukomet.gov.qa", "Hukomet Qatar", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.MiddleEast),
        // Education
        new("gulfinst.edu.ae", "Gulf Institute of Technology", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.MiddleEast),
        new("rus.edu.sa", "Riyadh University of Science", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.MiddleEast),
        new("bosphorus.edu.tr", "Bosphorus Technical University", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.MiddleEast),
        // Insurance
        new("tawuniya.com.sa", "Tawuniya", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.MiddleEast),
        // Retail
        new("lcwaikiki.com", "LC Waikiki", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("extrastores.com", "Extra Stores", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.MiddleEast),
        new("defacto.com.tr", "DeFacto", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.MiddleEast)
    ];

    internal static readonly Company[] Africa =
    [
        // Banking
        new("standardbank.co.za", "Standard Bank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Africa),
        new("fnb.co.za", "FNB", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Africa),
        new("accessbankplc.com", "Access Bank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Africa),
        new("equitybankgroup.com", "Equity Bank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Africa),
        new("absa.co.za", "Absa", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Africa),
        new("gtbank.com", "GTBank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Africa),
        new("nedbank.co.za", "Nedbank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Africa),
        new("kcbgroup.com", "KCB Bank", CompanyCategory.Banking, CompanyType.Consumer, GeographicRegion.Africa),
        // Telecom
        new("mtn.com", "MTN", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.Africa),
        new("safaricom.co.ke", "Safaricom", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.Africa),
        new("vodacom.co.za", "Vodacom", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.Africa),
        new("airtel.africa", "Airtel Africa", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.Africa),
        new("iam.ma", "Maroc Telecom", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.Africa),
        new("telkom.co.za", "Telkom SA", CompanyCategory.Telecom, CompanyType.Consumer, GeographicRegion.Africa),
        // Financial
        new("opayweb.com", "OPay", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.Africa),
        new("flutterwave.com", "Flutterwave", CompanyCategory.Financial, CompanyType.Enterprise, GeographicRegion.Africa),
        new("paystack.com", "Paystack", CompanyCategory.Financial, CompanyType.Enterprise, GeographicRegion.Africa),
        new("chippercash.com", "Chipper Cash", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.Africa),
        new("moniepoint.com", "Moniepoint", CompanyCategory.Financial, CompanyType.Consumer, GeographicRegion.Africa),
        // Airlines
        new("ethiopianairlines.com", "Ethiopian Airlines", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Africa),
        new("flysaa.com", "South African Airways", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Africa),
        new("kenya-airways.com", "Kenya Airways", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Africa),
        new("egyptair.com", "EgyptAir", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Africa),
        new("royalairmaroc.com", "Royal Air Maroc", CompanyCategory.Airlines, CompanyType.Consumer, GeographicRegion.Africa),
        // ECommerce
        new("jumia.com", "Jumia", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.Africa),
        new("takealot.com", "Takealot", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.Africa),
        new("konga.com", "Konga", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.Africa),
        // Food & Beverage
        new("mrd.co.za", "Mr D Food", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.Africa),
        new("chowdeck.com", "Chowdeck", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.Africa),
        new("yassir.com", "Yassir", CompanyCategory.FoodBeverage, CompanyType.Consumer, GeographicRegion.Africa),
        // Ride Share
        new("indrive.com", "inDrive", CompanyCategory.RideShare, CompanyType.Consumer, GeographicRegion.Africa),
        // News & Media
        new("news24.com", "News24", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.Africa),
        new("nation.africa", "Nation Africa", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.Africa),
        new("dailymaverick.co.za", "Daily Maverick", CompanyCategory.News, CompanyType.Consumer, GeographicRegion.Africa),
        // Streaming
        new("showmax.com", "Showmax", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.Africa),
        new("dstv.com", "DStv", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.Africa),
        // Insurance
        new("oldmutual.co.za", "Old Mutual", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.Africa),
        new("discovery.co.za", "Discovery", CompanyCategory.Insurance, CompanyType.Consumer, GeographicRegion.Africa),
        // Education
        new("ulesson.com", "uLesson", CompanyCategory.Education, CompanyType.Consumer, GeographicRegion.Africa),
        new("alxafrica.com", "ALX", CompanyCategory.Education, CompanyType.Consumer, GeographicRegion.Africa),
        new("cctu.edu.gh", "Cape Coast Technical University", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.Africa),
        new("jcc.edu.za", "Johannesburg College of Commerce", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.Africa),
        new("nit.ac.ke", "Nairobi Institute of Technology", CompanyCategory.Education, CompanyType.Enterprise, GeographicRegion.Africa),
        // Government
        new("sars.gov.za", "SARS eFiling", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.Africa),
        new("nimc.gov.ng", "NIMC Nigeria", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.Africa),
        new("ecitizen.go.ke", "eCitizen Kenya", CompanyCategory.Government, CompanyType.Consumer, GeographicRegion.Africa),
        new("dha.gov.za", "Department of Home Affairs SA", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.Africa),
        new("digital.gov.rw", "Rwanda Digital Governance Board", CompanyCategory.Government, CompanyType.Enterprise, GeographicRegion.Africa),
        // Retail
        new("shoprite.co.za", "Shoprite", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.Africa),
        new("woolworths.co.za", "Woolworths SA", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.Africa),
        new("checkers.co.za", "Checkers", CompanyCategory.Retail, CompanyType.Consumer, GeographicRegion.Africa),
        // Logistics
        new("thecourierguys.co.za", "The Courier Guy", CompanyCategory.Logistics, CompanyType.Consumer, GeographicRegion.Africa),
        // Energy
        new("eskom.co.za", "Eskom", CompanyCategory.Energy, CompanyType.Consumer, GeographicRegion.Africa)
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
