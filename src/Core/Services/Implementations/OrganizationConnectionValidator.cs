using System.Text.Json;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Services
{


    public abstract class OrganizationConnectionValidator<T> : IOrganizationConnectionValidator
    {
        public async Task<OrganizationConnection> ValidateAsync(OrganizationConnection organizationConnection)
        {
            var configObject = organizationConnection.Config.RootElement.ToObject<T>();
            var fixedObject = await ValidateObjectAsync(organizationConnection, configObject);
            organizationConnection.Config = JsonHelpers.SerializeToDocument(fixedObject, JsonHelpers.IgnoreWritingNull);
            return organizationConnection;
        }

        public abstract Task<T> ValidateObjectAsync(OrganizationConnection organizationConnection, T config);
    }
}
