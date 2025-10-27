#nullable enable
using System.Collections.Concurrent;
using System.Reflection;
using HandlebarsDotNet;

namespace Bit.Core.Platform.Mailer;

public class HandlebarMailRenderer : IMailRenderer
{
    /// <summary>
    /// Lazy-initialized Handlebars instance. Thread-safe and ensures initialization occurs only once.
    /// </summary>
    private readonly Lazy<Task<IHandlebars>> _handlebarsTask = new(InitializeHandlebarsAsync, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Helper function that returns the handlebar instance.
    /// </summary>
    private Task<IHandlebars> GetHandlebars() => _handlebarsTask.Value;

    /// <summary>
    /// This dictionary is used to cache compiled templates in a thread-safe manner.
    /// </summary>
    private readonly ConcurrentDictionary<string, HandlebarsTemplate<object, object>> _templateCache = new();

    public async Task<(string html, string txt)> RenderAsync(BaseMailView model)
    {
        var html = await CompileTemplateAsync(model, "html");
        var txt = await CompileTemplateAsync(model, "text");

        return (html, txt);
    }

    private async Task<string> CompileTemplateAsync(BaseMailView model, string type)
    {
        var handlebars = await GetHandlebars();

        var templateName = $"{model.GetType().FullName}.{type}.hbs";

        var template = _templateCache.GetOrAdd(templateName, _ =>
        {
            var assembly = model.GetType().Assembly;
            var source = ReadSourceAsync(assembly, templateName).GetAwaiter().GetResult();
            return handlebars.Compile(source);
        });

        return template(model);
    }

    private static async Task<string> ReadSourceAsync(Assembly assembly, string template)
    {
        if (assembly.GetManifestResourceNames().All(f => f != template))
        {
            throw new FileNotFoundException("Template not found: " + template);
        }

        await using var s = assembly.GetManifestResourceStream(template)!;
        using var sr = new StreamReader(s);
        return await sr.ReadToEndAsync();
    }

    private static async Task<IHandlebars> InitializeHandlebarsAsync()
    {
        var handlebars = Handlebars.Create();

        // TODO: Do we still need layouts with MJML?
        var assembly = typeof(HandlebarMailRenderer).Assembly;
        var layoutSource = await ReadSourceAsync(assembly, "Bit.Core.MailTemplates.Handlebars.Layouts.Full.html.hbs");
        handlebars.RegisterTemplate("FullHtmlLayout", layoutSource);

        return handlebars;
    }
}
