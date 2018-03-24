using System;
using System.IO;

namespace Bit.Setup
{
    public class DockerComposeBuilder
    {
        public DockerComposeBuilder(string os, string webVersion, string coreVersion)
        {
            MssqlDataDockerVolume = os == "mac";

            if(!string.IsNullOrWhiteSpace(webVersion))
            {
                WebVersion = webVersion;
            }
            if(!string.IsNullOrWhiteSpace(coreVersion))
            {
                CoreVersion = coreVersion;
            }
        }

        public bool MssqlDataDockerVolume { get; private set; }
        public int HttpPort { get; private set; } = 80;
        public int HttpsPort { get; private set; } = 443;
        public string CoreVersion { get; private set; } = "latest";
        public string WebVersion { get; private set; } = "latest";

        public void BuildForInstaller(int httpPort, int httpsPort)
        {
            if(httpPort != default(int) && httpsPort != default(int))
            {
                HttpPort = httpPort;
                HttpsPort = httpsPort;
            }

            Build();
        }

        public void BuildForUpdater()
        {
            if(File.Exists("/bitwarden/docker/docker-compose.yml"))
            {
                var fileLines = File.ReadAllLines("/bitwarden/docker/docker-compose.yml");
                foreach(var line in fileLines)
                {
                    if(!line.StartsWith("# Parameter:"))
                    {
                        continue;
                    }

                    var paramParts = line.Split("=");
                    if(paramParts.Length < 2)
                    {
                        continue;
                    }

                    if(paramParts[0] == "# Parameter:HttpPort" && int.TryParse(paramParts[1], out int httpPort))
                    {
                        HttpPort = httpPort;
                        continue;
                    }

                    if(paramParts[0] == "# Parameter:HttpsPort" && int.TryParse(paramParts[1], out int httpsPort))
                    {
                        HttpsPort = httpsPort;
                        continue;
                    }
                }
            }

            Build();
        }

        private void Build()
        {
            Console.WriteLine("Building docker-compose.yml.");
            Directory.CreateDirectory("/bitwarden/docker/");
            using(var sw = File.CreateText("/bitwarden/docker/docker-compose.yml"))
            {
                sw.Write($@"# https://docs.docker.com/compose/compose-file/
# Parameter:MssqlDataDockerVolume={MssqlDataDockerVolume}
# Parameter:HttpPort={HttpPort}
# Parameter:HttpsPort={HttpsPort}
# Parameter:CoreVersion={CoreVersion}
# Parameter:WebVersion={WebVersion}

version: '3'

services:
  mssql:
    image: bitwarden/mssql:{CoreVersion}
    container_name: bitwarden-mssql
    restart: always
    volumes:");

                if(MssqlDataDockerVolume)
                {
                    sw.Write(@"
      - mssql_data:/var/opt/mssql/data");
                }
                else
                {
                    sw.Write(@"
      - ../mssql/data:/var/opt/mssql/data");
                }

                sw.Write($@"
      - ../mssql/backups:/etc/bitwarden/mssql/backups
    env_file:
      - mssql.env
      - ../env/mssql.override.env

  web:
    image: bitwarden/web:{WebVersion}
    container_name: bitwarden-web
    restart: always
    volumes:
      - ../web:/etc/bitwarden/web

  attachments:
    image: bitwarden/attachments:{CoreVersion}
    container_name: bitwarden-attachments
    restart: always
    volumes:
      - ../core/attachments:/etc/bitwarden/core/attachments

  api:
    image: bitwarden/api:{CoreVersion}
    container_name: bitwarden-api
    restart: always
    volumes:
      - ../core:/etc/bitwarden/core
    env_file:
      - global.env
      - ../env/global.override.env

  identity:
    image: bitwarden/identity:{CoreVersion}
    container_name: bitwarden-identity
    restart: always
    volumes:
      - ../identity:/etc/bitwarden/identity
      - ../core:/etc/bitwarden/core
    env_file:
      - global.env
      - ../env/global.override.env

  admin:
    image: bitwarden/admin:{CoreVersion}
    container_name: bitwarden-admin
    restart: always
    volumes:
      - ../core:/etc/bitwarden/core
    env_file:
      - global.env
      - ../env/global.override.env

  icons:
    image: bitwarden/icons:{CoreVersion}
    container_name: bitwarden-icons
    restart: always

  nginx:
    image: bitwarden/nginx:{CoreVersion}
    container_name: bitwarden-nginx
    restart: always
    ports:
      - '{HttpPort}:80'
      - '{HttpsPort}:443'
    volumes:
      - ../nginx:/etc/bitwarden/nginx
      - ../letsencrypt:/etc/letsencrypt
      - ../ssl:/etc/ssl");

                if(MssqlDataDockerVolume)
                {
                    sw.Write(@"
volumes:
  mssql_data:");
                }

                // New line at end of file.
                sw.Write("\n");
            }
        }
    }
}
