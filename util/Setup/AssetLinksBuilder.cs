using System.IO;

namespace Bit.Setup
{
    public class AssetLinksBuilder
    {
        private readonly Context _context;

        public AssetLinksBuilder(Context context)
        {
            _context = context;
        }

        public void Build()
        {
            var model = new TemplateModel
            {
                Url = _context.Config.Url
            };

            Helpers.WriteLine(_context, "Building Asset Links For Fido2.");
            Directory.CreateDirectory("/bitwarden/web/");
            var template = Helpers.ReadTemplate("AssetLinks");
            using (var sw = File.CreateText("/bitwarden/web/assetlinks.json"))
            {
                sw.Write(template(model));
            }
        }

        public class TemplateModel
        {
            public string Url { get; set; }
        }
    }
}
