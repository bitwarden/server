using Microsoft.Extensions.Hosting;

namespace Bit.Core.Services;

public class PlayIdService(IHostEnvironment hostEnvironment) : IPlayIdService
{
    public string? PlayId { get; set; }
    public bool InPlay(out string playId)
    {
        playId = PlayId ?? string.Empty;
        return !string.IsNullOrEmpty(PlayId) && hostEnvironment.IsDevelopment();
    }
}
