using System;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api
{
    public class ApiKeyResponseModel : ResponseModel
    {
        public ApiKeyResponseModel(Organization organization, string obj = "apiKey")
            : base(obj)
        {
            if(organization == null)
            {
                throw new ArgumentNullException(nameof(organization));
            }
            ApiKey = organization.ApiKey;
        }

        public string ApiKey { get; set; }
    }
}
