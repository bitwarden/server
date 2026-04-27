using Microsoft.Extensions.Hosting;

namespace Bit.Core.Services;

public class PlayIdService(IHostEnvironment hostEnvironment) : IPlayIdService
{
    public string? PlayId { get; set; }
    public bool InPlay(out string playId)
    {
        playId = PlayId ?? string.Empty;
        var hasPlayId = !string.IsNullOrEmpty(playId);
        var isNotProd = !hostEnvironment.IsProduction();
        return hasPlayId && isNotProd;
    }
}
