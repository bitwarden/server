#nullable enable

namespace Bit.Identity.Models.Request.Accounts;

/// <summary>
/// The providers, organizations and users to delete outright, by their identifiers. Any list may be empty or absent.
/// </summary>
public class StaleCopyDeletionRequestModel
{
    /// <summary>The providers to delete, before anything else.</summary>
    public List<Guid>? ProviderIds { get; set; }

    /// <summary>The organizations to delete, with everything they hold, after the providers.</summary>
    public List<Guid>? OrganizationIds { get; set; }

    /// <summary>The users to delete, with everything they hold, last.</summary>
    public List<Guid>? UserIds { get; set; }
}
