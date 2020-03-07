using System;
using System.IO;

namespace Bit.Setup
{
    public class DockerComposeBuilder
    {
        private readonly Context _context;

        public DockerComposeBuilder(Context context)
        {
            _context = context;
        }

        public void BuildForInstaller()
        {
            _context.Config.DatabaseDockerVolume = _context.HostOS == "mac";
            Build();
        }

        public void BuildForUpdater()
        {
            Build();
        }

        private void Build()
        {
            Directory.CreateDirectory("/bitwarden/docker/");
            Helpers.WriteLine(_context, "Building docker-compose.yml.");
            if(!_context.Config.GenerateComposeConfig)
            {
                Helpers.WriteLine(_context, "...skipped");
                return;
            }

            var template = Helpers.ReadTemplate("DockerCompose");
            var model = new TemplateModel(_context);
            using(var sw = File.CreateText("/bitwarden/docker/docker-compose.yml"))
            {
                sw.Write(template(model));
            }
        }

        public class TemplateModel
        {
            public TemplateModel(Context context)
            {
                if(!string.IsNullOrWhiteSpace(context.Config.ComposeVersion))
                {
                    ComposeVersion = context.Config.ComposeVersion;
                }
                MssqlDataDockerVolume = context.Config.DatabaseDockerVolume;
                HttpPort = context.Config.HttpPort;
                HttpsPort = context.Config.HttpsPort;
                if(!string.IsNullOrWhiteSpace(context.CoreVersion))
                {
                    CoreVersion = context.CoreVersion;
                }
                if(!string.IsNullOrWhiteSpace(context.WebVersion))
                {
                    WebVersion = context.WebVersion;
                }
            }

            public string ComposeVersion { get; set; } = "3";
            public bool MssqlDataDockerVolume { get; set; }
            public string HttpPort { get; set; }
            public string HttpsPort { get; set; }
            public bool HasPort => !string.IsNullOrWhiteSpace(HttpPort) || !string.IsNullOrWhiteSpace(HttpsPort);
            public string CoreVersion { get; set; } = "latest";
            public string WebVersion { get; set; } = "latest";
        }
    }
}
