namespace Bit.Core.Services;

/// <summary>
/// Service for managing Play identifiers in automated testing infrastructure.
/// A "Play" is a test session that groups entities created during testing to enable cleanup.
/// The PlayId flows from client request (x-play-id header) through PlayIdMiddleware to this service,
/// which repositories query to create PlayData tracking records via IPlayDataService. The SeederAPI uses these records
/// to bulk delete all entities associated with a PlayId. Only active in Development environments.
/// </summary>
public interface IPlayIdService
{
    /// <summary>
    /// Gets or sets the current Play identifier from the x-play-id request header.
    /// </summary>
    string? PlayId { get; set; }

    /// <summary>
    /// Checks whether the current request is part of an active Play session.
    /// </summary>
    /// <param name="playId">The Play identifier if active, otherwise empty string.</param>
    /// <returns>True if in a Play session (has PlayId and in Development environment), otherwise false.</returns>
    bool InPlay(out string playId);
}
