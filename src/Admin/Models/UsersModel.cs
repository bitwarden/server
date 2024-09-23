namespace Bit.Admin.Models;

public class UsersModel : PagedModel<UserModel>
{
    public string Email { get; set; }
    public string Action { get; set; }
}
