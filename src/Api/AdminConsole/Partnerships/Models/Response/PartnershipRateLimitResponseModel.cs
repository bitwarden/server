using Bit.Core.Settings;

namespace Bit.Api.AdminConsole.Partnerships.Models.Response;

/// <summary>
/// The partnership's request limit: at most <see cref="Requests"/> per fixed window of <see cref="WindowSeconds"/>.
/// </summary>
public class PartnershipRateLimitResponseModel(GlobalSettings.PartnershipSettings partnershipSettings)
{
    public int Requests { get; } = partnershipSettings.RateLimitRequests;
    public int WindowSeconds { get; } = (int)partnershipSettings.RateLimitWindow.TotalSeconds;
}
