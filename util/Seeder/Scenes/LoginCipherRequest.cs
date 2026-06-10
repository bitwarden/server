using System.ComponentModel.DataAnnotations;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Shared request fields for seeding a login cipher, regardless of owner (user or organization).
/// </summary>
public abstract class LoginCipherRequest
{
    [Required]
    public required Guid UserId { get; set; }
    [Required]
    public required string Name { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Totp { get; set; }
    public string? Uri { get; set; }
    public string? Notes { get; set; }
    public bool Reprompt { get; set; }
    public bool Deleted { get; set; }
    public bool Favorite { get; set; }
    public Guid? FolderId { get; set; }
    public IEnumerable<FieldRequest>? Fields { get; set; }
    public IEnumerable<PasskeyRequest>? Passkeys { get; set; }

    public class FieldRequest
    {
        public required string Name { get; set; }
        public required string Value { get; set; }
        public required int Type { get; set; }
    }

    public class PasskeyRequest
    {
        public required string RpId { get; set; }
        public required string RpName { get; set; }
        public required string UserName { get; set; }
    }
}
