using System;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Bit.Function.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;

namespace Bit.Function
{
    public static class BlockIp
    {
        [FunctionName("BlockIp")]
        public static void Run(
            [QueueTrigger("blockip", Connection = "")]string myQueueItem,
            out string outputQueueItem,
            TraceWriter log)
        {
            outputQueueItem = BlockIpAsync(myQueueItem).GetAwaiter().GetResult();
            log.Info($"C# Queue trigger function processed: {myQueueItem}, outputted: {outputQueueItem}");
        }

        private static async Task<string> BlockIpAsync(string ipAddress)
        {
            var ipWhitelist = ConfigurationManager.AppSettings["WhitelistedIps"];
            if(ipWhitelist != null && ipWhitelist.Split(',').Contains(ipAddress))
            {
                return null;
            }

            var xAuthEmail = ConfigurationManager.AppSettings["X-Auth-Email"];
            var xAuthKey = ConfigurationManager.AppSettings["X-Auth-Key"];
            var zoneId = ConfigurationManager.AppSettings["ZoneId"];

            using(var client = new HttpClient())
            {
                client.BaseAddress = new Uri("https://api.cloudflare.com");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Add("X-Auth-Email", xAuthEmail);
                client.DefaultRequestHeaders.Add("X-Auth-Key", xAuthKey);

                var response = await client.PostAsJsonAsync(
                    $"/client/v4/zones/{zoneId}/firewall/access_rules/rules",
                    new
                    {
                        mode = "block",
                        configuration = new
                        {
                            target = "ip",
                            value = ipAddress
                        },
                        notes = $"Rate limit abuse on {DateTime.UtcNow.ToString()}."
                    });

                var responseString = await response.Content.ReadAsStringAsync();
                var responseJson = JsonConvert.DeserializeObject<AccessRuleResponse>(responseString);

                if(!responseJson.Success)
                {
                    return null;
                }

                // Uncomment whenever we can delay the returned message. Functions do not support that at this time.
                return null; //responseJson.Result?.Id;
            }
        }
    }
}
