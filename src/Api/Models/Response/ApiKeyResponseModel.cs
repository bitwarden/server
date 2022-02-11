using System;
using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Models.Response
{
    public class ApiKeyResponseModel : ResponseModel
    {
        public ApiKeyResponseModel(Organization organization, string obj = "apiKey")
            : base(obj)
        {
            if (organization == null)
            {
                throw new ArgumentNullException(nameof(organization));
            }
            ApiKey = organization.ApiKey;
        }

        public ApiKeyResponseModel(User user, string obj = "apiKey")
            : base(obj)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
            ApiKey = user.ApiKey;
        }

        public string ApiKey { get; set; }
    }
}
