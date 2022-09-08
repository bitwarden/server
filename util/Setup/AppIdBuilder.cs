namespace Bit.Setup;

public class AppIdBuilder
{
    private readonly Context _context;

    public AppIdBuilder(Context context)
    {
        _context = context;
    }

    public void Build()
    {
        var model = new TemplateModel
        {
            Url = _context.Config.Url
        };

        // Needed for backwards compatability with migrated U2F tokens.
        Helpers.WriteLine(_context, "Building FIDO U2F app id.");
        Directory.CreateDirectory("/bitwarden/web/");
        var template = Helpers.ReadTemplate("AppId");
        using (var sw = File.CreateText("/bitwarden/web/app-id.json"))
        {
            sw.Write(template(model));
        }
    }

    public class TemplateModel
    {
        public string Url { get; set; }
    }
}
