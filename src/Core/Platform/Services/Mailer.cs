using System.Reflection;
using HandlebarsDotNet;

namespace Bit.Core.Platform.Services;

#nullable enable

public class Mailer : IMailer
{
    public IHandlebars H { get; set; }

    public Mailer()
    {
        H = Handlebars.Create();
    }


    public async Task SendEmail(BaseMailModel2 message, string recipient)
    {
        var htmlTemplate = await ReadTemplateAsync(message, "html");
        var textTemplate = await ReadTemplateAsync(message, "txt");


        var assembly = typeof(Mailer).Assembly;
        var basicHtmlLayoutSource = await ReadSourceAsync(assembly, "Bit.Core.MailTemplates.Handlebars.Layouts.Full.html.hbs");
        H.RegisterTemplate("FullHtmlLayout", basicHtmlLayoutSource);

        var t = H.Compile(htmlTemplate);
        var tmp = t(message);

        Console.WriteLine(tmp);
    }

    public void SendEmails(BaseMailModel2 message, string[] recipients) => throw new NotImplementedException();


    private static async Task<string?> ReadTemplateAsync(BaseMailModel2 message, string suffix)
    {
        var assembly = message.GetType().Assembly;
        var qualifiedName = message.GetType().FullName;
        var template = $"{qualifiedName}.{suffix}.hbs";

        return await ReadSourceAsync(assembly, template);
    }

    private static async Task<string?> ReadSourceAsync(Assembly assembly, string template)
    {
        if (assembly.GetManifestResourceNames().All(f => f != template))
        {
            return null;
        }

        await using var s = assembly.GetManifestResourceStream(template)!;
        using var sr = new StreamReader(s);
        return await sr.ReadToEndAsync();
    }
}
