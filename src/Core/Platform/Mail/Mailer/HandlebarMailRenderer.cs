#nullable enable
using System.Collections.Concurrent;
using System.Reflection;
using HandlebarsDotNet;

namespace Bit.Core.Platform.Mail.Mailer;
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
    private readonly ConcurrentDictionary<string, Lazy<Task<HandlebarsTemplate<object, object>>>> _templateCache = new();

    public async Task<(string html, string txt)> RenderAsync(BaseMailView model)
    {
        var html = await CompileTemplateAsync(model, "html");
        var txt = await CompileTemplateAsync(model, "text");

        return (html, txt);
    }

    private async Task<string> CompileTemplateAsync(BaseMailView model, string type)
    {
        var templateName = $"{model.GetType().FullName}.{type}.hbs";
        var assembly = model.GetType().Assembly;

        // GetOrAdd is atomic - only one Lazy will be stored per templateName.
        // The Lazy with ExecutionAndPublication ensures the compilation happens exactly once.
        var lazyTemplate = _templateCache.GetOrAdd(
            templateName,
            key => new Lazy<Task<HandlebarsTemplate<object, object>>>(
                () => CompileTemplateInternalAsync(assembly, key),
                LazyThreadSafetyMode.ExecutionAndPublication));

        var template = await lazyTemplate.Value;
        return template(model);
    }

    private async Task<HandlebarsTemplate<object, object>> CompileTemplateInternalAsync(Assembly assembly, string templateName)
    {
        var source = await ReadSourceAsync(assembly, templateName);
        var handlebars = await GetHandlebars();
        return handlebars.Compile(source);
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
