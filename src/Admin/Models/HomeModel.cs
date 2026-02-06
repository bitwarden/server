// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Settings;

namespace Bit.Admin.Models;

public class HomeModel
{
    public string CurrentVersion { get; set; }
    public GlobalSettings GlobalSettings { get; set; }
}
