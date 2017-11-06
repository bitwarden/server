using System;
using System.IO;

namespace Bit.Setup
{
    public class DockerComposeBuilder
    {
        private const string CoreVersion = "1.13.1";
        private const string WebVersion = "1.19.0";

        public DockerComposeBuilder(string os)
        {
            MssqlDataDockerVolume = os == "mac";
        }

        public bool MssqlDataDockerVolume { get; private set; }
        public int HttpPort { get; private set; } = 80;
        public int HttpsPort { get; private set; } = 443;

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
    container_name: mssql
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
    container_name: web
    restart: always
    volumes:
      - ../web:/etc/bitwarden/web

  attachments:
    image: bitwarden/attachments:{CoreVersion}
    container_name: attachments
    restart: always
    volumes:
      - ../core/attachments:/etc/bitwarden/core/attachments

  api:
    image: bitwarden/api:{CoreVersion}
    container_name: api
    restart: always
    volumes:
      - ../core:/etc/bitwarden/core
    env_file:
      - global.env
      - ../env/global.override.env

  identity:
    image: bitwarden/identity:{CoreVersion}
    container_name: identity
    restart: always
    volumes:
      - ../identity:/etc/bitwarden/identity
      - ../core:/etc/bitwarden/core
    env_file:
      - global.env
      - ../env/global.override.env

  icons:
    image: bitwarden/icons:{CoreVersion}
    container_name: icons
    restart: always

  nginx:
    image: bitwarden/nginx:{CoreVersion}
    container_name: nginx
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
                sw.Write(@"
");
            }
        }
    }
}
