using Bit.Core.Models.Data;

namespace Bit.Admin.Models;

public class UsersModel : PagedModel<UserDetails>
{
    public string Email { get; set; }
    public string Action { get; set; }
}
