using Bit.Core.AdminConsole.Entities;
using Bit.HttpExtensions;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class OrganizationScopedApiKeyResponseModel : ResponseModel
{
    public OrganizationScopedApiKeyResponseModel(OrganizationScopedApiKey apiKey, string obj = "scopedApiKey")
        : base(obj)
    {
        ArgumentNullException.ThrowIfNull(apiKey);

        Id = apiKey.Id;
        ClientId = $"organization.{apiKey.OrganizationId}.{apiKey.Id}";
        Name = apiKey.Name;
        Scopes = apiKey.GetScopes();
        ExpireAt = apiKey.ExpireAt;
        CreationDate = apiKey.CreationDate;
    }

    public Guid Id { get; set; }
    public string ClientId { get; set; }
    public string Name { get; set; }
    public IEnumerable<string> Scopes { get; set; }
    public DateTime? ExpireAt { get; set; }
    public DateTime CreationDate { get; set; }
}
