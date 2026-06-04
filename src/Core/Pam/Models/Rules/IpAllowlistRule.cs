namespace Bit.Core.Pam.Models.Rules;

/// <summary>
/// Auto-approves a lease when the requester's IP matches a listed CIDR; otherwise denies.
/// </summary>
public sealed class IpAllowlistRule : Rule
{
    public IReadOnlyList<string> Cidrs { get; init; } = [];
}
