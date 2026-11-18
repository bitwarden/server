#nullable enable

using Bit.Core.Models.Api;

namespace Bit.Identity.Models.Response.Accounts;

/// <summary>Which organization here uses an identifier: its id alone, and nothing else of it.</summary>
public class OrganizationIdentifierLookupResponseModel : ResponseModel
{
    public OrganizationIdentifierLookupResponseModel(Guid? organizationId)
        : base("organizationIdentifierLookup")
    {
        OrganizationId = organizationId;
    }

    /// <summary>The organization whose identifier it is, or <c>null</c> when none here uses it.</summary>
    public Guid? OrganizationId { get; set; }
}
