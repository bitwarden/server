using Bit.Core.Vault.Enums;
using Bit.HttpExtensions;

namespace Bit.Api.Vault.Models.Response;

public class OrganizationCipherTypeCountResponseModel : ResponseModel
{
    public OrganizationCipherTypeCountResponseModel(CipherType type, int count)
        : base("organizationCipherTypeCount")
    {
        Type = type;
        Count = count;
    }

    /// <summary>
    /// The cipher type.
    /// </summary>
    public CipherType Type { get; set; }

    /// <summary>
    /// Number of organization-owned ciphers of this type, including trashed items.
    /// </summary>
    public int Count { get; set; }
}
