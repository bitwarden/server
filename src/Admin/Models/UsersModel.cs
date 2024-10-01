namespace Bit.Admin.Models;

public class UsersModel : PagedModel<UserViewModel>
{
    public string Email { get; set; }
    public string Action { get; set; }
}
