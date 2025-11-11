using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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

public class PlayIdSingletonService(IHttpContextAccessor httpContextAccessor, IHostEnvironment hostEnvironment) : IPlayIdService
{
    private PlayIdService Current
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                throw new InvalidOperationException("HttpContext is not available");
            }
            return httpContext.RequestServices.GetRequiredService<PlayIdService>();
        }
    }

    public string? PlayId
    {
        get => Current.PlayId;
        set => Current.PlayId = value;
    }

    public bool InPlay(out string playId)
    {
        if (hostEnvironment.IsDevelopment())
        {
            return Current.InPlay(out playId);
        }
        else
        {
            playId = string.Empty;
            return false;
        }
    }
}
