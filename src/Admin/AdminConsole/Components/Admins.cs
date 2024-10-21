using Bit.Admin.AdminConsole.Models;
using Bit.Admin.Enums;
using Bit.Admin.Services;
using Microsoft.AspNetCore.Components;

namespace Bit.Admin.AdminConsole.Components;

public partial class Admins(
    IAccessControlService accessControlService) : ComponentBase
{
    private readonly bool _canResendEmailInvite = accessControlService.UserHasPermission(Permission.Provider_ResendEmailInvite);

    [Parameter] public ProviderViewModel Model { get; set; }
}
