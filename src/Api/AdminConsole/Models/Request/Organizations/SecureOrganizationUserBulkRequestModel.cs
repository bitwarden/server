using System.ComponentModel.DataAnnotations;
using Bit.Api.Auth.Models.Request.Accounts;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class SecureOrganizationUserBulkRequestModel : SecretVerificationRequestModel
{
    [Required]
    public IEnumerable<Guid> Ids { get; set; }
}
