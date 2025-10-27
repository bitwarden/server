#nullable enable
namespace Bit.Core.Platform.Mailer;

public interface IMailRenderer
{
    Task<(string html, string? txt)> RenderAsync(BaseMailView model);
}
