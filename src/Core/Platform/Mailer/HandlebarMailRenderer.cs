#nullable enable
using System.Collections.Concurrent;
using System.Reflection;
using Bit.Core.Settings;
using HandlebarsDotNet;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Platform.Mailer;

public class HandlebarMailRenderer : IMailRenderer
{
    /// <summary>
    /// Lazy-initialized Handlebars instance. Thread-safe and ensures initialization occurs only once.
    /// </summary>
    private readonly Lazy<Task<IHandlebars>> _handlebarsTask;

    /// <summary>
    /// Helper function that returns the handlebar instance.
    /// </summary>
    private Task<IHandlebars> GetHandlebars() => _handlebarsTask.Value;

    /// <summary>
    /// This dictionary is used to cache compiled templates in a thread-safe manner.
    /// </summary>
    private readonly ConcurrentDictionary<string, Lazy<Task<HandlebarsTemplate<object, object>>>> _templateCache = new();

    private readonly ILogger<HandlebarMailRenderer> _logger;
    private readonly GlobalSettings _globalSettings;

    public HandlebarMailRenderer(ILogger<HandlebarMailRenderer> logger, GlobalSettings globalSettings)
    {
        _logger = logger;
        _globalSettings = globalSettings;

        _handlebarsTask = new Lazy<Task<IHandlebars>>(InitializeHandlebarsAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    }

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

    private async Task<string> ReadSourceAsync(Assembly assembly, string template)
    {
        if (assembly.GetManifestResourceNames().All(f => f != template))
        {
            throw new FileNotFoundException("Template not found: " + template);
        }

        var diskSource = await ReadSourceFromDiskAsync(template);
        if (!string.IsNullOrWhiteSpace(diskSource))
        {
            return diskSource;
        }

        await using var s = assembly.GetManifestResourceStream(template)!;
        using var sr = new StreamReader(s);
        return await sr.ReadToEndAsync();
    }

    private async Task<string?> ReadSourceFromDiskAsync(string template)
    {
        if (!_globalSettings.SelfHosted)
        {
            return null;
        }

        try
        {
            var diskPath = Path.GetFullPath(Path.Combine(_globalSettings.MailTemplateDirectory, template));
            var baseDirectory = Path.GetFullPath(_globalSettings.MailTemplateDirectory);

            // Ensure the resolved path is within the configured directory
            if (!diskPath.StartsWith(baseDirectory + Path.DirectorySeparatorChar) &&
                diskPath != baseDirectory)
            {
                _logger.LogWarning("Template path traversal attempt detected: {Template}", template);
                return null;
            }

            if (File.Exists(diskPath))
            {
                var fileContents = await File.ReadAllTextAsync(diskPath);
                return fileContents;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to read mail template from disk: {TemplateName}", template);
        }

        return null;
    }

    private async Task<IHandlebars> InitializeHandlebarsAsync()
    {
        var handlebars = Handlebars.Create();

        // TODO: Do we still need layouts with MJML?
        var assembly = typeof(HandlebarMailRenderer).Assembly;
        var layoutSource = await ReadSourceAsync(assembly, "Bit.Core.MailTemplates.Handlebars.Layouts.Full.html.hbs");
        handlebars.RegisterTemplate("FullHtmlLayout", layoutSource);

        return handlebars;
    }
}
