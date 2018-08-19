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
        public int HttpPort { get; private set; }
        public int HttpsPort { get; private set; }
        public string CoreVersion { get; private set; } = "latest";
        public string WebVersion { get; private set; } = "latest";

        public void BuildForInstaller(int httpPort, int httpsPort)
        {
            if(httpPort != default(int))
            {
                HttpPort = httpPort;
            }

            if(httpsPort != default(int))
            {
                HttpsPort = httpsPort;
            }

            Build();
        }

        public void BuildForUpdater()
        {
            var composeFile = "/bitwarden/docker/docker-compose.yml";
            if(File.Exists(composeFile))
            {
                var fileLines = File.ReadAllLines(composeFile);
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

                    if(paramParts[0] == "# Parameter:MssqlDataDockerVolume" &&
                        bool.TryParse(paramParts[1], out var mssqlDataDockerVolume))
                    {
                        MssqlDataDockerVolume = mssqlDataDockerVolume;
                        continue;
                    }

                    if(paramParts[0] == "# Parameter:HttpPort" && int.TryParse(paramParts[1], out var httpPort))
                    {
                        HttpPort = httpPort;
                        continue;
                    }

                    if(paramParts[0] == "# Parameter:HttpsPort" && int.TryParse(paramParts[1], out var httpsPort))
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
      - ../logs/mssql:/var/opt/mssql/log
      - ../mssql/backups:/etc/bitwarden/mssql/backups
    env_file:
      - mssql.env
      - ../env/uid.env
      - ../env/mssql.override.env

  web:
    image: bitwarden/web:{WebVersion}
    container_name: bitwarden-web
    restart: always
    volumes:
      - ../web:/etc/bitwarden/web
    env_file:
      - ../env/uid.env

  attachments:
    image: bitwarden/attachments:{CoreVersion}
    container_name: bitwarden-attachments
    restart: always
    volumes:
      - ../core/attachments:/etc/bitwarden/core/attachments
    env_file:
      - ../env/uid.env

  api:
    image: bitwarden/api:{CoreVersion}
    container_name: bitwarden-api
    restart: always
    volumes:
      - ../core:/etc/bitwarden/core
      - ../ca-certificates:/etc/bitwarden/ca-certificates
      - ../logs/api:/etc/bitwarden/logs
    env_file:
      - global.env
      - ../env/uid.env
      - ../env/global.override.env

  identity:
    image: bitwarden/identity:{CoreVersion}
    container_name: bitwarden-identity
    restart: always
    volumes:
      - ../identity:/etc/bitwarden/identity
      - ../core:/etc/bitwarden/core
      - ../ca-certificates:/etc/bitwarden/ca-certificates
      - ../logs/identity:/etc/bitwarden/logs
    env_file:
      - global.env
      - ../env/uid.env
      - ../env/global.override.env

  admin:
    image: bitwarden/admin:{CoreVersion}
    container_name: bitwarden-admin
    restart: always
    volumes:
      - ../core:/etc/bitwarden/core
      - ../ca-certificates:/etc/bitwarden/ca-certificates
      - ../logs/admin:/etc/bitwarden/logs
    env_file:
      - global.env
      - ../env/uid.env
      - ../env/global.override.env

  icons:
    image: bitwarden/icons:{CoreVersion}
    container_name: bitwarden-icons
    restart: always
    env_file:
      - ../env/uid.env

  nginx:
    image: bitwarden/nginx:{CoreVersion}
    container_name: bitwarden-nginx
    restart: always
    ports:");

                if(HttpPort != default(int))
                {
                    sw.Write($@"
      - '127.0.0.1:{HttpPort}:8080'");
                }

                if(HttpsPort != default(int))
                {
                    sw.Write($@"
      - '127.0.0.1:{HttpsPort}:8443'");
                }

                sw.Write($@"
    volumes:
      - ../nginx:/etc/bitwarden/nginx
      - ../letsencrypt:/etc/letsencrypt
      - ../ssl:/etc/ssl
      - ../logs/nginx:/var/log/nginx
    env_file:
      - ../env/uid.env");

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
