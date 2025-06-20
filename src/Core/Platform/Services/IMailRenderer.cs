#nullable enable
namespace Bit.Core.Platform.Services;

public interface IMailRenderer
{
    Task<(string html, string? txt)> RenderAsync(BaseMailView model);
}
