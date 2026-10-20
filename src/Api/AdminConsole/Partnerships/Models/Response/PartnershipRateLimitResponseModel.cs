using Bit.Core.Settings;

namespace Bit.Api.AdminConsole.Partnerships.Models.Response;

/// <summary>
/// The partnership's request limit: at most <c>requests</c> per fixed window of <c>windowSeconds</c>.
/// </summary>
public class PartnershipRateLimitResponseModel(GlobalSettings.PartnershipSettings partnershipSettings)
{
    /// <summary>
    /// The most requests allowed in one window.
    /// </summary>
    public int Requests { get; } = partnershipSettings.RateLimitRequests;
    /// <summary>
    /// The length of the window, in seconds.
    /// </summary>
    public int WindowSeconds { get; } = (int)partnershipSettings.RateLimitWindow.TotalSeconds;
}
