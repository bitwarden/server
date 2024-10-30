using System.ComponentModel.DataAnnotations;
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

    [SupplyParameterFromForm(FormName = "ManageAdminsForm")]
    public ManageAdminsFormModel FormModel { get; set; } = new();

    [Parameter]
    public Guid ProviderId { get; set; }

    [Parameter]
    public Guid OwnerId { get; set; }

    [SupplyParameterFromQuery]
    public Guid? InviteResentTo { get; set; }

    private async Task OnValidSubmitAsync()
    {
        await ProviderService.ResendProviderSetupInviteEmailAsync(ProviderId, OwnerId);
        var uri = NavigationManager.GetUriWithQueryParameters($"/admin/providers/{ProviderId}/admins", new Dictionary<string, object?> { { "inviteResentTo", OwnerId.ToString() } });
        NavigationManager.NavigateTo(uri);
    }

    public class ManageAdminsFormModel
    {
        [Required]
        public Guid UserId { get; set; }
    }
}
