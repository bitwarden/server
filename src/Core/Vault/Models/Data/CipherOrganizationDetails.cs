using Bit.Core.Vault.Entities;

namespace Bit.Core.Vault.Models.Data;

public class CipherOrganizationDetails : Cipher
{
    public bool OrganizationUseTotp { get; set; }
}
