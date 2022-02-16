using Bit.Core.Entities;
using Bit.Core.Models.Business.Tokenables;

namespace Bit.Api.Utilities
{
    public interface IApiKeyAuthorizationFeature
    {
        Installation Installation { get; }
        OrganizationApiKeyTokenable Token { get; set; }
    }

    public class ApiKeyAuthorizationFeature : IApiKeyAuthorizationFeature
    {
        public ApiKeyAuthorizationFeature(Installation installation, OrganizationApiKeyTokenable token)
        {
            Installation = installation;
            Token = token;
        }

        public Installation Installation { get; }
        public OrganizationApiKeyTokenable Token { get; set; }
    }
}
