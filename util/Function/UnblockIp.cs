using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Threading.Tasks;
using Bit.Function.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;

namespace Bit.Function
{
    public static class UnblockIp
    {
        [FunctionName("UnblockIp")]
        public static void Run(
            [QueueTrigger("unblockip", Connection = "")]string myQueueItem,
            TraceWriter log)
        {
            log.Info($"C# Queue trigger function processed: {myQueueItem}");
            UnblockIpAsync(myQueueItem, log).Wait();
        }

        private static async Task UnblockIpAsync(string id, TraceWriter log)
        {
            if(id == null)
            {
                return;
            }

            var zoneId = ConfigurationManager.AppSettings["ZoneId"];
            var xAuthEmail = ConfigurationManager.AppSettings["X-Auth-Email"];
            var xAuthKey = ConfigurationManager.AppSettings["X-Auth-Key"];

            if(id.Contains(".") || id.Contains(":"))
            {
                // IP address messages.
                using(var client = new HttpClient())
                {
                    client.BaseAddress = new Uri("https://api.cloudflare.com");
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Add("X-Auth-Email", xAuthEmail);
                    client.DefaultRequestHeaders.Add("X-Auth-Key", xAuthKey);

                    var response = await client.GetAsync($"/client/v4/zones/{zoneId}/firewall/access_rules/rules?" +
                        $"configuration_target=ip&configuration_value={id}");

                    var responseString = await response.Content.ReadAsStringAsync();
                    var responseJson = JsonConvert.DeserializeObject<ListResult>(responseString);

                    if(!responseJson.Success)
                    {
                        return;
                    }

                    foreach(var rule in responseJson.Result)
                    {
                        if(rule.Configuration?.Value != id)
                        {
                            continue;
                        }

                        log.Info($"Unblock IP {id}, {rule.Id}");
                        await DeleteRuleAsync(zoneId, xAuthEmail, xAuthKey, rule.Id);
                    }
                }
            }
            else
            {
                log.Info($"Unblock Id {id}");
                await DeleteRuleAsync(zoneId, xAuthEmail, xAuthKey, id);
            }
        }

        private static async Task DeleteRuleAsync(string zoneId, string xAuthEmail, string xAuthKey, string id)
        {
            var path = $"/client/v4/zones/{zoneId}/firewall/access_rules/rules/{id}";
            using(var client = new HttpClient())
            {
                client.BaseAddress = new Uri("https://api.cloudflare.com");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Add("X-Auth-Email", xAuthEmail);
                client.DefaultRequestHeaders.Add("X-Auth-Key", xAuthKey);
                await client.DeleteAsync(path);
            }
        }
    }
}
