using Bit.Seeder.Data.Enums;

namespace Bit.Seeder.Data;

internal sealed record OrgUnit(string Name, string[]? SubUnits = null);

internal sealed record OrgStructure(OrgStructureModel Model, OrgUnit[] Units);

/// <summary>
/// Pre-defined organizational structures for different company models.
/// </summary>
internal static class OrgStructures
{
    public static readonly OrgStructure Traditional = new(OrgStructureModel.Traditional,
    [
        new("Executive", ["CEO Office", "Strategy", "Board Relations"]),
        new("Finance", ["Accounting", "FP&A", "Treasury", "Tax", "Audit"]),
        new("Human Resources", ["Recruiting", "Benefits", "Training", "Employee Relations", "Compensation"]),
        new("Information Technology", ["Infrastructure", "Security", "Support", "Enterprise Apps", "Network"]),
        new("Marketing", ["Brand", "Digital Marketing", "Content", "Events", "PR"]),
        new("Sales", ["Enterprise Sales", "SMB Sales", "Sales Operations", "Account Management", "Inside Sales"]),
        new("Operations", ["Facilities", "Procurement", "Supply Chain", "Quality", "Business Operations"]),
        new("Research & Development", ["Product Development", "Innovation", "Research", "Prototyping"]),
        new("Legal", ["Corporate Legal", "Compliance", "Contracts", "IP", "Privacy"]),
        new("Customer Success", ["Onboarding", "Support", "Customer Education", "Renewals"]),
        new("Engineering", ["Backend", "Frontend", "Mobile", "QA", "DevOps", "Platform"]),
        new("Product", ["Product Management", "UX Design", "User Research", "Product Analytics"])
    ]);

    public static readonly OrgStructure Spotify = new(OrgStructureModel.Spotify,
    [
        // Tribes
        new("Payments Tribe", ["Checkout Squad", "Fraud Prevention Squad", "Billing Squad", "Payment Methods Squad"]),
        new("Growth Tribe", ["Acquisition Squad", "Activation Squad", "Retention Squad", "Monetization Squad"]),
        new("Platform Tribe", ["API Squad", "Infrastructure Squad", "Data Platform Squad", "Developer Tools Squad"]),
        new("Experience Tribe", ["Web App Squad", "Mobile Squad", "Desktop Squad", "Accessibility Squad"]),
        // Chapters
        new("Backend Chapter", ["Java Developers", "Go Developers", "Python Developers", "Database Specialists"]),
        new("Frontend Chapter", ["React Developers", "TypeScript Specialists", "Performance Engineers", "UI Engineers"]),
        new("QA Chapter", ["Test Automation", "Manual Testing", "Performance Testing", "Security Testing"]),
        new("Design Chapter", ["Product Designers", "UX Researchers", "Visual Designers", "Design Systems"]),
        new("Data Science Chapter", ["ML Engineers", "Data Analysts", "Data Engineers", "AI Researchers"]),
        // Guilds
        new("Security Guild"),
        new("Innovation Guild"),
        new("Architecture Guild"),
        new("Accessibility Guild"),
        new("Developer Experience Guild")
    ]);

    public static readonly OrgStructure Modern = new(OrgStructureModel.Modern,
    [
        // Feature Teams
        new("Auth Team", ["Identity", "SSO", "MFA", "Passwordless"]),
        new("Search Team", ["Indexing", "Ranking", "Query Processing", "Search UX"]),
        new("Notifications Team", ["Email", "Push", "In-App", "Preferences"]),
        new("Analytics Team", ["Tracking", "Dashboards", "Reporting", "Data Pipeline"]),
        new("Integrations Team", ["API Gateway", "Webhooks", "Third-Party Apps", "Marketplace"]),
        // Platform Teams
        new("Developer Experience", ["SDK", "Documentation", "Developer Portal", "API Design"]),
        new("Data Platform", ["Data Lake", "ETL", "Data Governance", "Real-Time Processing"]),
        new("ML Platform", ["Model Training", "Model Serving", "Feature Store", "MLOps"]),
        new("Security Platform", ["AppSec", "Infrastructure Security", "Security Tooling", "Compliance"]),
        new("Infrastructure Platform", ["Cloud", "Kubernetes", "Observability", "CI/CD"]),
        // Pods
        new("AI Assistant Pod", ["LLM Integration", "Prompt Engineering", "AI UX", "AI Safety"]),
        new("Performance Pod", ["Frontend Performance", "Backend Performance", "Database Optimization"]),
        new("Compliance Pod", ["SOC 2", "GDPR", "HIPAA", "Audit"]),
        new("Migration Pod", ["Legacy Systems", "Data Migration", "Cutover Planning"]),
        // Enablers
        new("Architecture", ["Technical Strategy", "System Design", "Tech Debt"]),
        new("Quality", ["Testing Strategy", "Release Quality", "Production Health"])
    ]);

    public static readonly OrgStructure[] All = [Traditional, Spotify, Modern];

    public static OrgStructure GetStructure(OrgStructureModel model) => model switch
    {
        OrgStructureModel.Traditional => Traditional,
        OrgStructureModel.Spotify => Spotify,
        OrgStructureModel.Modern => Modern,
        _ => Traditional
    };
}
