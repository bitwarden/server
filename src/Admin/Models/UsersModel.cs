using Bit.Core.Models.Table;

namespace Bit.Admin.Models
{
    public class UsersModel : PagedModel<User>
    {
        public string Email { get; set; }
    }
}
