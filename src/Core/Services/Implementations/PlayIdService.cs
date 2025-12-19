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

public class NeverPlayIdServices : IPlayIdService
{
    public string? PlayId
    {
        get => null;
        set { }
    }

    public bool InPlay(out string playId)
    {
        playId = string.Empty;
        return false;
    }
}

/// <summary>
/// Singleton wrapper service that bridges singleton-scoped service boundaries for PlayId tracking.
/// This allows singleton services to access the scoped PlayIdService via HttpContext.RequestServices.
///
/// Uses IHttpContextAccessor to retrieve the current request's scoped PlayIdService instance, enabling
/// singleton services to participate in Play session tracking without violating DI lifetime rules.
/// Falls back to NeverPlayIdServices when no HttpContext is available (e.g., background jobs).
/// </summary>
public class PlayIdSingletonService(IHttpContextAccessor httpContextAccessor, IHostEnvironment hostEnvironment) : IPlayIdService
{
    private IPlayIdService Current
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return new NeverPlayIdServices();
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
