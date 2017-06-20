using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using Bit.Core.Utilities.Duo;
using System.Collections.Generic;
using System.Net.Http;

namespace Bit.Core.Identity
{
    public class DuoTokenProvider : IUserTwoFactorTokenProvider<User>
    {
        public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user)
        {
            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Duo);
            var canGenerate = user.TwoFactorProviderIsEnabled(TwoFactorProviderType.Duo)
                && !string.IsNullOrWhiteSpace(provider?.MetaData["UserId"]);

            return Task.FromResult(canGenerate);
        }

        /// <param name="purpose">Ex: "auto", "push", "passcode:123456", "sms", "phone"</param>
        public async Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user)
        {
            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Duo);
            var duoClient = new DuoApi(provider.MetaData["IKey"], provider.MetaData["SKey"], provider.MetaData["Host"]);
            var parts = purpose.Split(':');

            var parameters = new Dictionary<string, string>
            {
                ["async"] = "1",
                ["user_id"] = provider.MetaData["UserId"],
                ["factor"] = parts[0]
            };

            if(parameters["factor"] == "passcode" && parts.Length > 1)
            {
                parameters["passcode"] = parts[1];
            }
            else
            {
                parameters["device"] = "auto";
            }

            try
            {
                var response = await duoClient.JSONApiCallAsync<Dictionary<string, object>>(HttpMethod.Post,
                    "/auth/v2/auth", parameters);

                if(response.ContainsKey("txid"))
                {
                    var txId = response["txid"] as string;
                    return txId;
                }
            }
            catch(DuoException) { }

            return null;
        }

        public async Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
        {
            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Duo);
            var duoClient = new DuoApi(provider.MetaData["IKey"], provider.MetaData["SKey"], provider.MetaData["Host"]);

            var parameters = new Dictionary<string, string>
            {
                ["txid"] = token
            };

            try
            {
                var response = await duoClient.JSONApiCallAsync<Dictionary<string, object>>(HttpMethod.Get,
                    "/auth/v2/auth_status", parameters);

                var result = response["result"] as string;
                return string.Equals(result, "allow");
            }
            catch(DuoException)
            {
                // TODO: We might want to return true in some cases? What if Duo is down?
            }

            return false;
        }
    }
}
