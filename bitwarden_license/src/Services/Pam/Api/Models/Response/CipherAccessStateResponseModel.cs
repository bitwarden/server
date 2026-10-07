using Bit.HttpExtensions;
using Bit.Services.Pam.Models;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>The caller's access state for one cipher, read as a single snapshot.</summary>
public class CipherAccessStateResponseModel : ResponseModel
{
    public CipherAccessStateResponseModel()
        : base("cipherAccessState")
    {
    }

    public CipherAccessStateResponseModel(CipherAccessState state)
        : base("cipherAccessState")
    {
        ArgumentNullException.ThrowIfNull(state);

        CipherId = state.CipherId;
        // Derived against the snapshot's clock, the same instant that filtered the reads.
        ActiveLease = state.ActiveLease is null ? null : new AccessLeaseResponseModel(state.ActiveLease, state.AsOf);
        PendingRequest = state.PendingRequest is null ? null : new AccessRequestDetailsResponseModel(state.PendingRequest);
        ApprovedRequest = state.ApprovedRequest is null ? null : new AccessRequestDetailsResponseModel(state.ApprovedRequest);
        ExtensionsAllowed = state.ExtensionsAllowed;
        MaxExtensionDurationSeconds = state.MaxExtensionDurationSeconds;
    }

    public Guid CipherId { get; set; }

    public AccessLeaseResponseModel? ActiveLease { get; set; }
    public AccessRequestDetailsResponseModel? PendingRequest { get; set; }

    /// <summary>
    /// An approved request awaiting the caller's activation, with a window that can still produce access.
    /// </summary>
    public AccessRequestDetailsResponseModel? ApprovedRequest { get; set; }

    /// <summary>
    /// Whether the active lease can still be extended (the rule opts in and it has not been extended yet).
    /// </summary>
    public bool ExtensionsAllowed { get; set; }

    /// <summary>
    /// The longest single extension the active lease's rule allows; null without an active lease or when the rule does
    /// not allow extensions.
    /// </summary>
    public int? MaxExtensionDurationSeconds { get; set; }
}
