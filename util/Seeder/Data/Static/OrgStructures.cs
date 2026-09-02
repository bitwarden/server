using Bit.Seeder.Data.Enums;

namespace Bit.Seeder.Data.Static;

internal sealed record OrgUnit(string Name, string[]? SubUnits = null);

internal sealed record OrgStructure(OrgStructureModel Model, OrgUnit[] Units);

/// <summary>
/// Pre-defined organizational structures for different company models.
/// </summary>
internal static class OrgStructures
{
    internal static readonly OrgStructure Traditional = new(OrgStructureModel.Traditional,
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

    internal static readonly OrgStructure Spotify = new(OrgStructureModel.Spotify,
    [
        // Tribes (product verticals)
        new("Payments Tribe", ["Checkout Squad", "Fraud Prevention Squad", "Billing Squad", "Payment Methods Squad"]),
        new("Growth Tribe", ["Acquisition Squad", "Activation Squad", "Retention Squad", "Monetization Squad"]),
        new("Platform Tribe", ["API Squad", "Infrastructure Squad", "Data Platform Squad", "Developer Tools Squad"]),
        new("Experience Tribe", ["Web App Squad", "Mobile Squad", "Desktop Squad", "Accessibility Squad"]),
        new("Content Tribe", ["Catalog Squad", "Curation Squad", "Metadata Squad", "Licensing Squad"]),
        new("Marketplace Tribe", ["Discovery Squad", "Recommendations Squad", "Ads Squad", "Creator Tools Squad"]),
        new("Infrastructure Tribe", ["Cloud Squad", "Networking Squad", "Storage Squad", "Observability Squad"]),
        new("Partner Tribe", ["Integrations Squad", "Partner API Squad", "Onboarding Squad", "Compliance Squad"]),
        // Chapters (skill groups)
        new("Backend Chapter", ["Java Developers", "Go Developers", "Python Developers", "Database Specialists"]),
        new("Frontend Chapter", ["React Developers", "TypeScript Specialists", "Performance Engineers", "UI Engineers"]),
        new("QA Chapter", ["Test Automation", "Manual Testing", "Performance Testing", "Security Testing"]),
        new("Design Chapter", ["Product Designers", "UX Researchers", "Visual Designers", "Design Systems"]),
        new("Data Science Chapter", ["ML Engineers", "Data Analysts", "Data Engineers", "AI Researchers"]),
        new("DevOps Chapter", ["CI/CD Engineers", "Release Engineers", "Site Reliability", "Infrastructure Automation"]),
        new("Product Management Chapter", ["Product Owners", "Business Analysts", "Market Research", "Roadmap Strategy"]),
        new("Analytics Chapter", ["Metrics Engineers", "A/B Testing", "Business Intelligence", "Data Governance"]),
        // Guilds (communities of practice)
        new("Security Guild"),
        new("Innovation Guild"),
        new("Architecture Guild"),
        new("Accessibility Guild"),
        new("Developer Experience Guild"),
        new("Reliability Guild")
    ]);

    internal static readonly OrgStructure Modern = new(OrgStructureModel.Modern,
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

    internal static readonly OrgStructure Government = new(OrgStructureModel.Government,
    [
        new("Mayor's Office", ["Chief of Staff", "Policy Advisors", "Scheduling", "Constituent Services"]),
        new("City Council", ["Council Members", "Legislative Aides", "Clerk of Council", "Public Comment"]),
        new("Police Department", ["Patrol", "Investigations", "Community Policing", "Records", "Training"]),
        new("Fire Department", ["Suppression", "EMS", "Prevention", "Training", "Hazmat"]),
        new("Public Works", ["Roads", "Water", "Sewer", "Stormwater", "Fleet Maintenance"]),
        new("Parks & Recreation", ["Maintenance", "Programming", "Aquatics", "Forestry", "Events"]),
        new("Finance", ["Budget", "Accounting", "Payroll", "Purchasing", "Revenue"]),
        new("Planning & Zoning", ["Current Planning", "Long Range", "Code Enforcement", "GIS", "Historic Preservation"]),
        new("Human Services", ["Social Services", "Aging", "Veterans", "Homelessness", "Youth Programs"]),
        new("Library", ["Circulation", "Reference", "Children's Services", "Digital Services", "Branches"]),
        new("Information Technology", ["Infrastructure", "Applications", "Cybersecurity", "Help Desk", "GIS"]),
        new("Legal", ["City Attorney", "Risk Management", "Contracts", "Litigation", "Ethics"]),
        new("Clerk", ["Records Management", "Elections", "FOIA", "Licensing", "Archives"]),
        new("Economic Development", ["Business Attraction", "Grants", "Tourism", "Redevelopment", "Small Business"]),
        new("Housing", ["Inspections", "Code Enforcement", "Affordable Housing", "Community Development"]),
        new("Transportation", ["Traffic Engineering", "Transit", "Bike & Pedestrian", "Parking", "Signal Operations"]),
        new("Environmental Services", ["Solid Waste", "Recycling", "Sustainability", "Air Quality", "Watershed"]),
        new("Public Health", ["Epidemiology", "Inspections", "Immunizations", "Emergency Preparedness", "Health Education"])
    ]);

    internal static readonly OrgStructure SchoolDistrict = new(OrgStructureModel.SchoolDistrict,
    [
        // District Administration
        new("Superintendent's Office", ["Deputy Superintendent", "Board Liaison", "Strategic Planning", "Communications"]),
        new("Board of Education", ["Board Members", "Board Secretary", "Policy Committee", "Finance Committee"]),
        new("Curriculum & Instruction", ["Literacy", "STEM", "Social Studies", "World Languages", "Assessment"]),
        new("Student Services", ["Counseling", "School Psychology", "Social Work", "Health Services", "Section 504"]),
        new("Finance & Operations", ["Budget", "Accounting", "Payroll", "Purchasing", "Grants Management"]),
        new("Human Resources", ["Recruitment", "Certification", "Benefits", "Labor Relations", "Professional Development"]),
        new("Technology", ["Infrastructure", "Student Systems", "Instructional Technology", "Help Desk", "Data Analytics"]),
        new("Facilities", ["Maintenance", "Custodial", "Capital Projects", "Energy Management", "Safety"]),
        new("Transportation", ["Routing", "Fleet Maintenance", "Driver Training", "Special Needs Transport"]),
        new("Food Services", ["Menu Planning", "Kitchen Operations", "Nutrition", "Free & Reduced Lunch"]),
        new("Special Education", ["IEP Coordination", "Related Services", "Behavioral Support", "Transition Services"]),
        // Schools
        new("Lincoln Elementary", ["Grade K-2", "Grade 3-5", "Specials", "Student Support", "Media Center"]),
        new("Washington Elementary", ["Grade K-2", "Grade 3-5", "Specials", "Student Support", "Media Center"]),
        new("Jefferson Middle School", ["English", "Math", "Science", "Social Studies", "Electives", "Guidance"]),
        new("Roosevelt High School", ["English", "Math", "Science", "Social Studies", "CTE", "Athletics", "Guidance"]),
        new("Kennedy High School", ["English", "Math", "Science", "Social Studies", "CTE", "Athletics", "Guidance"])
    ]);

    internal static readonly OrgStructure Healthcare = new(OrgStructureModel.Healthcare,
    [
        new("Administration", ["Executive Suite", "Strategic Planning", "Quality Improvement", "Accreditation"]),
        new("Medical Staff", ["Chief Medical Officer", "Physician Credentialing", "Medical Records", "Clinical Research"]),
        new("Nursing", ["Nurse Managers", "Clinical Educators", "Staffing", "Infection Control", "Patient Safety"]),
        new("Emergency Department", ["Triage", "Trauma", "Observation", "Fast Track", "Crisis Intervention"]),
        new("Surgery", ["General Surgery", "Orthopedics", "Neurosurgery", "Pre-Op", "Post-Op", "Anesthesiology"]),
        new("Internal Medicine", ["Hospitalists", "Pulmonology", "Gastroenterology", "Nephrology", "Endocrinology"]),
        new("Pediatrics", ["General Pediatrics", "NICU", "Pediatric Surgery", "Child Life", "Adolescent Medicine"]),
        new("Obstetrics & Gynecology", ["Labor & Delivery", "Maternal-Fetal Medicine", "Gynecologic Surgery", "Midwifery"]),
        new("Cardiology", ["Interventional", "Electrophysiology", "Heart Failure", "Cardiac Rehab", "Cath Lab"]),
        new("Oncology", ["Medical Oncology", "Radiation Therapy", "Surgical Oncology", "Infusion Center", "Palliative Care"]),
        new("Radiology", ["Diagnostic Imaging", "Interventional Radiology", "MRI", "CT", "Ultrasound"]),
        new("Laboratory", ["Clinical Chemistry", "Hematology", "Microbiology", "Blood Bank", "Pathology"]),
        new("Pharmacy", ["Inpatient", "Outpatient", "Clinical Pharmacy", "Medication Safety", "Compounding"]),
        new("Physical Therapy", ["Inpatient Rehab", "Outpatient Rehab", "Occupational Therapy", "Speech Therapy"]),
        new("Mental Health", ["Psychiatry", "Psychology", "Social Work", "Substance Abuse", "Crisis Services"]),
        new("Compliance", ["Regulatory Affairs", "HIPAA Privacy", "Risk Management", "Internal Audit", "Ethics"]),
        new("Finance", ["Revenue Cycle", "Billing", "Insurance Verification", "Financial Counseling", "Cost Accounting"]),
        new("Information Technology", ["EHR Systems", "Infrastructure", "Cybersecurity", "Telehealth", "Help Desk"])
    ]);

    internal static readonly OrgStructure Startup = new(OrgStructureModel.Startup,
    [
        new("Product", ["Product Management", "Design", "Research"]),
        new("Engineering", ["Backend", "Frontend", "Infrastructure"]),
        new("Growth", ["Marketing", "Sales", "Partnerships"]),
        new("Operations", ["Finance", "Legal", "Customer Support"]),
        new("People", ["Recruiting", "Culture", "Benefits"])
    ]);

    internal static readonly OrgStructure[] All = [Traditional, Spotify, Modern, Government, SchoolDistrict, Healthcare, Startup];

    internal static OrgStructure GetStructure(OrgStructureModel model) => model switch
    {
        OrgStructureModel.Traditional => Traditional,
        OrgStructureModel.Spotify => Spotify,
        OrgStructureModel.Modern => Modern,
        OrgStructureModel.Government => Government,
        OrgStructureModel.SchoolDistrict => SchoolDistrict,
        OrgStructureModel.Healthcare => Healthcare,
        OrgStructureModel.Startup => Startup,
        _ => throw new ArgumentOutOfRangeException(nameof(model), model, $"Unknown org structure model: {model}")
    };
}
