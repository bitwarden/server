using Bit.Core.Entities;

namespace Bit.Admin.Models;

public class UsersModel : PagedModel<User>
{
    public string Email { get; set; }
    public string Action { get; set; }
}
