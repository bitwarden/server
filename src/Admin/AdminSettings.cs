// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Admin;

public class AdminSettings
{
    public virtual string Admins { get; set; }
    public int? DeleteTrashDaysAgo { get; set; }
}
