namespace Bit.Identity.Models;

#nullable enable

/// <summary>
/// Subset of user details that are returned from the token endpoint.
/// </summary>
public class TokenUserInfoModel
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string Email { get; set; } = null!;
    public bool EmailVerified { get; set; }
    public DateTime CreationDate { get; set; }
    public bool Premium { get; set; }
}
