// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Admin.Models;

public class ErrorViewModel
{
    public string RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
