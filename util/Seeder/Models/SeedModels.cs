namespace Bit.Seeder.Models;

internal record SeedFile
{
    public required List<SeedVaultItem> Items { get; init; }
}

internal record SeedVaultItem
{
    public required string Type { get; init; }
    public required string Name { get; init; }
    public string? Notes { get; init; }
    public SeedLogin? Login { get; init; }
    public SeedCard? Card { get; init; }
    public SeedIdentity? Identity { get; init; }
    public List<SeedField>? Fields { get; init; }
}

internal record SeedLogin
{
    public string? Username { get; init; }
    public string? Password { get; init; }
    public List<SeedLoginUri>? Uris { get; init; }
    public string? Totp { get; init; }
}

internal record SeedLoginUri
{
    public required string Uri { get; init; }
    public string Match { get; init; } = "domain";
}

internal record SeedCard
{
    public string? CardholderName { get; init; }
    public string? Brand { get; init; }
    public string? Number { get; init; }
    public string? ExpMonth { get; init; }
    public string? ExpYear { get; init; }
    public string? Code { get; init; }
}

internal record SeedIdentity
{
    public string? FirstName { get; init; }
    public string? MiddleName { get; init; }
    public string? LastName { get; init; }
    public string? Address1 { get; init; }
    public string? Address2 { get; init; }
    public string? Address3 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
    public string? Company { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Ssn { get; init; }
    public string? Username { get; init; }
    public string? PassportNumber { get; init; }
    public string? LicenseNumber { get; init; }
}

internal record SeedField
{
    public string? Name { get; init; }
    public string? Value { get; init; }
    public string Type { get; init; } = "text";
}

internal record SeedOrganization
{
    public required string Name { get; init; }
    public required string Domain { get; init; }
    public int Seats { get; init; } = 10;
}

internal record SeedRoster
{
    public required List<SeedRosterUser> Users { get; init; }
    public List<SeedRosterGroup>? Groups { get; init; }
    public List<SeedRosterCollection>? Collections { get; init; }
}

internal record SeedRosterUser
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? Title { get; init; }
    public string Role { get; init; } = "user";
    public string? Branch { get; init; }
    public string? Department { get; init; }
}

internal record SeedRosterGroup
{
    public required string Name { get; init; }
    public required List<string> Members { get; init; }
}

internal record SeedRosterCollection
{
    public required string Name { get; init; }
    public List<SeedRosterCollectionGroup>? Groups { get; init; }
    public List<SeedRosterCollectionUser>? Users { get; init; }
}

internal record SeedRosterCollectionGroup
{
    public required string Group { get; init; }
    public bool ReadOnly { get; init; }
    public bool HidePasswords { get; init; }
    public bool Manage { get; init; }
}

internal record SeedRosterCollectionUser
{
    public required string User { get; init; }
    public bool ReadOnly { get; init; }
    public bool HidePasswords { get; init; }
    public bool Manage { get; init; }
}
