#nullable enable
namespace Bit.Core.Platform.Mail.Mailer;

public interface IMailRenderer
{
    Task<(string html, string txt)> RenderAsync(BaseMailView model);
}
