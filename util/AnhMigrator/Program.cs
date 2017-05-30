using Bit.Core.Models.Table;
using Dapper;
using Microsoft.Azure.NotificationHubs;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Newtonsoft.Json;

namespace Bit.AnhMigrator
{
    class Program
    {
        private const string SqlConnectionString = "";
        private const string AnhConnectionString = "";
        private const string AnhHubName = "";

        private static DateTime _startDate = DateTime.UtcNow.AddYears(-10);
        private static string _viewId = "";

        private static NotificationHubClient _client;

        static void Main(string[] args)
        {
            _client = NotificationHubClient.CreateClientFromConnectionString(
                AnhConnectionString,
                AnhHubName);

            //RegisterAsync(args).Wait();
            //ViewAsync(args).Wait();
            Console.Read();
        }

        private static async Task RegisterAsync(string[] args)
        {
            IDictionary<Guid, List<OrganizationUser>> orgUsersDict;
            using(var connection = new SqlConnection(SqlConnectionString))
            {
                var results = await connection.QueryAsync<OrganizationUser>(
                    "SELECT * FROM [dbo].[OrganizationUser] WHERE [Status] = 2 AND [UserId] IS NOT NULL",
                    commandType: CommandType.Text);

                orgUsersDict = results
                    .GroupBy(ou => ou.UserId.Value)
                    .ToDictionary(ou => ou.Key, ou => ou.ToList());
            }

            IEnumerable<Device> devices;
            using(var connection = new SqlConnection(SqlConnectionString))
            {
                devices = await connection.QueryAsync<Device>(
                    "SELECT * FROM [dbo].[Device] WHERE [PushToken] IS NOT NULL AND [RevisionDate] > @StartDate",
                    new { StartDate = _startDate },
                    commandType: CommandType.Text);
            }

            var i = 0;
            foreach(var device in devices)
            {
                i++;

                var installation = new Installation
                {
                    InstallationId = device.Id.ToString(),
                    PushChannel = device.PushToken,
                    Templates = new Dictionary<string, InstallationTemplate>()
                };

                installation.Tags = new List<string>
                {
                    $"userId:{device.UserId}"
                };

                if(!string.IsNullOrWhiteSpace(device.Identifier))
                {
                    installation.Tags.Add("deviceIdentifier:" + device.Identifier);
                }

                if(orgUsersDict.ContainsKey(device.UserId))
                {
                    foreach(var orgUser in orgUsersDict[device.UserId])
                    {
                        installation.Tags.Add("organizationId:" + orgUser.OrganizationId);
                    }
                }

                string payloadTemplate = null, messageTemplate = null, badgeMessageTemplate = null;
                switch(device.Type)
                {
                    case DeviceType.Android:
                        payloadTemplate = "{\"data\":{\"type\":\"#(type)\",\"payload\":\"$(payload)\"}}";
                        messageTemplate = "{\"data\":{\"type\":\"#(type)\"}," +
                            "\"notification\":{\"title\":\"$(title)\",\"body\":\"$(message)\"}}";

                        installation.Platform = NotificationPlatform.Gcm;
                        break;
                    case DeviceType.iOS:
                        payloadTemplate = "{\"data\":{\"type\":\"#(type)\",\"payload\":\"$(payload)\"}," +
                            "\"aps\":{\"alert\":null,\"badge\":null,\"content-available\":1}}";
                        messageTemplate = "{\"data\":{\"type\":\"#(type)\"}," +
                            "\"aps\":{\"alert\":\"$(message)\",\"badge\":null,\"content-available\":1}}";
                        badgeMessageTemplate = "{\"data\":{\"type\":\"#(type)\"}," +
                            "\"aps\":{\"alert\":\"$(message)\",\"badge\":\"#(badge)\",\"content-available\":1}}";

                        installation.Platform = NotificationPlatform.Apns;
                        break;
                    case DeviceType.AndroidAmazon:
                        payloadTemplate = "{\"data\":{\"type\":\"#(type)\",\"payload\":\"$(payload)\"}}";
                        messageTemplate = "{\"data\":{\"type\":\"#(type)\",\"message\":\"$(message)\"}}";

                        installation.Platform = NotificationPlatform.Adm;
                        break;
                    default:
                        break;
                }

                BuildInstallationTemplate(installation, "payload", payloadTemplate, device.UserId, device.Identifier);
                BuildInstallationTemplate(installation, "message", messageTemplate, device.UserId, device.Identifier);
                BuildInstallationTemplate(installation, "badgeMessage", badgeMessageTemplate ?? messageTemplate, device.UserId,
                    device.Identifier);

                await _client.CreateOrUpdateInstallationAsync(installation);

                Console.WriteLine("Added install #" + i + " (" + installation.InstallationId + ")");
            }
        }

        private static void BuildInstallationTemplate(Installation installation, string templateId, string templateBody,
           Guid userId, string deviceIdentifier)
        {
            if(templateBody == null)
            {
                return;
            }

            var fullTemplateId = $"template:{templateId}";

            var template = new InstallationTemplate
            {
                Body = templateBody,
                Tags = new List<string>
                {
                    fullTemplateId,
                    $"{fullTemplateId}_userId:{userId}"
                }
            };

            if(!string.IsNullOrWhiteSpace(deviceIdentifier))
            {
                template.Tags.Add($"{fullTemplateId}_deviceIdentifier:{deviceIdentifier}");
            }

            installation.Templates.Add(fullTemplateId, template);
        }

        private static async Task ViewAsync(string[] args)
        {
            var install = await _client.GetInstallationAsync(_viewId);
            var json = JsonConvert.SerializeObject(install, Formatting.Indented);
            Console.WriteLine(json);
        }
    }
}
