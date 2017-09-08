using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Dapper;
using Stripe;

namespace Bit.BillingUpdater
{
    public class Program
    {
        public static void Main(string[] args)
        {
            MainAsync().Wait();
        }

        public async static Task MainAsync()
        {
            var connectionString = "";
            var stripeApiKey = "";
            var stripeSubscriptionService = new StripeSubscriptionService(stripeApiKey);

            using(var connection = new SqlConnection(connectionString))
            {
                //Paid orgs

                var orgs = await connection.QueryAsync<Organization>(
                    "SELECT * FROM [Organization] WHERE [Enabled] = 1 AND AND GatewaySubscriptionId IS NOT NULL");

                foreach(var org in orgs)
                {
                    DateTime? expDate = null;
                    if(org.Gateway == Core.Enums.GatewayType.Stripe)
                    {
                        var sub = await stripeSubscriptionService.GetAsync(org.GatewaySubscriptionId);
                        if(sub != null)
                        {
                            expDate = sub.CurrentPeriodEnd;
                        }
                    }

                    if(expDate.HasValue)
                    {
                        Console.WriteLine("Updating org {0} exp to {1}.", org.Id, expDate.Value);
                        await connection.ExecuteAsync(
                            "UPDATE [Organization] SET [ExpirationDate] = @Date WHERE [Id] = @Id",
                            new { Date = expDate, Id = org.Id });
                    }
                }
            }
        }
    }
}
