#nullable enable
using System.Reflection;
using HandlebarsDotNet;

namespace Bit.Core.Platform.Mailer;

public class HandlebarMailRenderer : IMailRenderer
{
    /// <summary>
    /// This field holds the handlebars instance.
    ///
    /// This should never be used directly, rather use the GetHandlebars() method to ensure that it is initialized properly.
    /// </summary>
    private IHandlebars? _handlebars;

    /// <summary>
    /// This task is used to ensure that the handlebars instance is initialized only once.
    /// </summary>
    private Task<IHandlebars>? _initTask;
    /// <summary>
    /// This lock is used to ensure that the handlebars instance is initialized only once,
    /// even if multiple threads call GetHandlebars() at the same time.
    /// </summary>
    private readonly object _initLock = new();

    /// <summary>
    /// This dictionary is used to cache compiled templates.
    /// </summary>
    private readonly Dictionary<string, HandlebarsTemplate<object, object>> _templateCache = new();

    public async Task<(string html, string? txt)> RenderAsync(BaseMailView model)
    {
        var html = await CompileTemplateAsync(model, "html");
        var txt = await CompileTemplateAsync(model, "txt");

        return (html, txt);
    }

    private async Task<string> CompileTemplateAsync(BaseMailView model, string type)
    {
        var handlebars = await GetHandlebars();

        var templateName = $"{model.GetType().FullName}.{type}.hbs";

        if (!_templateCache.TryGetValue(templateName, out var template))
        {
            var assembly = model.GetType().Assembly;
            var source = await ReadSourceAsync(assembly, templateName);
            template = handlebars.Compile(source);
            _templateCache.Add(templateName, template);
        }

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

    /// <summary>
    /// Helper function that returns the handlebar instance, initializing it if necessary.
    ///
    /// Protects against initializing the same instance multiple times in parallel.
    /// </summary>
    private Task<IHandlebars> GetHandlebars()
    {
        if (_handlebars != null)
        {
            return Task.FromResult(_handlebars);
        }

        lock (_initLock)
        {
            _initTask ??= InitializeHandlebarsAsync();
            return _initTask;
        }
    }

    private async Task<IHandlebars> InitializeHandlebarsAsync()
    {
        var handlebars = Handlebars.Create();

        // TODO: Do we still need layouts with MJML?
        var assembly = typeof(HandlebarMailRenderer).Assembly;
        var layoutSource = await ReadSourceAsync(assembly, "Bit.Core.MailTemplates.Handlebars.Layouts.Full.html.hbs");
        handlebars.RegisterTemplate("FullHtmlLayout", layoutSource);

        _handlebars = handlebars;
        return handlebars;
    }
}
