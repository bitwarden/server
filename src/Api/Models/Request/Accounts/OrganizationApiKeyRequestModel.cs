using Bit.Core.Enums;

namespace Bit.Api.Models.Request.Accounts;

public class OrganizationApiKeyRequestModel : SecretVerificationRequestModel
{
    public OrganizationApiKeyType Type { get; set; }
}
