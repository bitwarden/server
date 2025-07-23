#nullable enable

namespace Bit.Seeder.Enums;

/// <summary>
/// Specifies the target environment for data seeding to ensure proper data protection configuration
/// </summary>
public enum SeederEnvironment
{
    /// <summary>
    /// Auto-detect from GlobalSettings and environment variables
    /// </summary>
    Auto = 0,
    
    /// <summary>
    /// Local development - default ASP.NET Core data protection
    /// </summary>
    Development = 1,
    
    /// <summary>
    /// Self-hosted production - directory-based persistence
    /// </summary>
    SelfHosted = 2,
    
    /// <summary>
    /// Bitwarden cloud - Azure blob storage with certificate protection
    /// </summary>
    Cloud = 3,
    
    /// <summary>
    /// Kubernetes ephemeral environments - shared volume at standard path
    /// </summary>
    Ephemeral = 4
}