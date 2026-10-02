// File lives next to its owning domain (Auth) for discoverability; the namespace stays Bit.Core.Models
// so that existing callers do not need updating. A future cleanup can align namespace with file path.
namespace Bit.Core.Models;

public class AuthRequestPushNotification
{
    public Guid UserId { get; set; }
    public Guid Id { get; set; }
}
