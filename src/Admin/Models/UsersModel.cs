// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Admin.Models;

public class UsersModel : PagedModel<UserViewModel>
{
    public string Email { get; set; }
    public string Action { get; set; }
}
