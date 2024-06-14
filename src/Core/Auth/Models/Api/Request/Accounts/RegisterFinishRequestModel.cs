#nullable enable
namespace Bit.Core.Auth.Models.Api.Request.Accounts;

public class RegisterFinishRequestModel
{
    public string emailVerificationToken { get; set; }
    public string? orgInviteToken { get; set; }
}
