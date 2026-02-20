using System.Globalization;
using Bogus;

namespace Bit.Seeder.Data.Generators;

internal sealed class SecureNoteDataGenerator(int seed)
{
    private readonly int _seed = seed;

    private static readonly string[] _noteCategories =
    [
        "API Keys & Secrets",
        "License Keys",
        "Recovery Codes",
        "Network Credentials",
        "Server Information",
        "Documentation",
        "WiFi Passwords",
        "Database Credentials",
        "Cloud Console Access",
        "Meeting Room Codes",
        "Vendor Portal",
        "Building Access",
        "Expense System",
        "Coffee Machine",
        "Parking Garage"
    ];

    /// <summary>
    /// Generates a deterministic secure note based on index for reproducible test data.
    /// </summary>
    /// <returns>Tuple of (name, notes) for the secure note cipher.</returns>
    internal (string name, string notes) GenerateByIndex(int index)
    {
        var category = _noteCategories[index % _noteCategories.Length];
        var seededFaker = new Faker { Random = new Randomizer(_seed + index) };
        return (GenerateNoteName(category, seededFaker), GenerateNoteContent(category, seededFaker));
    }

    private static string GenerateNoteName(string category, Faker faker) => category switch
    {
        "API Keys & Secrets" => $"{faker.Company.CompanyName()} API Key",
        "License Keys" => $"{faker.Commerce.ProductName()} License",
        "Recovery Codes" => $"{faker.Internet.DomainName()} Recovery Codes",
        "Network Credentials" => $"{faker.Company.CompanyName()} VPN",
        "Server Information" => $"{faker.Hacker.Noun()}-{faker.Random.Int(1, 99)} Server Info",
        "Documentation" => $"{faker.Commerce.Department()} Docs",
        "WiFi Passwords" => $"{faker.PickRandom("Office", "Guest", "Executive", "Lab", "Warehouse")} WiFi - Floor {faker.Random.Int(1, 12)}",
        "Database Credentials" => $"{faker.PickRandom("Production", "Staging", "Analytics", "Reporting")} {faker.PickRandom("MySQL", "PostgreSQL", "MongoDB", "Redis")}",
        "Cloud Console Access" => $"{faker.PickRandom("AWS", "Azure", "GCP", "DigitalOcean")} - {faker.Company.CompanyName()}",
        "Meeting Room Codes" => $"{faker.Address.City()} Conference Room",
        "Vendor Portal" => $"{faker.Company.CompanyName()} Vendor Portal",
        "Building Access" => $"{faker.Address.StreetName()} Office Access",
        "Expense System" => $"{faker.PickRandom("Concur", "Expensify", "SAP", "Corporate Card")} Access",
        "Coffee Machine" => $"{faker.PickRandom("Break Room", "Executive Lounge", "Cafeteria", "Kitchen")} Coffee Machine",
        "Parking Garage" => $"{faker.Address.StreetName()} Parking",
        _ => faker.Lorem.Sentence(3)
    };

    private static string GenerateNoteContent(string category, Faker faker) => category switch
    {
        "API Keys & Secrets" => $"""
            API Key: sk_test_FAKE_{faker.Random.AlphaNumeric(32)}
            Created: {faker.Date.Past():yyyy-MM-dd}
            Environment: {faker.PickRandom("production", "staging", "development")}
            """,

        "License Keys" => $"""
            License: {faker.Random.AlphaNumeric(5).ToUpper(CultureInfo.InvariantCulture)}-{faker.Random.AlphaNumeric(5).ToUpper(CultureInfo.InvariantCulture)}-{faker.Random.AlphaNumeric(5).ToUpper(CultureInfo.InvariantCulture)}
            Expires: {faker.Date.Future():yyyy-MM-dd}
            Seats: {faker.Random.Int(1, 100)}
            """,

        "Recovery Codes" => string.Join("\n",
            Enumerable.Range(1, 10).Select(i => $"{i}. {faker.Random.AlphaNumeric(8).ToLower(CultureInfo.InvariantCulture)}")),

        "Network Credentials" => $"""
            Host: vpn.{faker.Internet.DomainName()}
            Port: {faker.PickRandom(443, 1194, 500)}
            Protocol: {faker.PickRandom("OpenVPN", "IKEv2", "WireGuard")}
            """,

        "Server Information" => $"""
            Host: {faker.Internet.Ip()}
            SSH Port: 22
            OS: {faker.PickRandom("Ubuntu 22.04", "Debian 12", "CentOS 9")}
            """,

        "Documentation" => faker.Lorem.Paragraphs(2),

        "WiFi Passwords" => $"""
            Network: {faker.Company.CompanyName()}-{faker.PickRandom("Corp", "Guest", "IoT", "Secure")}
            Password: {faker.Internet.Password(12)}
            Security: {faker.PickRandom("WPA2-Enterprise", "WPA3", "WPA2-PSK")}
            Note: {faker.PickRandom("Rotates quarterly", "Ask IT for guest access", "Do not share externally")}
            """,

        "Database Credentials" => $"""
            Host: {faker.Hacker.Noun()}-db-{faker.Random.Int(1, 9)}.{faker.Internet.DomainName()}
            Port: {faker.PickRandom(3306, 5432, 27017, 6379)}
            Database: {faker.Hacker.Noun()}_{faker.PickRandom("prod", "staging", "analytics")}
            Username: svc_{faker.Hacker.Noun()}_{faker.Random.Int(100, 999)}
            Password: {faker.Internet.Password(24)}
            """,

        "Cloud Console Access" => $"""
            Console: {faker.PickRandom("https://console.aws.amazon.com", "https://portal.azure.com", "https://console.cloud.google.com")}
            Account ID: {faker.Random.Int(100000000, 999999999)}
            IAM User: {faker.Internet.UserName()}
            MFA Device: {faker.PickRandom("Yubikey", "Google Authenticator", "Authy", "1Password")}
            Role: {faker.PickRandom("AdministratorAccess", "PowerUserAccess", "ReadOnlyAccess", "BillingAccess")}
            """,

        "Meeting Room Codes" => $"""
            Room: {faker.Address.City()} {faker.PickRandom("A", "B", "C", "")}{faker.Random.Int(100, 450)}
            Capacity: {faker.PickRandom(4, 6, 8, 12, 20)} people
            PIN: {faker.Random.Int(1000, 9999)}
            Zoom Room ID: {faker.Random.Int(100, 999)}-{faker.Random.Int(100, 999)}-{faker.Random.Int(1000, 9999)}
            AV Contact: x{faker.Random.Int(1000, 9999)}
            """,

        "Vendor Portal" => $"""
            URL: https://vendor.{faker.Internet.DomainName()}/portal
            Company ID: {faker.Random.AlphaNumeric(8).ToUpper(CultureInfo.InvariantCulture)}
            Username: {faker.Internet.Email()}
            Password: {faker.Internet.Password(16)}
            Support: {faker.Phone.PhoneNumber("1-800-###-####")}
            Account Rep: {faker.Name.FullName()}
            """,

        "Building Access" => $"""
            Address: {faker.Address.StreetAddress()}, {faker.Address.City()}
            Alarm Code: {faker.Random.Int(1000, 9999)}#
            Disarm Window: {faker.Random.Int(30, 90)} seconds
            Emergency Contact: {faker.Phone.PhoneNumber()}
            After Hours: {faker.PickRandom("Call security at x5555", "Use side entrance", "Badge required 24/7")}
            """,

        "Expense System" => $"""
            System: {faker.PickRandom("Concur", "Expensify", "SAP Concur", "Certify")}
            Employee ID: {faker.Random.AlphaNumeric(6).ToUpper(CultureInfo.InvariantCulture)}
            Approval Limit: ${faker.Random.Int(500, 5000):N0}
            Corporate Card: **** **** **** {faker.Random.Int(1000, 9999)}
            PIN: {faker.Random.Int(1000, 9999)}
            Billing Code: {faker.Random.Int(10000, 99999)}-{faker.Random.Int(100, 999)}
            """,

        "Coffee Machine" => $"""
            Machine: {faker.PickRandom("Jura", "Breville", "De'Longhi", "Nespresso")} {faker.Commerce.ProductAdjective()}
            Premium Code: {faker.Random.Int(1000, 9999)}
            Maintenance: {faker.PickRandom("Facilities", "Office Manager", "Self-service")}
            Bean Refill: {faker.PickRandom("Tuesdays", "Wednesdays", "Weekly", "As needed")}
            Secret Menu: Double-tap for extra shot
            """,

        "Parking Garage" => $"""
            Location: {faker.Address.StreetAddress()}
            Gate Code: #{faker.Random.Int(1000, 9999)}
            Assigned Spot: {faker.PickRandom("A", "B", "C", "P")}{faker.Random.Int(1, 4)}-{faker.Random.Int(100, 450)}
            Monthly Pass: {faker.Random.AlphaNumeric(10).ToUpper(CultureInfo.InvariantCulture)}
            Validation: {faker.PickRandom("Get ticket stamped at reception", "Use company app", "Auto-validated by badge")}
            Emergency Exit: {faker.PickRandom("Stairwell B", "North ramp", "Elevator to lobby")}
            """,

        _ => faker.Lorem.Paragraph()
    };
}
