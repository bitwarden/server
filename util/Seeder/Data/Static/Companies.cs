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
        new("spotify.com", "Spotify", CompanyCategory.Streaming, CompanyType.Consumer, GeographicRegion.NorthAmerica)
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
        new("adyen.com", "Adyen", CompanyCategory.Financial, CompanyType.Enterprise, GeographicRegion.Europe)
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
        new("flipkart.com", "Flipkart", CompanyCategory.ECommerce, CompanyType.Consumer, GeographicRegion.AsiaPacific)
    ];

    internal static readonly Company[] All = [.. NorthAmerica, .. Europe, .. AsiaPacific];

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
