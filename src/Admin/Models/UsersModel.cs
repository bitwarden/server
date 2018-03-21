using System.Collections;
using System.Collections.Generic;
using Bit.Core.Models.Table;

namespace Bit.Admin.Models
{
    public class UsersModel
    {
        public List<User> Users { get; set; }
        public string Email { get; set; }
        public int Page { get; set; }
        public int Count { get; set; }
        public int? PreviousPage => Page < 2 ? (int?)null : Page - 1;
        public int? NextPage => Users.Count < Count ? (int?)null : Page + 1;
    }
}
