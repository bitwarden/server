using System.ComponentModel.DataAnnotations;
using Bit.Api.KeyManagement.Models.Requests;

namespace Bit.Api.Auth.Models.Request.SsoJitProvisionFinish;

/// <summary>
/// Request for <c>sso-jit-provision-finish/key-connector</c>. Wraps the Key Management
/// <see cref="SetKeyConnectorKeyRequestModel"/> so that model stays unchanged.
/// </summary>
public class SsoJitProvisionFinishSetKeyConnectorKeyRequestModel
{
    // MVC validates nested models, so SetKeyConnectorKeyRequestModel.Validate() still runs for V1 and V2.
    [Required]
    public required SetKeyConnectorKeyRequestModel KeyConnectorKeys { get; init; }

    /// <summary>
    /// True moves the pending organization membership to Accepted. False leaves it Invited
    /// (open-invite links). The C# <c>required</c> modifier makes System.Text.Json reject a body without it.
    /// </summary>
    public required bool AcceptOrganizationInvite { get; init; }
}
