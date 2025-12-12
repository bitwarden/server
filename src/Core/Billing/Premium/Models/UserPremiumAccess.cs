namespace Bit.Core.Billing.Premium.Models;

/// <summary>
/// Represents user premium access status from personal subscriptions and organization memberships.
/// </summary>
public class UserPremiumAccess
{
    /// <summary>
    /// The unique identifier for the user.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Indicates whether the user has a personal premium subscription.
    /// This does NOT include premium access from organizations.
    /// </summary>
    public bool PersonalPremium { get; set; }

    /// <summary>
    /// Indicates whether the user has premium access through any organization membership.
    /// This is true if the user is a member of at least one enabled organization that grants premium access to users.
    /// </summary>
    public bool OrganizationPremium { get; set; }

    /// <summary>
    /// Indicates whether the user has premium access from any source (personal subscription or organization).
    /// </summary>
    public bool HasPremiumAccess => PersonalPremium || OrganizationPremium;
}
